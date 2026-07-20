using System.Text;
using TuyaHub.Infrastructure.Tuya.Codec;
using Xunit;

namespace TuyaHub.Tests.Tuya;

public class TuyaCryptoTests
{
    private static readonly byte[] Key = Encoding.ASCII.GetBytes("0123456789abcdef");

    [Fact]
    public void Ecb_round_trips_with_padding()
    {
        var data = Encoding.UTF8.GetBytes("hello tuya");

        var cipher = TuyaCrypto.EcbEncrypt(Key, data, pad: true);
        var plain = TuyaCrypto.EcbDecrypt(Key, cipher, unpad: true);

        Assert.Equal(data, plain);
    }

    [Fact]
    public void Gcm_round_trips_with_associated_data()
    {
        var iv = new byte[12];
        var aad = new byte[] { 1, 2, 3, 4 };
        var data = Encoding.UTF8.GetBytes("payload");

        var blob = TuyaCrypto.GcmEncrypt(Key, iv, data, aad); // iv + ct + tag
        var ct = blob[12..^16];
        var tag = blob[^16..];
        var plain = TuyaCrypto.GcmDecrypt(Key, iv, ct, tag, aad);

        Assert.Equal(data, plain);
    }

    [Fact]
    public void Gcm_decrypt_fails_when_associated_data_differs()
    {
        var iv = new byte[12];
        var blob = TuyaCrypto.GcmEncrypt(Key, iv, [9, 9, 9], [1, 2, 3, 4]);

        Assert.ThrowsAny<Exception>(
            () => TuyaCrypto.GcmDecrypt(Key, iv, blob[12..^16], blob[^16..], [4, 3, 2, 1]));
    }

    [Fact]
    public void SessionKey34_is_ecb_encrypt_of_the_nonce_xor()
    {
        var localNonce = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray();
        var remoteNonce = Enumerable.Range(0, 16).Select(i => (byte)(0xFF - i)).ToArray();

        var sessionKey = TuyaCrypto.DeriveSessionKey34(Key, localNonce, remoteNonce);

        Assert.Equal(16, sessionKey.Length);
        // Decrypting the session key (no padding) must recover the nonce XOR.
        var recoveredXor = TuyaCrypto.EcbDecrypt(Key, sessionKey, unpad: false);
        Assert.Equal(TuyaCrypto.Xor(localNonce, remoteNonce), recoveredXor);
    }

    [Fact]
    public void SessionKey35_yields_sixteen_bytes_and_differs_from_34()
    {
        var localNonce = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray();
        var remoteNonce = Enumerable.Range(0, 16).Select(i => (byte)(0xFF - i)).ToArray();

        var key35 = TuyaCrypto.DeriveSessionKey35(Key, localNonce, remoteNonce);
        var key34 = TuyaCrypto.DeriveSessionKey34(Key, localNonce, remoteNonce);

        Assert.Equal(16, key35.Length);
        Assert.NotEqual(key34, key35);
    }
}
