using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TuyaHub.Application.Abstractions;
using TuyaHub.Domain;
using TuyaHub.Domain.Events;
using TuyaHub.Infrastructure.Knx;
using TuyaHub.Infrastructure.Options;
using TuyaHub.Infrastructure.Tuya;

namespace TuyaHub.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the infrastructure layer: options binding (KNX / Tuya / device mappings), the
    /// config-backed device registry, the Tuya ACL (connection manager, gateway, and the supervisor
    /// that owns the persistent per-device connections), and the KNX ACL (the bridge, its connection
    /// supervisor, and the event handlers that mirror device state to the status group addresses).
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<KnxOptions>(configuration.GetSection("KnxOptions"));
        services.Configure<TuyaOptions>(configuration.GetSection("TuyaOptions"));
        services.Configure<DeviceMappingOptions>(configuration.GetSection("DeviceMappings"));

        // Domain registry — one Device aggregate per enabled configured device.
        services.AddSingleton<IDeviceRegistry, ConfigurationDeviceRegistry>();

        // Tuya ACL: the manager owns the connections; the gateway sends commands into them and the
        // supervisor runs their lifetimes for the app's duration.
        services.AddSingleton<TuyaConnectionManager>();
        services.AddSingleton<IDeviceGateway, TuyaDeviceGateway>();
        services.AddHostedService<TuyaConnectionSupervisor>();

        // KNX ACL (outbound / feedback path): the bridge owns the connection and status cache; its
        // supervisor opens the connection at startup; the handlers translate domain events to status
        // writes. Registered explicitly per event type (MediatR dispatches on the concrete type), as
        // DsmrHub registers its sink handlers. Light CCT (M6) has no handler here.
        services.AddSingleton<KnxBridge>();
        services.AddHostedService<KnxConnectionSupervisor>();
        services.AddSingleton<INotificationHandler<FanPowerChanged>, DeviceEventKnxHandler>();
        services.AddSingleton<INotificationHandler<FanSpeedChanged>, DeviceEventKnxHandler>();
        services.AddSingleton<INotificationHandler<FanDirectionChanged>, DeviceEventKnxHandler>();
        services.AddSingleton<INotificationHandler<FanTimerChanged>, DeviceEventKnxHandler>();
        services.AddSingleton<INotificationHandler<LightPowerChanged>, DeviceEventKnxHandler>();
        services.AddSingleton<INotificationHandler<LightBrightnessChanged>, DeviceEventKnxHandler>();
        services.AddSingleton<INotificationHandler<DeviceWentOffline>, DeviceEventKnxHandler>();
        services.AddSingleton<INotificationHandler<DeviceReconnected>, DeviceEventKnxHandler>();

        return services;
    }
}
