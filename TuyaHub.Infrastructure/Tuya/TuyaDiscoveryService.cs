using com.clusterrr.TuyaNet;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TuyaHub.Application.Dashboard.Options;
using TuyaHub.Infrastructure.Dashboard;

namespace TuyaHub.Infrastructure.Tuya;

/// <summary>
/// Passively discovers Tuya devices broadcasting on the LAN and feeds them to the dashboard. Drives our
/// own <see cref="TuyaLanDiscoveryListener"/> (UDP 6666/6667, decrypted with the universal, non-secret
/// discovery key — no local key needed) inside the Tuya ACL, translating its protocol type into the
/// domain-agnostic <see cref="TuyaDiscoveryStore"/>. Purely read-only: it binds and listens, never
/// probes a device or writes any configuration (UC-01).
///
/// The listener replaces TuyaNet's <c>TuyaScanner</c>, whose library-owned thread rethrew any decode
/// failure and crashed the host on an undecodable (e.g. protocol-3.5) beacon; ours decodes each packet
/// inside a try/catch and simply skips the bad ones.
///
/// Gated by <see cref="DashboardOptions.Enabled"/> — discovery is only ever surfaced on the dashboard,
/// so when the dashboard is off the listener is not started and no UDP port is bound.
/// </summary>
internal sealed class TuyaDiscoveryService(
    TuyaDiscoveryStore store,
    DashboardSnapshotPublisher publisher,
    IOptions<DashboardOptions> dashboardOptions,
    ILogger<TuyaDiscoveryService> logger)
    : BackgroundService
{
    private static readonly TimeSpan PruneInterval = TimeSpan.FromSeconds(30);

    /// <summary>Drop a device that hasn't beaconed for this long (beacons arrive every ~5 s).</summary>
    private static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(90);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (dashboardOptions.Value.Enabled is false)
        {
            logger.LogInformation("Dashboard disabled; Tuya LAN discovery will not run.");
            return;
        }

        var listener = new TuyaLanDiscoveryListener(OnBeacon, logger);

        try
        {
            listener.Start(stoppingToken);
            logger.LogInformation("Tuya LAN discovery started (UDP 6666/6667).");
        }
        catch (Exception ex)
        {
            // Most likely the discovery UDP port is already bound by another process (UC-01 error
            // scenario). Discovery is a best-effort convenience — log and stay down rather than
            // faulting the host and its device connections.
            logger.LogWarning(ex, "Tuya LAN discovery could not start; continuing without it.");
            await listener.DisposeAsync();
            return;
        }

        try
        {
            using var timer = new PeriodicTimer(PruneInterval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                if (store.PruneOlderThan(DateTimeOffset.UtcNow - StaleAfter))
                    publisher.PublishCurrent();
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        finally
        {
            await listener.DisposeAsync();
        }
    }

    private void OnBeacon(TuyaDeviceScanInfo info)
    {
        // Raised on the listener's receive-loop tasks; TuyaDiscoveryStore is thread-safe.
        if (store.Upsert(info.GwId, info.IP, info.Version, info.ProductKey, DateTimeOffset.UtcNow))
            publisher.PublishCurrent();
    }
}
