using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using TuyaHub.Domain.ValueObjects;
using TuyaHub.Infrastructure.Options;
using TuyaHub.Infrastructure.Tuya.Codec;
using Xunit;

namespace TuyaHub.Tests.Tuya;

/// <summary>
/// End-to-end exercise of the 3.4/3.5 session codec against an in-memory device that speaks the same
/// protocol: the negotiation handshake, HMAC verification both ways, session-key agreement, and the
/// CONTROL_NEW / DP_QUERY_NEW envelopes — all validated by having the fake device decrypt what the codec
/// produced with its own independently-derived session key.
/// </summary>
public class TuyaSessionCodecTests
{
    private const string LocalKey = "0123456789abcdef";

    private static TuyaDeviceOptions Options(string version) => new()
    {
        Name = "test",
        IpAddress = "127.0.0.1",
        DeviceId = "bfdevice123",
        LocalKey = LocalKey,
        ProtocolVersion = version,
        Port = 6668,
    };

    private static TuyaSessionCodec Codec(string version) => new(
        DeviceName.Create("test"),
        Options(version),
        ProtocolVersion.Create(version),
        TimeSpan.FromSeconds(5),
        NullLogger.Instance);

    [Theory]
    [InlineData("3.4")]
    [InlineData("3.5")]
    public async Task Handshake_agrees_a_session_key_and_control_frames_decrypt(string version)
    {
        var codec = Codec(version);
        var device = new FakeTuyaDevice(Encoding.ASCII.GetBytes(LocalKey), isGcm: version == "3.5");

        await codec.NegotiateSessionAsync(device, CancellationToken.None);

        // The device accepted our FINISH HMAC — proves the handshake completed correctly.
        Assert.True(device.FinishVerified);

        // A control frame built with the codec's session key must decrypt under the device's — i.e. the
        // two independently-derived session keys match.
        var frame = codec.BuildControl(new Dictionary<string, object> { ["1"] = true, ["62"] = 3 });
        var json = device.DecryptToJson(frame);
        var root = JObject.Parse(json);

        Assert.Equal(5, (int)root["protocol"]!);
        Assert.Equal(3, (int)root["data"]!["dps"]!["62"]!);
        Assert.Equal(true, (bool)root["data"]!["dps"]!["1"]!);
    }

    [Theory]
    [InlineData("3.4")]
    [InlineData("3.5")]
    public async Task Handshake_throws_when_the_response_hmac_is_wrong(string version)
    {
        var codec = Codec(version);
        // Frames still decode (same key), but the device returns an HMAC keyed by the wrong key — the
        // exact tampering the codec's step-2 verification must reject.
        var device = new FakeTuyaDevice(Encoding.ASCII.GetBytes(LocalKey), isGcm: version == "3.5", corruptResponseHmac: true);

        await Assert.ThrowsAsync<IOException>(() => codec.NegotiateSessionAsync(device, CancellationToken.None));
    }

    [Fact]
    public void BuildQuery_uses_dp_query_new_without_a_version_header()
    {
        // A device with a matching key can decrypt the query straight away (no handshake needed to build).
        var key = Encoding.ASCII.GetBytes(LocalKey);
        var codec = Codec("3.4");

        var frame = codec.BuildQuery();
        var (cmd, plaintext) = TuyaFrame.Parse55AA(frame, key);

        Assert.Equal(16u, cmd); // DP_QUERY_NEW
        Assert.Equal((byte)'{', plaintext[0]); // no version header prefix
        Assert.Contains("bfdevice123", Encoding.UTF8.GetString(plaintext));
    }

