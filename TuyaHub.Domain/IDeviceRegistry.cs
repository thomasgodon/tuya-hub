using TuyaHub.Domain.ValueObjects;

namespace TuyaHub.Domain;

/// <summary>
/// Port for retrieving the configured <see cref="Device"/> aggregates. Implemented by the
/// infrastructure layer from static configuration (no LAN discovery in the MVP).
/// </summary>
public interface IDeviceRegistry
{
    /// <summary>All configured, enabled devices.</summary>
    IReadOnlyCollection<Device> Devices { get; }

    /// <summary>Returns the device with the given name, or <c>null</c> if it is not configured.</summary>
    Device? Find(DeviceName name);
}
