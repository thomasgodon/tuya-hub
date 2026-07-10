using Microsoft.Extensions.Logging;
using TuyaHub.Application.Abstractions;
using TuyaHub.Domain;
using TuyaHub.Domain.ValueObjects;

namespace TuyaHub.Infrastructure.Tuya;

/// <summary>
/// Tuya ACL implementation of <see cref="IDeviceGateway"/>: routes a domain command to the named
/// device's persistent connection.
/// </summary>
internal sealed class TuyaDeviceGateway(TuyaConnectionManager manager, ILogger<TuyaDeviceGateway> logger)
    : IDeviceGateway
{
    public Task SendAsync(DeviceName device, DeviceCommand command, CancellationToken cancellationToken)
    {
        var connection = manager.Get(device);
        if (connection is null)
        {
            logger.LogWarning("No Tuya connection for device {Device}; command dropped.", device);
            return Task.CompletedTask;
        }

        return connection.SendCommandAsync(command, cancellationToken);
    }
}
