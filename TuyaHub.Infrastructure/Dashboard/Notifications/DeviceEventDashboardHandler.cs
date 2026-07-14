using MediatR;
using TuyaHub.Domain.Events;

namespace TuyaHub.Infrastructure.Dashboard.Notifications;

/// <summary>
/// Republishes the whole dashboard snapshot on every device state change (the Tuya/KNX → dashboard
/// feedback path). One handler covers all in-scope events; each <c>Handle</c> just triggers a full
/// re-projection via <see cref="DashboardSnapshotPublisher"/>, since the device aggregates are the
/// single source of truth. Registered once per concrete event type (MediatR dispatches on the
/// concrete type), mirroring the KNX event handler.
/// </summary>
internal sealed class DeviceEventDashboardHandler(DashboardSnapshotPublisher publisher) :
    INotificationHandler<FanPowerChanged>,
    INotificationHandler<FanSpeedChanged>,
    INotificationHandler<FanDirectionChanged>,
    INotificationHandler<FanTimerChanged>,
    INotificationHandler<LightPowerChanged>,
    INotificationHandler<LightBrightnessChanged>,
    INotificationHandler<LightCctChanged>,
    INotificationHandler<DeviceWentOffline>,
    INotificationHandler<DeviceReconnected>
{
    public Task Handle(FanPowerChanged n, CancellationToken ct) => Publish();
    public Task Handle(FanSpeedChanged n, CancellationToken ct) => Publish();
    public Task Handle(FanDirectionChanged n, CancellationToken ct) => Publish();
    public Task Handle(FanTimerChanged n, CancellationToken ct) => Publish();
    public Task Handle(LightPowerChanged n, CancellationToken ct) => Publish();
    public Task Handle(LightBrightnessChanged n, CancellationToken ct) => Publish();
    public Task Handle(LightCctChanged n, CancellationToken ct) => Publish();
    public Task Handle(DeviceWentOffline n, CancellationToken ct) => Publish();
    public Task Handle(DeviceReconnected n, CancellationToken ct) => Publish();

    private Task Publish()
    {
        publisher.PublishCurrent();
        return Task.CompletedTask;
    }
}
