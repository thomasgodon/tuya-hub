using System.Security.Cryptography;

namespace TuyaHub.Infrastructure.Tuya.Codec;

/// <summary>
/// Low-level cryptographic primitives for the hand-rolled 3.4/3.5 Tuya codec. Everything is standard
/// .NET crypto — the protocol just wires it together in a particular way:
/// <list type="bullet">
/// <item>3.4 payloads: AES-128-ECB (PKCS7) keyed by the negotiated session key;</item>
/// <item>3.5 payloads: AES-128-GCM (12-byte IV prepended, 16-byte tag appended, header as AAD);</item>
/// <item>the session-key negotiation HMACs and the two per-version key-derivation forms.</item>
/// </list>
/// See <c>scratchpad/tuya-34-35-spec.md</c> / tinytuya <c>PROTOCOL.md</c> for the wire spec this follows.
/// </summary>
internal static class TuyaCrypto
{
    private const int GcmIvSize = 12;
    private const int GcmTagSize = 16;

    public static byte[] EcbEncrypt(byte[] key, byte[] data, bool pad)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.Mode = CipherMode.ECB;
        aes.Padding = pad ? PaddingMode.PKCS7 : PaddingMode.None;
        using var encryptor = aes.CreateEncryptor();
        return encryptor.TransformFinalBlock(data, 0, data.Length);
    }

    public static byte[] EcbDecrypt(byte[] key, byte[] data, bool unpad)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.Mode = CipherMode.ECB;
        aes.Padding = unpad ? PaddingMode.PKCS7 : PaddingMode.None;
        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(data, 0, data.Length);
    }

    /// <summary>AES-128-GCM. Returns <c>IV(12) ‖ ciphertext ‖ tag(16)</c>, the layout the 6699 frame carries.</summary>
    public static byte[] GcmEncrypt(byte[] key, byte[] iv, byte[] plaintext, byte[]? associatedData)
    {
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[GcmTagSize];
        using var gcm = new AesGcm(key, GcmTagSize);
        gcm.Encrypt(iv, plaintext, ciphertext, tag, associatedData);

        var output = new byte[GcmIvSize + ciphertext.Length + GcmTagSize];
        Buffer.BlockCopy(iv, 0, output, 0, GcmIvSize);
        Buffer.BlockCopy(ciphertext, 0, output, GcmIvSize, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, output, GcmIvSize + ciphertext.Length, GcmTagSize);
        return output;
    }

    public static byte[] GcmDecrypt(byte[] key, byte[] iv, byte[] ciphertext, byte[] tag, byte[]? associatedData)
    {
        var plaintext = new byte[ciphertext.Length];
        using var gcm = new AesGcm(key, GcmTagSize);
        gcm.Decrypt(iv, ciphertext, tag, plaintext, associatedData);
        return plaintext;
    }

    public static byte[] HmacSha256(byte[] key, byte[] data) => HMACSHA256.HashData(key, data);

    public static byte[] Xor(byte[] a, byte[] b)
    {
        var result = new byte[a.Length];
        for (var i = 0; i < a.Length; i++)
        {
            result[i] = (byte)(a[i] ^ b[i]);
        }

        return result;
    }

    /// <summary>3.4 session key: AES-ECB-encrypt (no padding) of the nonce XOR under the real local key.</summary>
    public static byte[] DeriveSessionKey34(byte[] localKey, byte[] localNonce, byte[] remoteNonce)
        => EcbEncrypt(localKey, Xor(localNonce, remoteNonce), pad: false);

    /// <summary>
    /// 3.5 session key: GCM-encrypt the nonce XOR (IV = first 12 bytes of the local nonce) and take the
    /// 16 ciphertext bytes (the <c>[12:28]</c> slice of <c>IV‖ct‖tag</c>); the tag is discarded.
    /// </summary>
    public static byte[] DeriveSessionKey35(byte[] localKey, byte[] localNonce, byte[] remoteNonce)
    {
        var iv = localNonce[..GcmIvSize];
        var blob = GcmEncrypt(localKey, iv, Xor(localNonce, remoteNonce), associatedData: null);
        return blob[GcmIvSize..(GcmIvSize + 16)];
    }
}
