using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using TuyaHub.Domain.ValueObjects;
using TuyaHub.Infrastructure.Options;
using TuyaHub.Infrastructure.Tuya.Codec;
using Xunit;
using Xunit.Abstractions;

namespace TuyaHub.Tests.Tuya;

/// <summary>
/// THROWAWAY live probe (not a real test). Talks to VentilatorVictor (3.5) directly, toggles the light
/// power (DP 20) several times and reads DP 23 after each power-on — WITHOUT ever writing DP 23. Answers:
/// does the device change its own colour temperature on a power cycle? Run explicitly:
///   dotnet test --filter FullyQualifiedName~LightCctProbe
/// Delete when done.
/// </summary>
public class LightCctProbe(ITestOutputHelper output)
{
    private const string LogPath = @"C:\Users\thoma\AppData\Local\Temp\claude\C--Users-thoma-source-repos-thomasgodon-tuya-hub\a993db75-3d31-4224-a6e2-72f173e5228c\scratchpad\cct-probe.log";

    private static readonly TuyaDeviceOptions Victor = new()
    {
        Name = "VentilatorVictor",
        IpAddress = "192.168.0.194",
        DeviceId = "bf7c7c541fec5351d5pxss",
        LocalKey = "HbG@mk(sRKPk1+sB",
        ProtocolVersion = "3.5",
        Port = 6668,
    };

    [Fact]
    public async Task Probe_light_power_toggle_effect_on_cct()
    {
        var log = new StringBuilder();
        void Line(string s) { log.AppendLine(s); output.WriteLine(s); }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var ct = cts.Token;

        var codec = new TuyaSessionCodec(
            DeviceName.Create(Victor.Name), Victor, ProtocolVersion.Create("3.5"),
            TimeSpan.FromSeconds(5), NullLogger.Instance);

        using var client = new TcpClient();
        await client.ConnectAsync(Victor.IpAddress, Victor.Port, ct);
        var stream = client.GetStream();

        await codec.NegotiateSessionAsync(stream, ct);
        Line("session negotiated");

        var baseline = await QueryDpsAsync(stream, codec, ct);
        Line($"baseline dp20={baseline["20"]} dp23={baseline["23"]}");
        Line("");

        // Narrated, user-observable experiment: does writing DP 23 right after power-on pin the CCT?
        // (int?)null CCT means "no DP 23 write" for the baseline phase.
        var phases = new (string Label, int? Cct)[]
        {
            ("phase 1: ON, NO cct write (baseline color)", null),
            ("phase 2: ON + write DP23=0    (expect white)", 0),
            ("phase 3: ON + write DP23=1000 (expect warm)", 1000),
            ("phase 4: ON + write DP23=0    (expect white)", 0),
            ("phase 5: ON + write DP23=0    (expect white, repeat)", 0),
        };

        foreach (var (label, cct) in phases)
        {
            // OFF first so every phase starts from a fresh power-on.
            await SendDpAsync(stream, codec, new Dictionary<string, object> { ["20"] = false }, ct);
            await Task.Delay(1500, ct);

            await SendDpAsync(stream, codec, new Dictionary<string, object> { ["20"] = true }, ct);
            if (cct is { } target)
            {
                await Task.Delay(300, ct);
                await SendDpAsync(stream, codec, new Dictionary<string, object> { ["23"] = target }, ct);
            }

            Line($">> {label}  (hold ~8s, observe now)");
            await Task.Delay(8000, ct);
        }

        // Leave the light on at white.
        await SendDpAsync(stream, codec, new Dictionary<string, object> { ["20"] = true }, ct);
        await SendDpAsync(stream, codec, new Dictionary<string, object> { ["23"] = 0 }, ct);

        await File.WriteAllTextAsync(LogPath, log.ToString(), ct);
    }

    private static async Task SendDpAsync(
        NetworkStream stream, TuyaSessionCodec codec, Dictionary<string, object> dps, CancellationToken ct)
    {
        await stream.WriteAsync(codec.BuildControl(dps), ct);
    }

    /// <summary>
    /// Drains any queued pushes, then sends DP_QUERY and returns the first FULL snapshot (a frame that
    /// carries DP 23) — partial STATUS pushes that echo a single changed DP are skipped.
    /// </summary>
    private static async Task<JObject> QueryDpsAsync(NetworkStream stream, TuyaSessionCodec codec, CancellationToken ct)
    {
        // Drain whatever is already queued so we don't read a stale push.
        var drainChunk = new byte[1024];
        while (stream.DataAvailable)
        {
            await stream.ReadAsync(drainChunk, ct);
        }

        await stream.WriteAsync(codec.BuildQuery(), ct);

        var buffer = new List<byte>(1024);
        var chunk = new byte[1024];
        using var readCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        readCts.CancelAfter(TimeSpan.FromSeconds(8));

        while (true)
        {
            var read = await stream.ReadAsync(chunk, readCts.Token);
            if (read == 0)
            {
                throw new IOException("connection closed during query");
            }

            buffer.AddRange(chunk[..read]);
            while (codec.TryReadMessage(buffer, out var json))
            {
                if (json is null)
                {
                    continue;
                }

                var root = JObject.Parse(json);
                var dps = root["dps"] as JObject ?? (root["data"] as JObject)?["dps"] as JObject;
                // Only accept the full snapshot (has the CCT DP), not a single-DP push echo.
                if (dps is not null && dps["23"] is not null)
                {
                    return dps;
                }
            }
        }
    }
}
