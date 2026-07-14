using MediatR;
using TuyaHub.Domain.ValueObjects;

namespace TuyaHub.Domain;

/// <summary>
/// The device-type-agnostic contract the application pipeline depends on: identity, connectivity, a
/// read-only state snapshot, and the feedback-path mutators. Each supported device type is a concrete
/// aggregate implementing this (the Wind Calm fan+light is <see cref="Device"/>); the registry, gateway,
/// ingestion service, and dashboard projection speak only to this interface, so a new device type is
/// added without editing them. Type-specific command methods (e.g. <see cref="Device.SetFanPower"/>)
/// stay on the concrete aggregate — its own command handlers cast to it.
/// </summary>
public interface IDevice
{
    DeviceName Name { get; }
    bool IsOnline { get; }

    /// <summary>Takes a consistent, lock-safe snapshot of current state for read-only consumers.</summary>
    DeviceStateSnapshot CaptureState();

    /// <summary>Applies an observed device snapshot (authoritative) and returns the change events to publish.</summary>
    IReadOnlyList<INotification> ApplyReportedState(DeviceReport report);

    /// <summary>Marks the device unreachable; emits the transition event only on a real change.</summary>
    IReadOnlyList<INotification> MarkOffline();

    /// <summary>Marks the device reachable; emits the transition event only on a real change.</summary>
    IReadOnlyList<INotification> MarkReconnected();
}
