using Microsoft.Extensions.Logging;
using TuyaHub.Domain.ValueObjects;
using TuyaHub.Infrastructure.Options;

namespace TuyaHub.Infrastructure.Tuya.Codec;

/// <summary>
/// Selects the wire codec for a device from its configured protocol version: TuyaNet for 3.1/3.3,
/// the hand-rolled session codec for 3.4/3.5.
/// </summary>
internal static class TuyaCodecFactory
{
    public static ITuyaCodec Create(
        DeviceName name,
        TuyaDeviceOptions options,
        TimeSpan handshakeTimeout,
        ILogger logger)
    {
        var version = ProtocolVersion.Create(options.ProtocolVersion);
        return version.RequiresSessionHandshake
            ? new TuyaSessionCodec(name, options, version, handshakeTimeout, logger)
            : new TuyaNetCodec(options, version);
    }
}
