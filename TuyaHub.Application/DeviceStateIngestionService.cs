using MediatR;
using Microsoft.Extensions.Logging;
using TuyaHub.Application.Abstractions;
using TuyaHub.Domain;
using TuyaHub.Domain.Events;
using TuyaHub.Domain.ValueObjects;

namespace TuyaHub.Application;

/// <summary>
/// Applies observed device state / connectivity to the aggregate and publishes the resulting domain
/// events via MediatR. All business rules live in the aggregate; this service only orchestrates.
/// </summary>
internal sealed class DeviceStateIngestionService(
    IDeviceRegistry registry,
    IPublisher publisher,
    ILogger<DeviceStateIngestionService> logger) : IDeviceStateIngestionService
{
    public Task ReportStateAsync(DeviceName device, DeviceReport report, CancellationToken cancellationToken)
    {
        var aggregate = registry.Find(device);
        if (aggregate is null)
        {
            logger.LogWarning("Received state for unknown device {Device}; ignoring.", device);
            return Task.CompletedTask;
        }

        var events = aggregate.ApplyReportedState(report);

        // Log each genuine fan/light state change once — ApplyReportedState only returns capabilities
        // whose value actually changed (or the first-report baseline), never one line per poll.
        foreach (var change in events.OfType<DeviceCapabilityChanged>())
        {
            logger.LogInformation("Device {Device} {Capability} changed to {Value}.",
                device, change.Capability, change.Value);
        }

        return PublishAll(events, cancellationToken);
    }

    public Task ReportConnectivityAsync(DeviceName device, bool online, CancellationToken cancellationToken)
    {
        var aggregate = registry.Find(device);
        if (aggregate is null)
        {
            logger.LogWarning("Received connectivity for unknown device {Device}; ignoring.", device);
            return Task.CompletedTask;
        }

        var events = online ? aggregate.MarkReconnected() : aggregate.MarkOffline();
        return PublishAll(events, cancellationToken);
    }

    private async Task PublishAll(IReadOnlyList<INotification> events, CancellationToken cancellationToken)
    {
        foreach (var domainEvent in events)
        {
            await publisher.Publish(domainEvent, cancellationToken);
        }
    }
}
