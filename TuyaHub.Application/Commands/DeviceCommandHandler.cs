using MediatR;
using Microsoft.Extensions.Logging;
using TuyaHub.Application.Abstractions;
using TuyaHub.Domain;
using TuyaHub.Domain.ValueObjects;

namespace TuyaHub.Application.Commands;

/// <summary>Marker for an inbound command targeting a specific device (dispatched via MediatR from the KNX ACL).</summary>
public interface IDeviceCommand : IRequest
{
    DeviceName Device { get; }
}

/// <summary>
/// Base handler for the command path (KNX → device): resolve the aggregate, delegate the rule to it
/// via <see cref="Apply"/>, and send the resulting datapoint writes to the gateway. No status events
/// are raised here — the bridge reflects state to KNX only from authoritative device readback, so a
/// command is never echoed unconfirmed.
/// </summary>
public abstract class DeviceCommandHandler<TCommand>(
    IDeviceRegistry registry,
    IDeviceGateway gateway,
    ILogger logger) : IRequestHandler<TCommand>
    where TCommand : IDeviceCommand
{
    public async Task Handle(TCommand request, CancellationToken cancellationToken)
    {
        // These handlers target the Wind Calm aggregate; a device of another type (or none) is ignored.
        if (registry.Find(request.Device) is not Device device)
        {
            logger.LogWarning("Command {Command} for unknown or incompatible device {Device} ignored.",
                typeof(TCommand).Name, request.Device);
            return;
        }

        var command = Apply(device, request);
        if (command.IsEmpty)
        {
            return;
        }

        await gateway.SendAsync(request.Device, command, cancellationToken);
    }

    /// <summary>Delegates the command to the aggregate and returns the datapoint writes to send.</summary>
    protected abstract DeviceCommand Apply(Device device, TCommand request);
}
