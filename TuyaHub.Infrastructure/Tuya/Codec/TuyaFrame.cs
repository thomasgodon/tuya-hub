using System.Buffers.Binary;
using System.Security.Cryptography;

namespace TuyaHub.Infrastructure.Tuya.Codec;

/// <summary>
/// Frame (de)serialization for the hand-rolled 3.4/3.5 codec. Two wire frames:
/// <list type="bullet">
/// <item><b>55AA</b> (3.4): <c>00 00 55 AA | seq | cmd | length | [retcode] | AES-ECB(payload) | HMAC-SHA256 | 00 00 AA 55</c>.
///   <c>length</c> counts everything after it (payload + HMAC + suffix). Device→client frames carry a
///   4-byte plaintext return code before the ciphertext.</item>
/// <item><b>6699</b> (3.5): <c>00 00 66 99 | reserved | seq | cmd | payloadLen | IV(12) | GCM(ct) | tag(16) | 00 00 99 66</c>.
///   <c>payloadLen</c> counts <c>IV+ct+tag</c> only (excludes the suffix); the 14 header bytes after the
///   prefix are the GCM associated data. Any return code sits inside the decrypted plaintext.</item>
/// </list>
/// All integrity/crypto uses the caller-supplied key (the local key during the handshake, then the
/// negotiated session key). See <c>scratchpad/tuya-34-35-spec.md</c> for the byte-exact spec.
/// </summary>
internal static class TuyaFrame
{
    private const uint Prefix55AA = 0x000055AA;
    private const uint Suffix55AA = 0x0000AA55;
    private const uint Prefix6699 = 0x00006699;
    private const uint Suffix6699 = 0x00009966;

    private const int HmacSize = 32;
    private const int SuffixSize = 4;
    private const int GcmIvSize = 12;
    private const int GcmTagSize = 16;
    private const int MaxFrameSize = 1 << 20;

    // ---- 55AA (3.4): AES-ECB payload + HMAC-SHA256 integrity ----

