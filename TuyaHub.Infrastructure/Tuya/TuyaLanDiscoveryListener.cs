using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using com.clusterrr.TuyaNet;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace TuyaHub.Infrastructure.Tuya;

/// <summary>
/// Own UDP listener for Tuya LAN discovery beacons, replacing TuyaNet's <see cref="TuyaScanner"/>.
///
/// <para><b>Why not <see cref="TuyaScanner"/>:</b> its listener runs on a raw <see cref="Thread"/> whose
/// loop <em>rethrows</em> any decode failure (<c>catch { if (!running) return; throw; }</c>). A single
/// undecodable datagram on UDP 6667 — most commonly a <b>protocol-3.5 beacon</b> (<c>00 00 66 99</c> /
/// AES-GCM framing, which the 2022-era library predates), or stray/malformed UDP — throws
/// <see cref="System.IO.InvalidDataException"/> on that library-owned thread, which becomes an unhandled
/// exception and terminates the whole host. That fault never crosses our stack frames, so it cannot be
/// caught around the scanner; the only robust fix is to own the receive loop and wrap each packet.</para>
///
/// <para>This listener binds the same ports (6666 for 3.1, 6667 for 3.3/3.4) and reuses TuyaNet's tested
/// codec (<c>internal static TuyaParser.DecodeResponse</c>, reached via a cached reflection delegate — the
/// PRD forbids hand-rolling protocol 3.3, and TuyaNet is pinned/unmaintained so the internal API is
/// effectively frozen). Each datagram is decoded inside a <c>try/catch</c>, so an undecodable packet is
/// logged at Debug and skipped instead of crashing the host. Purely read-only: it binds and listens only.</para>
/// </summary>
internal sealed class TuyaLanDiscoveryListener : IAsyncDisposable
{
    /// <summary>Universal, non-secret discovery key; the AES key is its MD5 (matches TuyaNet/tinytuya).</summary>
    private const string UdpKey = "yGAdlopoPVldABfn";

    private const int Udp31Port = 6666;
    private const int Udp33Port = 6667;

    private delegate TuyaLocalResponse DecodeResponse(byte[] data, byte[] key, TuyaProtocolVersion version);

    private readonly byte[] _aesKey;
    private readonly DecodeResponse _decode;
    private readonly Action<TuyaDeviceScanInfo> _onBeacon;
    private readonly ILogger _logger;

    private UdpClient? _udp31;
    private UdpClient? _udp33;
    private Task? _loop31;
    private Task? _loop33;

    public TuyaLanDiscoveryListener(Action<TuyaDeviceScanInfo> onBeacon, ILogger logger)
    {
        _onBeacon = onBeacon;
        _logger = logger;

        using var md5 = MD5.Create();
        _aesKey = md5.ComputeHash(Encoding.ASCII.GetBytes(UdpKey));

        // Reuse TuyaNet's own frame/AES/JSON codec rather than re-implementing protocol 3.3. TuyaParser is
        // internal, so bind to it once via reflection and fail loud if a future package bump renames it.
        var parser = typeof(TuyaScanner).Assembly.GetType("com.clusterrr.TuyaNet.TuyaParser")
            ?? throw new InvalidOperationException("TuyaNet.TuyaParser type not found; discovery codec unavailable.");
        var method = parser.GetMethod(
            "DecodeResponse",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
            binder: null,
            types: [typeof(byte[]), typeof(byte[]), typeof(TuyaProtocolVersion)],
            modifiers: null)
            ?? throw new InvalidOperationException("TuyaNet.TuyaParser.DecodeResponse not found; discovery codec unavailable.");
        _decode = method.CreateDelegate<DecodeResponse>();
    }

    /// <summary>
    /// Binds both discovery ports and starts the receive loops. Throws on a bind failure (e.g. the port is
    /// already in use) so the caller can decide to continue without discovery.
    /// </summary>
    public void Start(CancellationToken stoppingToken)
    {
        _udp31 = Bind(Udp31Port);
        _udp33 = Bind(Udp33Port);
        _loop31 = ReceiveLoopAsync(_udp31, TuyaProtocolVersion.V31, stoppingToken);
        _loop33 = ReceiveLoopAsync(_udp33, TuyaProtocolVersion.V33, stoppingToken);
    }

    private static UdpClient Bind(int port)
    {
        // Bind manually with SO_REUSEADDR so a restart doesn't hit "address already in use" (the library
        // binds bare via `new UdpClient(port)`).
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        socket.Bind(new IPEndPoint(IPAddress.Any, port));
        return new UdpClient { Client = socket };
    }

    private async Task ReceiveLoopAsync(UdpClient udp, TuyaProtocolVersion version, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            byte[] data;
            try
            {
                var result = await udp.ReceiveAsync(stoppingToken);
                data = result.Buffer;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return; // normal shutdown
            }
            catch (ObjectDisposedException)
            {
                return; // socket closed during shutdown
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Tuya discovery socket receive failed on UDP {Port}; continuing.", version == TuyaProtocolVersion.V31 ? Udp31Port : Udp33Port);
                continue;
            }

            // Per-packet decode boundary — the crash-safety this whole class exists for. An undecodable
            // beacon (protocol-3.5 66 99 frame, junk UDP, truncated packet) is skipped, not fatal.
            try
            {
                // Cheap prefix gate: skip anything that isn't a 00 00 55 AA frame before touching the codec.
                // This silently drops protocol-3.5 (00 00 66 99) beacons and non-Tuya traffic.
                if (data.Length < 4 || data[2] != 0x55 || data[3] != 0xAA)
                {
                    continue;
                }

                var response = _decode(data, _aesKey, version);
                if (string.IsNullOrEmpty(response.JSON))
                {
                    continue;
                }

                // Newtonsoft matches gwId->GwId etc. case-insensitively; this is exactly what
                // TuyaScanner.Parse does with the same public model.
                var info = JsonConvert.DeserializeObject<TuyaDeviceScanInfo>(response.JSON);
                if (info is null || string.IsNullOrWhiteSpace(info.GwId))
                {
                    continue;
                }

                _onBeacon(info);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Skipped an undecodable Tuya discovery packet on UDP {Port}.", version == TuyaProtocolVersion.V31 ? Udp31Port : Udp33Port);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _udp31?.Dispose();
        _udp33?.Dispose();

        foreach (var loop in new[] { _loop31, _loop33 })
        {
            if (loop is null)
            {
                continue;
            }

            try
            {
                await loop;
            }
            catch
            {
                // Loops swallow their own errors; nothing meaningful can surface here.
            }
        }
    }
}
