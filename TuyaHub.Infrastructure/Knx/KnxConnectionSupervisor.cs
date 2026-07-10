using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TuyaHub.Infrastructure.Knx;

/// <summary>
/// Hosted service that opens the KNX connection at startup so read requests are answerable before the
/// first status write. Mirrors <c>TuyaConnectionSupervisor</c>. A failed initial connect is logged,
/// not fatal — the bus is re-established lazily on the next write/respond (robust reconnect is M5).
/// </summary>
internal sealed class KnxConnectionSupervisor(KnxBridge bridge, ILogger<KnxConnectionSupervisor> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (bridge.HasWork is false)
        {
            return; // Disabled or nothing mapped — the bridge already logged why.
        }

        try
        {
            await bridge.ConnectAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Initial KNX connect failed; will retry on the next status write.");
        }
    }
}
