using TuyaHub.Domain.ValueObjects;

namespace TuyaHub.Domain;

/// <summary>
/// The <see cref="CapabilityKey"/>s for the Wind Calm fan+light profile — the single source of truth
/// shared by the typed <see cref="DeviceCommand"/> / <see cref="DeviceReport"/> facades (which live in
/// this layer) and the Wind Calm profile in the infrastructure layer. The string values match the
/// historical capability names, so the KNX <c>Capability</c>/<c>CommandCapability</c> enum names map to
/// these keys 1:1.
/// </summary>
public static class WindCalmCapabilities
{
    public static readonly CapabilityKey FanPower = new("FanPower");
    public static readonly CapabilityKey FanSpeed = new("FanSpeed");
    public static readonly CapabilityKey FanDirection = new("FanDirection");
    public static readonly CapabilityKey FanTimer = new("FanTimer");
    public static readonly CapabilityKey LightPower = new("LightPower");
    public static readonly CapabilityKey LightCct = new("LightCct");
    public static readonly CapabilityKey LightCctStep = new("LightCctStep");
}
