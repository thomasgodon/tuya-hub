using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TuyaHub.Application.Abstractions;
using TuyaHub.Application.Dashboard.Options;
using TuyaHub.Domain;
using TuyaHub.Domain.Events;
using TuyaHub.Infrastructure.Dashboard;
using TuyaHub.Infrastructure.Dashboard.Notifications;
using TuyaHub.Infrastructure.Knx;
using TuyaHub.Infrastructure.Options;
using TuyaHub.Infrastructure.Profiles;
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
        services.Configure<DashboardOptions>(configuration.GetSection(nameof(DashboardOptions)));

        // Device profiles — the registry of supported device types (Wind Calm is profile #1). A new
        // device type is added by registering another DeviceProfile here.
        services.AddSingleton<IDeviceProfileRegistry>(_ => new DeviceProfileRegistry([WindCalmProfile.Create()]));
        services.AddSingleton<ConfiguredDeviceProfiles>();

        // Domain registry — one aggregate per enabled configured device, built via its profile factory.
        services.AddSingleton<IDeviceRegistry, ConfigurationDeviceRegistry>();

        // Tuya ACL: the manager owns the connections; the gateway sends commands into them and the
        // supervisor runs their lifetimes for the app's duration.
        services.AddSingleton<TuyaConnectionManager>();
        services.AddSingleton<IDeviceGateway, TuyaDeviceGateway>();
        services.AddHostedService<TuyaConnectionSupervisor>();

        // Passive LAN discovery: the store holds devices seen broadcasting on the network; the service
        // wraps TuyaNet's scanner and feeds it. Surfaced on the dashboard only (gated by
        // DashboardOptions.Enabled inside the service), so it lives alongside the Tuya ACL.
        services.AddSingleton<TuyaDiscoveryStore>();
        services.AddHostedService<TuyaDiscoveryService>();

        // KNX ACL (outbound / feedback path): the bridge owns the connection and status cache; its
        // supervisor opens the connection at startup; the handler translates domain events to status
        // writes. Registered explicitly per event type (MediatR dispatches on the concrete type), as
        // DsmrHub registers its sink handlers. The generic DeviceCapabilityChanged event means one
        // registration per handler covers every capability of every device type.
        services.AddSingleton<KnxBridge>();
        services.AddHostedService<KnxConnectionSupervisor>();
        services.AddSingleton<INotificationHandler<DeviceCapabilityChanged>, DeviceEventKnxHandler>();
        services.AddSingleton<INotificationHandler<DeviceWentOffline>, DeviceEventKnxHandler>();
        services.AddSingleton<INotificationHandler<DeviceReconnected>, DeviceEventKnxHandler>();

        // Dashboard feedback path: the publisher projects the whole hub state to a snapshot; the event
        // handler republishes it on every state change; the initializer seeds one snapshot at startup.
        services.AddSingleton<DashboardSnapshotPublisher>();
        services.AddHostedService<DashboardSnapshotInitializer>();
        services.AddSingleton<INotificationHandler<DeviceCapabilityChanged>, DeviceEventDashboardHandler>();
        services.AddSingleton<INotificationHandler<DeviceWentOffline>, DeviceEventDashboardHandler>();
        services.AddSingleton<INotificationHandler<DeviceReconnected>, DeviceEventDashboardHandler>();

        return services;
    }
}
