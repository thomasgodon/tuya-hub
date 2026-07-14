using Microsoft.Extensions.DependencyInjection;
using TuyaHub.Application.Abstractions;
using TuyaHub.Application.Dashboard;

namespace TuyaHub.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the application layer: MediatR (scanning this assembly for command/notification
    /// handlers), the device-state ingestion service that drives the feedback path, and the dashboard
    /// snapshot broadcaster that fans hub state out to Server-Sent Events subscribers.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        services.AddSingleton<IDeviceStateIngestionService, DeviceStateIngestionService>();
        services.AddSingleton<IDeviceSnapshotBroadcaster, DeviceSnapshotBroadcaster>();

        return services;
    }
}
