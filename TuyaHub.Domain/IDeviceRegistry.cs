using TuyaHub.Domain.ValueObjects;

namespace TuyaHub.Domain;

/// <summary>
/// Port for retrieving the configured device aggregates. Implemented by the infrastructure layer from
/// static configuration (no LAN discovery in the MVP). Returns the device-type-agnostic
/// <see cref="IDevice"/> contract; a handler that needs a concrete aggregate casts to it.
/// </summary>
public interface IDeviceRegistry
{
    /// <summary>All configured, enabled devices.</summary>
    IReadOnlyCollection<IDevice> Devices { get; }

    /// <summary>Returns the device with the given name, or <c>null</c> if it is not configured.</summary>
    IDevice? Find(DeviceName name);
}