    [Theory]
    [InlineData("3.4")]
    [InlineData("3.5")]
    public void Heartbeat_carries_the_gwId_devId_identifying_body(string version)
    {
        // Regression: a bare "{}" heartbeat body (no gwId/devId) makes 3.5 firmware drop the socket on
        // every heartbeat, so the connection flaps ~every 10 s. HEART_BEAT must carry {gwId,devId}, like
        // DP_QUERY_NEW does (matching tinytuya).
        var key = Encoding.ASCII.GetBytes(LocalKey);
        var codec = Codec(version);

        var frame = codec.BuildHeartbeat();
        var (cmd, plaintext) = version == "3.5"
            ? TuyaFrame.Parse6699(frame, key)
            : TuyaFrame.Parse55AA(frame, key);
        var body = JObject.Parse(Encoding.UTF8.GetString(plaintext));

        Assert.Equal(9u, cmd); // HEART_BEAT
        Assert.Equal("bfdevice123", (string)body["gwId"]!);
        Assert.Equal("bfdevice123", (string)body["devId"]!);
    }

    /// <summary>
    /// A minimal in-memory Tuya device: it decodes the handshake frames the codec writes, replies with a
    /// valid SESS_KEY_NEG_RESP, verifies the FINISH HMAC, and derives its own session key.
    /// </summary>
    private sealed class FakeTuyaDevice : Stream
    {
        private readonly byte[] _localKey;
        private readonly bool _isGcm;
        private readonly bool _corruptResponseHmac;
        private readonly Queue<byte> _toClient = new();

        private byte[] _localNonce = [];
        private byte[] _remoteNonce = [];
        private byte[] _sessionKey = [];
        private uint _seq = 100;

        public bool FinishVerified { get; private set; }

        public FakeTuyaDevice(byte[] localKey, bool isGcm, bool corruptResponseHmac = false)
        {
            _localKey = localKey;
            _isGcm = isGcm;
            _corruptResponseHmac = corruptResponseHmac;
        }

        public string DecryptToJson(byte[] frame)
        {
            var (_, plaintext) = _isGcm
                ? TuyaFrame.Parse6699(frame, _sessionKey)
                : TuyaFrame.Parse55AA(frame, _sessionKey);
            var start = Array.IndexOf(plaintext, (byte)'{');
            return Encoding.UTF8.GetString(plaintext, start, plaintext.Length - start);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var frame = buffer.ToArray();
            var (cmd, payload) = _isGcm
                ? TuyaFrame.Parse6699(frame, _localKey)
                : TuyaFrame.Parse55AA(frame, _localKey);

            switch (cmd)
            {
                case 3: // SESS_KEY_NEG_START — reply with our nonce + HMAC(localKey, clientNonce)
                    _localNonce = payload[..16];
                    _remoteNonce = RandomNumberGenerator.GetBytes(16);
                    var hmacKey = _corruptResponseHmac ? TuyaCrypto.Xor(_localKey, Enumerable.Repeat((byte)0xFF, 16).ToArray()) : _localKey;
                    byte[] resp = [.. _remoteNonce, .. TuyaCrypto.HmacSha256(hmacKey, _localNonce)];
                    var respFrame = _isGcm
                        ? TuyaFrame.Build6699(_seq++, 4, resp, _localKey)
                        : TuyaFrame.Build55AA(_seq++, 4, resp, _localKey);
                    foreach (var b in respFrame)
                    {
                        _toClient.Enqueue(b);
                    }

                    _sessionKey = _isGcm
                        ? TuyaCrypto.DeriveSessionKey35(_localKey, _localNonce, _remoteNonce)
                        : TuyaCrypto.DeriveSessionKey34(_localKey, _localNonce, _remoteNonce);
                    break;

                case 5: // SESS_KEY_NEG_FINISH — verify HMAC(localKey, deviceNonce)
                    FinishVerified = CryptographicOperations.FixedTimeEquals(
                        payload[..32], TuyaCrypto.HmacSha256(_localKey, _remoteNonce));
                    break;
            }

            return ValueTask.CompletedTask;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var count = 0;
            while (count < buffer.Length && _toClient.Count > 0)
            {
                buffer.Span[count++] = _toClient.Dequeue();
            }

            return ValueTask.FromResult(count);
        }

        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
