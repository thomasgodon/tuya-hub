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
    INotificationHandler<DeviceCapabilityChanged>,
    INotificationHandler<DeviceWentOffline>,
    INotificationHandler<DeviceReconnected>
{
    public Task Handle(DeviceCapabilityChanged n, CancellationToken ct) => Publish();
    public Task Handle(DeviceWentOffline n, CancellationToken ct) => Publish();
    public Task Handle(DeviceReconnected n, CancellationToken ct) => Publish();

    private Task Publish()
    {
        publisher.PublishCurrent();
        return Task.CompletedTask;
    }
}
