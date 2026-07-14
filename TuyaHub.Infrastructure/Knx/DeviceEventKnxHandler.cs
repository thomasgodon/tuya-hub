using MediatR;
using TuyaHub.Domain.Events;
using TuyaHub.Domain.ValueObjects;
using TuyaHub.Infrastructure.Profiles;

namespace TuyaHub.Infrastructure.Knx;

/// <summary>
/// Translates domain state-change events into KNX status writes (the Tuya → KNX feedback path). A
/// single generic handler: it looks up the device's profile binding for the changed capability, encodes
/// the scalar onto the wire (the binding owns the DPT choice), and hands it to the <see cref="KnxBridge"/>,
/// which owns dedup, caching and the bus write. Connectivity transitions drive the availability object.
/// </summary>
internal sealed class DeviceEventKnxHandler(KnxBridge bridge, ConfiguredDeviceProfiles profiles) :
    INotificationHandler<DeviceCapabilityChanged>,
    INotificationHandler<DeviceWentOffline>,
    INotificationHandler<DeviceReconnected>
{
    public Task Handle(DeviceCapabilityChanged n, CancellationToken ct)
    {
        var binding = profiles.For(n.Device).Capabilities.FirstOrDefault(c => c.Key == n.Capability);
        if (binding?.EncodeStatus is null)
        {
            return Task.CompletedTask;
        }

        return bridge.PublishAsync(n.Device, n.Capability, binding.EncodeStatus(n.Value), ct);
    }

    public Task Handle(DeviceWentOffline n, CancellationToken ct)
        => PublishAvailability(n.Device, online: false, ct);

    public Task Handle(DeviceReconnected n, CancellationToken ct)
        => PublishAvailability(n.Device, online: true, ct);

    private Task PublishAvailability(DeviceName device, bool online, CancellationToken ct)
        => bridge.PublishAsync(device, WellKnownCapabilities.Availability, KnxDpt.Bool(online), ct);
}
