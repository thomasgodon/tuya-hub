namespace TuyaHub.Infrastructure.Knx;

/// <summary>
/// The device capabilities the KNX ACL mirrors to status group addresses (the Tuya → KNX feedback
/// path). Used to key the per-device status store. Light CCT is intentionally absent — it is deferred
/// to M6 along with the rest of the colour-temperature handling.
/// </summary>
internal enum Capability
{
    FanPower,
    FanSpeed,
    FanDirection,
    FanTimer,
    LightPower,
    LightBrightness,
    Availability,
}
