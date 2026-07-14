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
    bool LightPower,
    int LightBrightnessDp,
    int LightBrightnessPercent,
    int LightCctDp,
    int LightCctPercent);
