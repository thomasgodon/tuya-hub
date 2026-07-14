using Microsoft.Extensions.Hosting;

namespace TuyaHub.Infrastructure.Dashboard;

/// <summary>
/// Seeds one dashboard snapshot at startup so a browser that connects before any device event still
/// sees every configured device (initially offline until the Tuya connections come up). No-op when
/// the dashboard is disabled — <see cref="DashboardSnapshotPublisher.PublishCurrent"/> guards that.
/// </summary>
internal sealed class DashboardSnapshotInitializer(DashboardSnapshotPublisher publisher) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        publisher.PublishCurrent();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
