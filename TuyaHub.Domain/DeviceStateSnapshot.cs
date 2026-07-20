using TuyaHub.Domain.ValueObjects;

namespace TuyaHub.Domain;

/// <summary>
/// An immutable, lock-safe read model of a <see cref="Device"/> aggregate's current state, produced
/// by <see cref="Device.CaptureState"/> under the aggregate's lock. Lets a concurrent reader (the web
/// dashboard) observe consistent state without touching the entity's unsynchronized getters. Carries
/// no connection secrets.
/// </summary>
public sealed record DeviceStateSnapshot(
    DeviceName Name,
    bool IsOnline,
    bool FanPower,
    int FanSpeedStatus,
    FanDirection FanDirection,
    int FanTimerMinutes,
    bool FanTimerRunning,
    bool FanBeep,
    bool LightPower,
    int LightCctDp,
    int LightCctPercent)
{
    /// <summary>
    /// Generic per-capability values for device types rendered by the capability-driven dashboard.
    /// Wind Calm uses the typed fields above instead and leaves this empty.
    /// </summary>
    public IReadOnlyDictionary<CapabilityKey, CapabilityValue> Capabilities { get; init; }
        = new Dictionary<CapabilityKey, CapabilityValue>();
}
