using MediatR;
using TuyaHub.Domain.ValueObjects;

namespace TuyaHub.Domain.Events;

/// <summary>
/// Domain events raised by a device aggregate when its observed state changes. Each carries the
/// <see cref="DeviceName"/> so a subscriber (the KNX ACL) can route it to the correct device. State
/// changes are raised only from authoritative device readback (<see cref="IDevice.ApplyReportedState"/>),
/// never from unconfirmed commands.
/// </summary>
public interface IDeviceEvent : INotification
{
    DeviceName Device { get; }
}

/// <summary>
/// One capability's observed value changed. The generic feedback event: the ACL looks up the device's
/// profile binding for <see cref="Capability"/> and encodes <see cref="Value"/> onto its status group
/// address. Replaces the former per-capability events (FanPowerChanged, LightBrightnessChanged, …) so a
/// new device type raises the same event for its own capabilities without adding event types or handlers.
/// </summary>
public sealed record DeviceCapabilityChanged(DeviceName Device, CapabilityKey Capability, CapabilityValue Value)
    : IDeviceEvent;

/// <summary>The device could not be reached and has been marked offline.</summary>
public sealed record DeviceWentOffline(DeviceName Device) : IDeviceEvent;

/// <summary>A previously offline device has reconnected.</summary>
public sealed record DeviceReconnected(DeviceName Device) : IDeviceEvent;
