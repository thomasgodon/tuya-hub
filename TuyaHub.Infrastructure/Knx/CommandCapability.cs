namespace TuyaHub.Infrastructure.Knx;

/// <summary>
/// The device capabilities the KNX ACL accepts commands for (the KNX → Tuya command path). Used to
/// key the per-device command-GA lookup and to select the decoder + command record in
/// <see cref="KnxCommandTranslator"/>. Light CCT is intentionally absent — the colour-temperature
/// command path is deferred to M6, mirroring how <see cref="Capability"/> omits it on the feedback side.
/// </summary>
internal enum CommandCapability
{
    FanPower,
    FanSpeedStep,
    FanDirection,
    FanTimer,
    LightPower,
    LightBrightness,
}