    public static byte[] Build55AA(uint seq, uint cmd, byte[] plaintext, byte[] key)
    {
        var ciphertext = TuyaCrypto.EcbEncrypt(key, plaintext, pad: true);
        var length = (uint)(ciphertext.Length + HmacSize + SuffixSize);

        var frame = new byte[16 + ciphertext.Length + HmacSize + SuffixSize];
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(0), Prefix55AA);
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(4), seq);
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(8), cmd);
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(12), length);
        Buffer.BlockCopy(ciphertext, 0, frame, 16, ciphertext.Length);

        var bodyEnd = 16 + ciphertext.Length;
        var mac = TuyaCrypto.HmacSha256(key, frame[..bodyEnd]);
        Buffer.BlockCopy(mac, 0, frame, bodyEnd, HmacSize);
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(bodyEnd + HmacSize), Suffix55AA);
        return frame;
    }

    public static (uint Cmd, byte[] Plaintext) Parse55AA(byte[] frame, byte[] key)
    {
        var cmd = BinaryPrimitives.ReadUInt32BigEndian(frame.AsSpan(8));
        var length = (int)BinaryPrimitives.ReadUInt32BigEndian(frame.AsSpan(12));
        var total = 16 + length;
        if (frame.Length < total)
        {
            throw new InvalidDataException("Truncated 55AA frame.");
        }

        var bodyEnd = total - (HmacSize + SuffixSize); // first byte of the HMAC
        var mac = frame[bodyEnd..(bodyEnd + HmacSize)];
        var expected = TuyaCrypto.HmacSha256(key, frame[..bodyEnd]);
        if (!CryptographicOperations.FixedTimeEquals(mac, expected))
        {
            throw new InvalidDataException("55AA HMAC verification failed.");
        }

        var payloadStart = 16;
        // Device responses prepend a 4-byte plaintext return code (near-always 00 00 00 xx).
        if (bodyEnd - payloadStart >= SuffixSize && frame[16] == 0 && frame[17] == 0 && frame[18] == 0)
        {
            payloadStart += 4;
        }

        if (bodyEnd - payloadStart <= 0)
        {
            return (cmd, []);
        }

        var plaintext = TuyaCrypto.EcbDecrypt(key, frame[payloadStart..bodyEnd], unpad: true);
        return (cmd, plaintext);
    }

    // ---- 6699 (3.5): AES-GCM ----

    public static byte[] Build6699(uint seq, uint cmd, byte[] plaintext, byte[] key)
    {
        var iv = RandomNumberGenerator.GetBytes(GcmIvSize);
        var payloadLen = (uint)(GcmIvSize + plaintext.Length + GcmTagSize);

        var header = new byte[18];
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(0), Prefix6699);
        // header[4..6] reserved = 0x0000
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(6), seq);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(10), cmd);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(14), payloadLen);

        var aad = header[4..18];
        var blob = TuyaCrypto.GcmEncrypt(key, iv, plaintext, aad); // IV ‖ ct ‖ tag

        var frame = new byte[18 + blob.Length + SuffixSize];
        Buffer.BlockCopy(header, 0, frame, 0, 18);
        Buffer.BlockCopy(blob, 0, frame, 18, blob.Length);
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(18 + blob.Length), Suffix6699);
        return frame;
    }

    public static (uint Cmd, byte[] Plaintext) Parse6699(byte[] frame, byte[] key)
    {
        var cmd = BinaryPrimitives.ReadUInt32BigEndian(frame.AsSpan(10));
        var payloadLen = (int)BinaryPrimitives.ReadUInt32BigEndian(frame.AsSpan(14));
        var total = 18 + payloadLen + SuffixSize;
        if (frame.Length < total || payloadLen < GcmIvSize + GcmTagSize)
        {
            throw new InvalidDataException("Truncated 6699 frame.");
        }

        var aad = frame[4..18];
        var region = frame[18..(18 + payloadLen)];
        var iv = region[..GcmIvSize];
        var tag = region[(payloadLen - GcmTagSize)..payloadLen];
        var ciphertext = region[GcmIvSize..(payloadLen - GcmTagSize)];

        var plaintext = TuyaCrypto.GcmDecrypt(key, iv, ciphertext, tag, aad);

        // A leading 4-byte return code (00 00 00 xx) may precede the payload; strip it.
        if (plaintext.Length >= SuffixSize && plaintext[0] == 0 && plaintext[1] == 0 && plaintext[2] == 0)
        {
            plaintext = plaintext[4..];
        }

        return (cmd, plaintext);
    }

    // ---- Buffer framing: pull one complete frame out of a rolling receive buffer ----

    public static byte[]? TryExtract55AA(List<byte> buffer)
        => TryExtract(buffer, 0x55, 0xAA, headerLen: 16, lengthOffset: 12, trailer: 0);

    public static byte[]? TryExtract6699(List<byte> buffer)
        => TryExtract(buffer, 0x66, 0x99, headerLen: 18, lengthOffset: 14, trailer: SuffixSize);

    private static byte[]? TryExtract(List<byte> buffer, byte b2, byte b3, int headerLen, int lengthOffset, int trailer)
    {
        while (true)
        {
            AlignToPrefix(buffer, b2, b3);
            if (buffer.Count < headerLen)
            {
                return null;
            }

            var length = (buffer[lengthOffset] << 24) | (buffer[lengthOffset + 1] << 16)
                | (buffer[lengthOffset + 2] << 8) | buffer[lengthOffset + 3];
            var total = headerLen + length + trailer;
            if (length <= 0 || total > MaxFrameSize)
            {
                buffer.RemoveRange(0, 4); // corrupt length; drop this prefix and resynchronise
                continue;
            }

            if (buffer.Count < total)
            {
                return null;
            }

            var frame = buffer.GetRange(0, total).ToArray();
            buffer.RemoveRange(0, total);
            return frame;
        }
    }

    private static void AlignToPrefix(List<byte> buffer, byte b2, byte b3)
    {
        if (StartsWithPrefix(buffer, 0, b2, b3))
        {
            return;
        }

        for (var i = 1; i <= buffer.Count - 4; i++)
        {
            if (StartsWithPrefix(buffer, i, b2, b3))
            {
                buffer.RemoveRange(0, i);
                return;
            }
        }

        // No prefix found; keep only the last 3 bytes (a prefix may be split across reads).
        if (buffer.Count > 3)
        {
            buffer.RemoveRange(0, buffer.Count - 3);
        }
    }

    private static bool StartsWithPrefix(List<byte> buffer, int offset, byte b2, byte b3)
        => offset + 4 <= buffer.Count
            && buffer[offset] == 0 && buffer[offset + 1] == 0
            && buffer[offset + 2] == b2 && buffer[offset + 3] == b3;
}
