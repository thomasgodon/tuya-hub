using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TuyaHub.Domain.ValueObjects;
using TuyaHub.Infrastructure.Options;

namespace TuyaHub.Infrastructure.Tuya.Codec;

/// <summary>
/// Hand-rolled codec for Tuya local protocol <b>3.4</b> and <b>3.5</b>, which the TuyaNet library does
/// not support. Both require a per-connection session key negotiated over a 3-message handshake
/// (<c>SESS_KEY_NEG_START/RESP/FINISH</c>) before any datapoint traffic; 3.4 then uses the 55AA frame
/// with AES-ECB + HMAC-SHA256, while 3.5 uses the 6699 frame with AES-GCM. The two share the handshake
/// and command envelope and differ only in framing/cipher, selected by <see cref="_isGcm"/>.
///
/// <para>Control uses the newer <c>CONTROL_NEW</c> (13) command with a <c>{"protocol":5,"data":{"dps":…}}</c>
/// envelope and DP reads use <c>DP_QUERY_NEW</c> (16), matching what 3.4/3.5 firmware expects.</para>
/// </summary>
internal sealed class TuyaSessionCodec : ITuyaCodec
{
    private const uint CmdHeartbeat = 9;
    private const uint CmdSessKeyNegStart = 3;
    private const uint CmdSessKeyNegResp = 4;
    private const uint CmdSessKeyNegFinish = 5;
    private const uint CmdControlNew = 13;
    private const uint CmdDpQueryNew = 16;

    private readonly DeviceName _name;
    private readonly TuyaDeviceOptions _options;
    private readonly byte[] _localKey;
    private readonly byte[] _versionHeader;
    private readonly bool _isGcm;
    private readonly TimeSpan _handshakeTimeout;
    private readonly ILogger _logger;

    /// <summary>The key currently in force: the local key until negotiation completes, then the session key.</summary>
    private byte[] _key;
    private uint _seq = 1;

    public ProtocolVersion Version { get; }

    public TuyaSessionCodec(
        DeviceName name,
        TuyaDeviceOptions options,
        ProtocolVersion version,
        TimeSpan handshakeTimeout,
        ILogger logger)
    {
        _name = name;
        _options = options;
        Version = version;
        _isGcm = version.Value == ProtocolVersion.V35;

        _localKey = Encoding.ASCII.GetBytes(options.LocalKey);
        if (_localKey.Length != 16)
        {
            throw new ArgumentException(
                $"Tuya local key for device '{name}' must be 16 bytes; got {_localKey.Length}.", nameof(options));
        }

        _key = _localKey;
        // Version header prepended before encryption for CONTROL_NEW: e.g. "3.4" + 12 zero bytes.
        _versionHeader = [.. Encoding.ASCII.GetBytes(version.Value), .. new byte[12]];
        _handshakeTimeout = handshakeTimeout;
        _logger = logger;
    }

    public async Task NegotiateSessionAsync(Stream stream, CancellationToken cancellationToken)
    {
        // Reset per-connection state so a reconnect re-negotiates from the real local key.
        _key = _localKey;
        _seq = 1;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_handshakeTimeout);
        var ct = cts.Token;

        var localNonce = RandomNumberGenerator.GetBytes(16);

        // Step 1 — send our nonce (keyed by the local key; no version header).
        await stream.WriteAsync(BuildFrame(CmdSessKeyNegStart, localNonce), ct);

        // Step 2 — device replies with its nonce + HMAC(local_key, our nonce); verify it.
        var (cmd, payload) = ParseFrame(await ReadFrameAsync(stream, ct));
        if (cmd != CmdSessKeyNegResp || payload.Length < 48)
        {
            throw new IOException(
                $"Tuya {_name}: unexpected session-negotiation response (cmd={cmd}, len={payload.Length}).");
        }

