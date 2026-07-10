using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TuyaHub.Application.Abstractions;
using TuyaHub.Domain;
using TuyaHub.Infrastructure.Options;
using TuyaHub.Infrastructure.Tuya;

namespace TuyaHub.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the infrastructure layer: options binding (KNX / Tuya / device mappings), the
    /// config-backed device registry, and the Tuya ACL (connection manager, gateway, and the
    /// supervisor that owns the persistent per-device connections). The KNX ACL is wired here too
    /// once it exists; for now only the Tuya side is composed.
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

        return services;
    }
}
