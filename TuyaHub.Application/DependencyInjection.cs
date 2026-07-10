using Microsoft.Extensions.DependencyInjection;
using TuyaHub.Application.Abstractions;

namespace TuyaHub.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the application layer: MediatR (scanning this assembly for command/notification
    /// handlers) and the device-state ingestion service that drives the feedback path.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        services.AddSingleton<IDeviceStateIngestionService, DeviceStateIngestionService>();

        return services;
    }
}