        var remoteNonce = payload[..16];
        var remoteHmac = payload[16..48];
        if (!CryptographicOperations.FixedTimeEquals(remoteHmac, TuyaCrypto.HmacSha256(_localKey, localNonce)))
        {
            throw new IOException($"Tuya {_name}: session-negotiation HMAC mismatch (wrong local key?).");
        }

        // Step 3 — prove we hold the local key by returning HMAC(local_key, device nonce).
        await stream.WriteAsync(BuildFrame(CmdSessKeyNegFinish, TuyaCrypto.HmacSha256(_localKey, remoteNonce)), ct);

        // Both sides now derive the same session key; all later traffic uses it.
        _key = _isGcm
            ? TuyaCrypto.DeriveSessionKey35(_localKey, localNonce, remoteNonce)
            : TuyaCrypto.DeriveSessionKey34(_localKey, localNonce, remoteNonce);

        _logger.LogDebug("Tuya {Device}: {Version} session key negotiated.", _name, Version);
    }

    public byte[] BuildControl(IReadOnlyDictionary<string, object> dps)
    {
        var envelope = new Dictionary<string, object>
        {
            ["protocol"] = 5,
            ["t"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ["data"] = new Dictionary<string, object> { ["dps"] = dps },
        };
        var payload = WithVersionHeader(JsonConvert.SerializeObject(envelope));
        return BuildFrame(CmdControlNew, payload);
    }

    public byte[] BuildQuery()
    {
        // DP_QUERY_NEW is in the no-protocol-header command set — send the base envelope unprefixed.
        var envelope = new Dictionary<string, object>
        {
            ["gwId"] = _options.DeviceId,
            ["devId"] = _options.DeviceId,
            ["uid"] = _options.DeviceId,
            ["t"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        };
        return BuildFrame(CmdDpQueryNew, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(envelope)));
    }

    public byte[] BuildHeartbeat() => BuildFrame(CmdHeartbeat, "{}"u8.ToArray());

    public bool TryReadMessage(List<byte> buffer, out string? json)
    {
        json = null;
        var frame = _isGcm ? TuyaFrame.TryExtract6699(buffer) : TuyaFrame.TryExtract55AA(buffer);
        if (frame is null)
        {
            return false;
        }

        try
        {
            var (_, plaintext) = ParseFrame(frame);
            json = ExtractJson(plaintext);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to decode a {Version} frame from {Device}; ignoring.", Version, _name);
            json = null;
        }

        return true;
    }

    private byte[] WithVersionHeader(string json) => [.. _versionHeader, .. Encoding.UTF8.GetBytes(json)];

    private byte[] BuildFrame(uint cmd, byte[] payload) => _isGcm
        ? TuyaFrame.Build6699(_seq++, cmd, payload, _key)
        : TuyaFrame.Build55AA(_seq++, cmd, payload, _key);

    private (uint Cmd, byte[] Plaintext) ParseFrame(byte[] frame) => _isGcm
        ? TuyaFrame.Parse6699(frame, _key)
        : TuyaFrame.Parse55AA(frame, _key);

    /// <summary>Skips any leading version header / padding — the JSON body starts at the first <c>{</c>.</summary>
    private static string? ExtractJson(byte[] plaintext)
    {
        var start = Array.IndexOf(plaintext, (byte)'{');
        return start < 0 ? null : Encoding.UTF8.GetString(plaintext, start, plaintext.Length - start);
    }

    private async Task<byte[]> ReadFrameAsync(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = new List<byte>(256);
        var chunk = new byte[512];
        while (true)
        {
            var read = await stream.ReadAsync(chunk, cancellationToken);
            if (read == 0)
            {
                throw new IOException($"Tuya {_name}: connection closed during session negotiation.");
            }

            buffer.AddRange(chunk[..read]);
            var frame = _isGcm ? TuyaFrame.TryExtract6699(buffer) : TuyaFrame.TryExtract55AA(buffer);
            if (frame is not null)
            {
                return frame;
            }
        }
    }
}
