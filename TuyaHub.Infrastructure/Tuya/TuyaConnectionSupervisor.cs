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
        await Task.WhenAll(connections.Select(c => RunGuardedAsync(c, stoppingToken)));
    }

    /// <summary>
    /// Isolates one device's supervision loop (FR-10): <see cref="TuyaConnection.RunAsync"/> already
    /// handles its own reconnects, but should it ever throw unexpectedly, swallowing it here keeps the
    /// fault from faulting <see cref="Task.WhenAll"/> and tearing down the host and every other device.
    /// </summary>
    private async Task RunGuardedAsync(TuyaConnection connection, CancellationToken stoppingToken)
    {
        try
        {
            await connection.RunAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Tuya device {Device} supervision loop terminated unexpectedly.", connection.Name);
        }
    }
}
