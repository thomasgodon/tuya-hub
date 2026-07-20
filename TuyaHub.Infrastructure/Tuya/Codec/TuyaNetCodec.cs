using com.clusterrr.TuyaNet;
using Newtonsoft.Json;
using TuyaHub.Domain.ValueObjects;
using TuyaHub.Infrastructure.Options;

namespace TuyaHub.Infrastructure.Tuya.Codec;

/// <summary>
/// The 3.1/3.3 codec, delegating framing/AES-ECB/CRC to the pinned TuyaNet library (the PRD forbids
/// hand-rolling 3.3). This preserves the exact behaviour tuya-hub shipped with; it adds no handshake
/// (3.1/3.3 have none). Frame boundary detection reuses the shared <see cref="TuyaFrame"/> 55AA reader.
/// </summary>
internal sealed class TuyaNetCodec : ITuyaCodec
{
    private readonly TuyaDevice _device;

    public ProtocolVersion Version { get; }

    public TuyaNetCodec(TuyaDeviceOptions options, ProtocolVersion version)
    {
        Version = version;
        _device = new TuyaDevice(
            options.IpAddress,
            options.LocalKey,
            options.DeviceId,
            version.Value == ProtocolVersion.V31 ? TuyaProtocolVersion.V31 : TuyaProtocolVersion.V33,
            options.Port,
            receiveTimeout: 250);
    }

    public Task NegotiateSessionAsync(Stream stream, CancellationToken cancellationToken) => Task.CompletedTask;

    public byte[] BuildControl(IReadOnlyDictionary<string, object> dps)
    {
        var json = JsonConvert.SerializeObject(new Dictionary<string, object> { ["dps"] = dps });
        json = _device.FillJson(json);
        return _device.EncodeRequest(TuyaCommand.CONTROL, json);
    }

    public byte[] BuildQuery() => _device.EncodeRequest(TuyaCommand.DP_QUERY, _device.FillJson(null));

    public byte[] BuildHeartbeat() => _device.EncodeRequest(TuyaCommand.HEART_BEAT, "{}");

    public bool TryReadMessage(List<byte> buffer, out string? json)
    {
        json = null;
        var frame = TuyaFrame.TryExtract55AA(buffer);
        if (frame is null)
        {
            return false;
        }

        try
        {
            var response = _device.DecodeResponse(frame);
            json = string.IsNullOrEmpty(response.JSON) ? null : response.JSON;
        }
        catch
        {
            json = null; // undecodable / ack — skip, matching the previous behaviour
        }

        return true;
    }
}
