using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TuyaHub.Infrastructure.Options;
using TuyaHub.Infrastructure.Resilience;

namespace TuyaHub.Infrastructure.Knx;

/// <summary>
/// Hosted service that keeps the KNX connection alive for the app lifetime (M5). It connects, waits
/// for the bus to drop (<see cref="KnxBridge.WaitForDropAsync"/>), then reconnects with jittered
/// backoff. Because every reconnect goes through <c>EnsureConnectedAsync</c>, the inbound
/// <c>GroupMessageReceived</c> subscription is re-attached — so the KNX→Tuya command path self-heals
/// after a gateway restart or LAN loss, not only on the next outbound status write.
/// </summary>
internal sealed class KnxConnectionSupervisor(
    KnxBridge bridge,
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

                // Block until the connection drops, then fall through to the backoff + reconnect.
                await bridge.WaitForDropAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "KNX connection error; retrying with backoff.");
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
}
