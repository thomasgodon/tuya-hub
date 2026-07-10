using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TuyaHub.Infrastructure.Tuya;

/// <summary>
/// Hosted service that runs every device's persistent connection for the app lifetime. Connections
/// are independent — a single device failing (or being absent) never affects the others (FR-10).
/// </summary>
internal sealed class TuyaConnectionSupervisor(TuyaConnectionManager manager, ILogger<TuyaConnectionSupervisor> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connections = manager.Connections;
        if (connections.Count == 0)
        {
            logger.LogWarning("No enabled Tuya devices configured; the bridge has nothing to do.");
            return;
        }

        logger.LogInformation("Starting {Count} Tuya device connection(s).", connections.Count);
        await Task.WhenAll(connections.Select(c => c.RunAsync(stoppingToken)));
    }
}
