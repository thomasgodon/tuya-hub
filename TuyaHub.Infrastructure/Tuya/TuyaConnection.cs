using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using TuyaHub.Application.Abstractions;
using TuyaHub.Domain;
using TuyaHub.Domain.ValueObjects;
using TuyaHub.Infrastructure.Options;
using TuyaHub.Infrastructure.Profiles;
using TuyaHub.Infrastructure.Resilience;
using TuyaHub.Infrastructure.Tuya.Codec;

namespace TuyaHub.Infrastructure.Tuya;

/// <summary>
/// A supervised, persistent local connection to one device. All wire concerns (framing, encryption,
/// and the 3.4/3.5 session handshake) live behind <see cref="ITuyaCodec"/>; this class is transport-only
/// and owns the single TCP socket plus the reliability layer the codec lacks:
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
    /// <summary>Monotonic timestamp (ms) of the last inbound byte; drives the liveness watchdog.</summary>
    private long _lastInboundTicks;

    private readonly ITuyaCodec _codec = TuyaCodecFactory.Create(name, options, connectTimeout, logger);

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
                // 3.4/3.5 negotiate a per-connection session key before any DP traffic; no-op for 3.1/3.3.
                await _codec.NegotiateSessionAsync(_stream!, cancellationToken);
                logger.LogInformation("Connected to Tuya device {Device} at {Ip}.", name, options.IpAddress);
                await ingestion.ReportConnectivityAsync(name, online: true, cancellationToken);
                backoff.Reset();
                _lastInboundTicks = Environment.TickCount64;

                // Full state sync on (re)connect so the KNX status GAs are correct immediately.
                await SendQueryAsync(cancellationToken);

                // Force any profile-declared baseline (Wind Calm silences the DP 66 buzzer) so no LAN
                // command beeps. Blind write — inert on firmware that doesn't implement the DP.
                if (profile.OnConnectDps.Count > 0)
                {
                    await WriteAsync(_codec.BuildControl(profile.OnConnectDps), cancellationToken);
                }

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
        var frame = _codec.BuildControl(dps);
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
        await WriteAsync(_codec.BuildQuery(), cancellationToken);
    }

    private async Task SendHeartbeatAsync(CancellationToken cancellationToken)
    {
        await WriteAsync(_codec.BuildHeartbeat(), cancellationToken);
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
        // The codec pulls one frame per call and decodes it; a null json is an ack/undecodable frame.
        while (_codec.TryReadMessage(_buffer, out var json))
        {
            if (json is not null)
            {
                await HandleJsonAsync(json, cancellationToken);
            }
        }
    }

    private async Task HandleJsonAsync(string json, CancellationToken cancellationToken)
    {
        DeviceReport report;
        try
        {
            var root = JObject.Parse(json);
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
            logger.LogDebug(ex, "Failed to interpret a status payload from device {Device}; ignoring.", name);
            return;
        }

        await ingestion.ReportStateAsync(name, report, cancellationToken);
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
