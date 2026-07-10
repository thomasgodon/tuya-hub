using TuyaHub.Domain;
using TuyaHub.Domain.ValueObjects;

namespace TuyaHub.Application.Abstractions;

/// <summary>
/// Port for sending datapoint writes to a device. Implemented by the Tuya ACL, which translates a
/// domain <see cref="DeviceCommand"/> into a Tuya <c>dps</c> write over the local protocol.
/// </summary>
public interface IDeviceGateway
{
    /// <summary>Sends the intended datapoint writes to the named device. A no-op for an empty command.</summary>
    Task SendAsync(DeviceName device, DeviceCommand command, CancellationToken cancellationToken);
}
