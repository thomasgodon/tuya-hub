using MediatR;
using TuyaHub.Domain.ValueObjects;

namespace TuyaHub.Domain.Events;

/// <summary>
/// Domain events raised by the <see cref="Device"/> aggregate when its observed state changes.
/// Each carries the <see cref="DeviceName"/> so a subscriber (the KNX ACL) can route it to the
/// correct device's status group address. State-change events are raised only from authoritative
/// device readback (<see cref="Device.ApplyReportedState"/>), never from unconfirmed commands.
/// </summary>
public interface IDeviceEvent : INotification
{
    DeviceName Device { get; }
}

/// <summary>Fan power (DP 60) changed. Drives the fan-power status GA (DPT 1.001).</summary>
public sealed record FanPowerChanged(DeviceName Device, bool IsOn) : IDeviceEvent;

/// <summary>
/// Fan speed status changed. Drives the fan-speed status GA (DPT 5.010): <see cref="StatusValue"/>
/// is 0 when the fan is off, otherwise the level 1..6. Raised when either power or speed changes.
/// </summary>
public sealed record FanSpeedChanged(DeviceName Device, int StatusValue) : IDeviceEvent;

/// <summary>Fan direction (DP 63) changed. Drives the direction status GA (DPT 1.001).</summary>
public sealed record FanDirectionChanged(DeviceName Device, FanDirection Direction) : IDeviceEvent;

/// <summary>Fan countdown (DP 64) changed. Drives the timer status GA (DPT 7.006).</summary>
public sealed record FanTimerChanged(DeviceName Device, int Minutes) : IDeviceEvent;

/// <summary>Light power (DP 20) changed. Drives the light-power status GA (DPT 1.001).</summary>
public sealed record LightPowerChanged(DeviceName Device, bool IsOn) : IDeviceEvent;

/// <summary>Light brightness (DP 22) changed. Drives the brightness status GA (DPT 5.001).</summary>
public sealed record LightBrightnessChanged(DeviceName Device, Brightness Brightness) : IDeviceEvent;

/// <summary>Light colour temperature (DP 23) changed. Drives the CCT status GA (DPT 5.001).</summary>
public sealed record LightCctChanged(DeviceName Device, ColourTemperature ColourTemperature) : IDeviceEvent;

/// <summary>The device could not be reached and has been marked offline.</summary>
public sealed record DeviceWentOffline(DeviceName Device) : IDeviceEvent;

/// <summary>A previously offline device has reconnected.</summary>
public sealed record DeviceReconnected(DeviceName Device) : IDeviceEvent;
