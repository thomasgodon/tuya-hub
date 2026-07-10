namespace TuyaHub.Infrastructure.Knx;

/// <summary>
/// The device capabilities the KNX ACL mirrors to status group addresses (the Tuya → KNX feedback
/// path). Used to key the per-device status store.
/// </summary>
internal enum Capability
{
    FanPower,
    FanSpeed,
    FanDirection,
    FanTimer,
    LightPower,
    LightBrightness,
    LightCct,
    Availability,
}
