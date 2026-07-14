using System.Net.Sockets;
using com.clusterrr.TuyaNet;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TuyaHub.Application.Abstractions;
using TuyaHub.Domain;
using TuyaHub.Domain.ValueObjects;
using TuyaHub.Infrastructure.Options;
using TuyaHub.Infrastructure.Profiles;
using TuyaHub.Infrastructure.Resilience;

namespace TuyaHub.Infrastructure.Tuya;

/// <summary>
/// A supervised, persistent local connection to one device. TuyaNet is used purely as the
/// 3.3 codec (<see cref="TuyaDevice.EncodeRequest"/> / <see cref="TuyaDevice.DecodeResponse"/>); this
/// class owns the single TCP socket and adds the reliability layer TuyaNet lacks:
/// <list type="bullet">
/// <item>a <b>heartbeat</b> to keep the module from dropping the idle socket (~30 s);</item>
/// <item>a continuous <b>read loop</b> that decodes unsolicited STATUS pushes into domain reports;</item>
/// <item>a <b>poll loop</b> (DP_QUERY) as the backstop for RF-remote changes that do not push;</item>
/// <item>a <b>liveness watchdog</b> that force-reconnects a stalled/half-open socket when no inbound
/// bytes arrive within <c>livenessTimeout</c> (UC-09 step 1 — heartbeat/poll silence, not just a TCP error);</item>
/// <item><b>reconnect with jittered backoff</b>, keeping exactly one socket per device (the module allows only ~3).</item>
/// </list>
/// </summary>
internal sealed class TuyaConnection(
    DeviceName name,
    TuyaDeviceOptions options,
    DeviceProfile profile,
    TimeSpan pollInterval,
    TimeSpan heartbeatInterval,
    TimeSpan livenessTimeout,
    TimeSpan connectTimeout,
    TimeSpan backoffInitial,
    TimeSpan backoffMax,
    IDeviceStateIngestionService ingestion,
    ILogger logger)
{
    private static readonly byte[] FramePrefix = [0x00, 0x00, 0x55, 0xAA];

    /// <summary>Monotonic timestamp (ms) of the last inbound byte; drives the liveness watchdog.</summary>
    private long _lastInboundTicks;

    private readonly TuyaDevice _codec = new(
        options.IpAddress,
        options.LocalKey,
        options.DeviceId,
        options.ProtocolVersion == ProtocolVersion.V31 ? TuyaProtocolVersion.V31 : TuyaProtocolVersion.V33,
        options.Port,
        receiveTimeout: 250);

    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly List<byte> _buffer = new(1024);

    private TcpClient? _client;
    private NetworkStream? _stream;

    public DeviceName Name => name;

    public bool IsConnected => _stream is not null && _client?.Connected == true;

    /// <summary>Supervises the connection for the app lifetime: connect, run the loops, reconnect on failure.</summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var backoff = new BackoffPolicy(backoffInitial, backoffMax);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ConnectAsync(cancellationToken);
                logger.LogInformation("Connected to Tuya device {Device} at {Ip}.", name, options.IpAddress);
                await ingestion.ReportConnectivityAsync(name, online: true, cancellationToken);
                backoff.Reset();
                _lastInboundTicks = Environment.TickCount64;

                // Full state sync on (re)connect so the KNX status GAs are correct immediately.
                await SendQueryAsync(cancellationToken);

                using var loopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var read = ReadLoopAsync(loopCts.Token);
                var heartbeat = HeartbeatLoopAsync(loopCts.Token);
                var poll = PollLoopAsync(loopCts.Token);
                var watchdog = WatchdogLoopAsync(loopCts.Token);

                await Task.WhenAny(read, heartbeat, poll, watchdog);
                await loopCts.CancelAsync();
                await Task.WhenAll(Swallow(read), Swallow(heartbeat), Swallow(poll), Swallow(watchdog));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Tuya device {Device} connection error.", name);
            }
            finally
            {
                CloseSocket();
            }

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            await ingestion.ReportConnectivityAsync(name, online: false, CancellationToken.None);
            var delay = backoff.Next();
            logger.LogInformation("Tuya device {Device} offline; reconnecting in {Backoff:0.#}s.", name, delay.TotalSeconds);

            try
            {
                await Task.Delay(delay, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        CloseSocket();
    }

    /// <summary>Sends a control write. A no-op when offline (commands during an outage are dropped, per UC-09).</summary>
    public async Task SendCommandAsync(DeviceCommand command, CancellationToken cancellationToken)
    {
        if (command.IsEmpty)
        {
            return;
        }

        if (!IsConnected)
        {
            logger.LogWarning("Dropping command for offline device {Device}.", name);
            return;
        }

        var dps = TuyaProfileCodec.ToDps(profile, command);
        var json = JsonConvert.SerializeObject(new Dictionary<string, object> { ["dps"] = dps });
        json = _codec.FillJson(json);
        var frame = _codec.EncodeRequest(TuyaCommand.CONTROL, json);
        await WriteAsync(frame, cancellationToken);
    }

    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        var client = new TcpClient();
        using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        connectCts.CancelAfter(connectTimeout);

        await client.ConnectAsync(options.IpAddress, options.Port, connectCts.Token);

        _client = client;
        _stream = client.GetStream();
        _buffer.Clear();
    }

    private async Task SendQueryAsync(CancellationToken cancellationToken)
    {
        var frame = _codec.EncodeRequest(TuyaCommand.DP_QUERY, _codec.FillJson(null));
        await WriteAsync(frame, cancellationToken);
    }

    private async Task SendHeartbeatAsync(CancellationToken cancellationToken)
    {
        var frame = _codec.EncodeRequest(TuyaCommand.HEART_BEAT, "{}");
        await WriteAsync(frame, cancellationToken);
    }

    private async Task WriteAsync(byte[] frame, CancellationToken cancellationToken)
    {
        var stream = _stream ?? throw new IOException("Tuya socket is not connected.");
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await stream.WriteAsync(frame, cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        var stream = _stream ?? throw new IOException("Tuya socket is not connected.");
        var chunk = new byte[1024];

        while (!cancellationToken.IsCancellationRequested)
        {
            var read = await stream.ReadAsync(chunk, cancellationToken);
            if (read == 0)
            {
                throw new IOException($"Tuya device {name} closed the connection.");
            }

            // Any inbound byte (STATUS push, poll reply, or heartbeat ack) proves liveness.
            _lastInboundTicks = Environment.TickCount64;
            _buffer.AddRange(chunk[..read]);
            await ProcessBufferAsync(cancellationToken);
        }
    }

    private async Task ProcessBufferAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            AlignToPrefix();
            if (_buffer.Count < 16)
            {
                return;
            }

            var length = (_buffer[12] << 24) | (_buffer[13] << 16) | (_buffer[14] << 8) | _buffer[15];
            var frameSize = 16 + length;
            if (length <= 0 || frameSize > 1 << 20)
            {
                // Corrupt length; drop the prefix and resynchronise.
                _buffer.RemoveRange(0, 4);
                continue;
            }

            if (_buffer.Count < frameSize)
            {
                return;
            }

            var frame = _buffer.GetRange(0, frameSize).ToArray();
            _buffer.RemoveRange(0, frameSize);
            await HandleFrameAsync(frame, cancellationToken);
        }
    }

    private async Task HandleFrameAsync(byte[] frame, CancellationToken cancellationToken)
    {
        DeviceReport report;
        try
        {
            var response = _codec.DecodeResponse(frame);
            if (string.IsNullOrEmpty(response.JSON))
            {
                return; // heartbeat ack / empty acknowledgement
            }

            var root = JObject.Parse(response.JSON);
            var dpsToken = root["dps"] as JObject ?? (root["data"] as JObject)?["dps"] as JObject;
            if (dpsToken is null)
            {
                return;
            }

            var dps = new Dictionary<int, object>();
            foreach (var property in dpsToken.Properties())
            {
                if (int.TryParse(property.Name, out var id) && property.Value is JValue { Value: { } value })
                {
                    dps[id] = value;
                }
            }

            if (dps.Count == 0)
            {
                return;
            }

            report = TuyaProfileCodec.ToReport(profile, dps);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to decode a frame from device {Device}; ignoring.", name);
            return;
        }

        await ingestion.ReportStateAsync(name, report, cancellationToken);
    }

    private void AlignToPrefix()
    {
        if (StartsWithPrefix(0))
        {
            return;
        }

        for (var i = 1; i <= _buffer.Count - FramePrefix.Length; i++)
        {
            if (StartsWithPrefix(i))
            {
                _buffer.RemoveRange(0, i);
                return;
            }
        }

        // No prefix found; keep only the last 3 bytes (a prefix may be split across reads).
        if (_buffer.Count > FramePrefix.Length - 1)
        {
            _buffer.RemoveRange(0, _buffer.Count - (FramePrefix.Length - 1));
        }
    }

    private bool StartsWithPrefix(int offset)
    {
        if (offset + FramePrefix.Length > _buffer.Count)
        {
            return false;
        }

        for (var i = 0; i < FramePrefix.Length; i++)
        {
            if (_buffer[offset + i] != FramePrefix[i])
            {
                return false;
            }
        }

        return true;
    }

    private async Task HeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(heartbeatInterval, cancellationToken);
            await SendHeartbeatAsync(cancellationToken);
        }
    }

    private async Task PollLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(pollInterval, cancellationToken);
            await SendQueryAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Liveness watchdog (UC-09 step 1). The heartbeat/poll loops keep provoking the device; if none of
    /// those elicit any inbound byte within <paramref name="livenessTimeout"/>, the socket is stalled or
    /// half-open, so we throw to trip the reconnect path — a TCP error may never surface on its own.
    /// </summary>
    private async Task WatchdogLoopAsync(CancellationToken cancellationToken)
    {
        // Check at least twice per liveness window (bounded by the heartbeat cadence) for prompt detection.
        var checkInterval = TimeSpan.FromTicks(Math.Min(heartbeatInterval.Ticks, livenessTimeout.Ticks / 2));
        if (checkInterval < TimeSpan.FromMilliseconds(100))
        {
            checkInterval = TimeSpan.FromMilliseconds(100);
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(checkInterval, cancellationToken);

            var idle = Environment.TickCount64 - _lastInboundTicks;
            if (idle > livenessTimeout.TotalMilliseconds)
            {
                throw new IOException(
                    $"Tuya device {name} silent for {idle / 1000.0:0.#}s (> {livenessTimeout.TotalSeconds:0.#}s); reconnecting.");
            }
        }
    }

    private void CloseSocket()
    {
        try
        {
            _stream?.Dispose();
            _client?.Dispose();
        }
        catch
        {
            // best-effort cleanup
        }
        finally
        {
            _stream = null;
            _client = null;
        }
    }

    private static async Task Swallow(Task task)
    {
        try
        {
            await task;
        }
        catch
        {
            // loop faults are expected on teardown
        }
    }
}
