using System.Security.Cryptography;
using System.Text;
using TuyaHub.Infrastructure.Tuya.Codec;
using Xunit;

namespace TuyaHub.Tests.Tuya;

/// <summary>
/// Discovery-side check: a protocol-3.5 (6699/GCM) beacon encrypted with the universal UDP key decodes
/// back to its JSON via the same <see cref="TuyaFrame.Parse6699"/> path the listener uses.
/// </summary>
public class TuyaBeacon6699Tests
{
    // Universal, non-secret discovery key; the AES key is its MD5 (matches TuyaLanDiscoveryListener).
    private static readonly byte[] UdpKey = MD5.HashData(Encoding.ASCII.GetBytes("yGAdlopoPVldABfn"));

    [Fact]
    public void Beacon6699_decodes_to_its_json_body()
    {
        const string beaconJson = "{\"gwId\":\"bf35device\",\"ip\":\"192.168.0.42\",\"version\":\"3.5\",\"productKey\":\"abcd\"}";
        var frame = TuyaFrame.Build6699(seq: 0, cmd: 8, plaintext: Encoding.UTF8.GetBytes(beaconJson), key: UdpKey);

        var (_, plaintext) = TuyaFrame.Parse6699(frame, UdpKey);
        var start = Array.IndexOf(plaintext, (byte)'{');
        var decoded = Encoding.UTF8.GetString(plaintext, start, plaintext.Length - start);

        Assert.Equal(beaconJson, decoded);
    }
}
