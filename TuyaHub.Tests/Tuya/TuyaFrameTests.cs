using System.Buffers.Binary;
using System.Text;
using TuyaHub.Infrastructure.Tuya.Codec;
using Xunit;

namespace TuyaHub.Tests.Tuya;

/// <summary>
/// Byte-level checks for the hand-rolled 3.4 (55AA/HMAC) and 3.5 (6699/GCM) frame codecs: encode→decode
/// round-trips, the plaintext return-code strip on responses, and rolling-buffer frame extraction.
/// </summary>
public class TuyaFrameTests
{
    private static readonly byte[] Key = Encoding.ASCII.GetBytes("0123456789abcdef");

    private static byte[] Payload(string text) => Encoding.UTF8.GetBytes(text);

    [Fact]
    public void Frame55AA_round_trips_cmd_and_payload()
    {
        var payload = Payload("{\"dps\":{\"1\":true}}");

        var frame = TuyaFrame.Build55AA(seq: 7, cmd: 13, plaintext: payload, key: Key);
        var (cmd, decoded) = TuyaFrame.Parse55AA(frame, Key);

        Assert.Equal(13u, cmd);
        Assert.Equal(payload, decoded);
    }

    [Fact]
    public void Frame6699_round_trips_cmd_and_payload()
    {
        var payload = Payload("{\"dps\":{\"1\":true}}");

        var frame = TuyaFrame.Build6699(seq: 9, cmd: 13, plaintext: payload, key: Key);
        var (cmd, decoded) = TuyaFrame.Parse6699(frame, Key);

        Assert.Equal(13u, cmd);
        Assert.Equal(payload, decoded);
    }

    [Fact]
    public void Parse55AA_throws_on_tampered_hmac()
    {
        var frame = TuyaFrame.Build55AA(1, 10, Payload("{}"), Key);
        frame[^5] ^= 0xFF; // flip a byte inside the HMAC

        Assert.Throws<InvalidDataException>(() => TuyaFrame.Parse55AA(frame, Key));
    }

    [Fact]
    public void Parse55AA_strips_a_leading_return_code()
    {
        var plaintext = Payload("{\"dps\":{\"1\":1}}");
        var frameWithRetcode = Build55AAWithRetcode(seq: 3, cmd: 10, plaintext, Key);

        var (cmd, decoded) = TuyaFrame.Parse55AA(frameWithRetcode, Key);

        Assert.Equal(10u, cmd);
        Assert.Equal(plaintext, decoded);
    }

    [Fact]
    public void Parse6699_strips_a_leading_return_code()
    {
        var plaintext = new byte[] { 0, 0, 0, 0 }.Concat(Payload("{\"dps\":{}}")).ToArray();
        var frame = TuyaFrame.Build6699(1, 8, plaintext, Key);

        var (_, decoded) = TuyaFrame.Parse6699(frame, Key);

        Assert.Equal(Payload("{\"dps\":{}}"), decoded);
    }

    [Fact]
    public void TryExtract55AA_returns_frames_in_order_then_null()
    {
        var a = TuyaFrame.Build55AA(1, 10, Payload("{\"a\":1}"), Key);
        var b = TuyaFrame.Build55AA(2, 10, Payload("{\"b\":2}"), Key);
        var buffer = new List<byte>();
        buffer.AddRange(a);
        buffer.AddRange(b);

        var first = TuyaFrame.TryExtract55AA(buffer);
        var second = TuyaFrame.TryExtract55AA(buffer);
        var third = TuyaFrame.TryExtract55AA(buffer);

        Assert.Equal(a, first);
        Assert.Equal(b, second);
        Assert.Null(third);
        Assert.Empty(buffer);
    }

    [Fact]
    public void TryExtract55AA_returns_null_until_the_whole_frame_has_arrived()
    {
        var frame = TuyaFrame.Build55AA(1, 10, Payload("{\"a\":1}"), Key);
        var buffer = new List<byte>(frame[..10]);

        Assert.Null(TuyaFrame.TryExtract55AA(buffer));

        buffer.AddRange(frame[10..]);
        Assert.Equal(frame, TuyaFrame.TryExtract55AA(buffer));
    }

    [Fact]
    public void TryExtract6699_resynchronises_past_leading_junk()
    {
        var frame = TuyaFrame.Build6699(1, 8, Payload("{\"a\":1}"), Key);
        var buffer = new List<byte> { 0x11, 0x22, 0x33 };
        buffer.AddRange(frame);

        Assert.Equal(frame, TuyaFrame.TryExtract6699(buffer));
    }

    /// <summary>Builds a 55AA response frame with a 4-byte zero return code, as a device sends.</summary>
    private static byte[] Build55AAWithRetcode(uint seq, uint cmd, byte[] plaintext, byte[] key)
    {
        var ciphertext = TuyaCrypto.EcbEncrypt(key, plaintext, pad: true);
        var region = new byte[4 + ciphertext.Length]; // retcode(4) + ciphertext
        Buffer.BlockCopy(ciphertext, 0, region, 4, ciphertext.Length);

        var length = (uint)(region.Length + 32 + 4);
        var frame = new byte[16 + region.Length + 32 + 4];
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(0), 0x000055AA);
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(4), seq);
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(8), cmd);
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(12), length);
        Buffer.BlockCopy(region, 0, frame, 16, region.Length);

        var bodyEnd = 16 + region.Length;
        Buffer.BlockCopy(TuyaCrypto.HmacSha256(key, frame[..bodyEnd]), 0, frame, bodyEnd, 32);
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(bodyEnd + 32), 0x0000AA55);
        return frame;
    }
}
