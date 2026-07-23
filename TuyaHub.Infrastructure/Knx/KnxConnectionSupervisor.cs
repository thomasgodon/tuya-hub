using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TuyaHub.Infrastructure.Dashboard;
using TuyaHub.Infrastructure.Options;
using TuyaHub.Infrastructure.Resilience;

namespace TuyaHub.Infrastructure.Knx;

/// <summary>
/// Hosted service that keeps the KNX connection alive for the app lifetime (M5). It connects, waits
/// for the bus to drop (<see cref="KnxBridge.WaitForDropAsync"/>), then reconnects with jittered
/// backoff. Because every reconnect goes through <c>EnsureConnectedAsync</c>, the inbound
/// <c>GroupMessageReceived</c> subscription is re-attached — so the KNX→Tuya command path self-heals
/// after a gateway restart or LAN loss, not only on the next outbound status write.
/// <para>
/// It also republishes the dashboard snapshot on each connect and drop, so the KNX pill tracks the bus
/// in real time. The <see cref="KnxBridge"/> can't do this itself (the publisher already depends on the
/// bridge, which would be a DI cycle); the supervisor is the one place that observes both transitions.
/// The snapshot is otherwise only republished on device state-change events, so without this the pill
/// stays stale after a connect that happens between device events.
/// </para>
/// </summary>
internal sealed class KnxConnectionSupervisor(
    KnxBridge bridge,
    DashboardSnapshotPublisher dashboard,
    IOptions<KnxOptions> options,
    ILogger<KnxConnectionSupervisor> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (bridge.HasWork is false)
        {
            return; // Disabled or nothing mapped — the bridge already logged why.
        }

        var knx = options.Value;
        var backoff = new BackoffPolicy(
            TimeSpan.FromSeconds(Math.Max(1, knx.ReconnectInitialBackoffSeconds)),
            TimeSpan.FromSeconds(Math.Max(knx.ReconnectMaxBackoffSeconds, knx.ReconnectInitialBackoffSeconds)));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await bridge.ConnectAsync(stoppingToken);
                backoff.Reset();
                RefreshDashboard(); // Now connected — flip the KNX pill without waiting for a device event.

                // Block until the connection drops, then fall through to the backoff + reconnect.
                await bridge.WaitForDropAsync(stoppingToken);
                RefreshDashboard(); // Dropped — reflect it immediately.
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "KNX connection error; retrying with backoff.");
                RefreshDashboard(); // Connect failed — keep the pill honest (disconnected).
            }

            var delay = backoff.Next();
            logger.LogInformation("Reconnecting to KNX in {Backoff:0.#}s.", delay.TotalSeconds);
            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    // Republish the dashboard snapshot (which reads KnxBridge.IsConnected) so the KNX pill reflects the
    // current bus state. No-op when the dashboard is disabled; guarded so a publish fault never stalls
    // the reconnect loop.
    private void RefreshDashboard()
    {
        try
        {
            dashboard.PublishCurrent();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Dashboard snapshot refresh after a KNX connection change failed.");
        }
    }
}
