using MediatR;
using TuyaHub.Domain.Events;

namespace TuyaHub.Infrastructure.Knx;

/// <summary>
/// Translates domain state-change events into KNX status writes (the Tuya → KNX feedback path). One
/// handler covers every in-scope event; each <c>Handle</c> maps the event to a capability + encoded
/// value and hands it to the <see cref="KnxBridge"/>, which owns dedup, caching and the bus write.
/// </summary>
internal sealed class DeviceEventKnxHandler(KnxBridge bridge) :
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
    public Task Handle(FanPowerChanged n, CancellationToken ct)
        => bridge.PublishAsync(n.Device, Capability.FanPower, KnxDpt.Bool(n.IsOn), ct);

    public Task Handle(FanSpeedChanged n, CancellationToken ct)
        => bridge.PublishAsync(n.Device, Capability.FanSpeed, KnxDpt.Count(n.StatusValue), ct);

    public Task Handle(FanDirectionChanged n, CancellationToken ct)
        => bridge.PublishAsync(n.Device, Capability.FanDirection, KnxDpt.Bool(n.Direction == Domain.ValueObjects.FanDirection.Reverse), ct);

    public Task Handle(FanTimerChanged n, CancellationToken ct)
        => bridge.PublishAsync(n.Device, Capability.FanTimer, KnxDpt.Minutes(n.Minutes), ct);

    public Task Handle(LightPowerChanged n, CancellationToken ct)
        => bridge.PublishAsync(n.Device, Capability.LightPower, KnxDpt.Bool(n.IsOn), ct);

    public Task Handle(LightBrightnessChanged n, CancellationToken ct)
        => bridge.PublishAsync(n.Device, Capability.LightBrightness, KnxDpt.Percent(n.Brightness.ToPercent()), ct);

    public Task Handle(LightCctChanged n, CancellationToken ct)
        => bridge.PublishAsync(n.Device, Capability.LightCct, KnxDpt.Percent(n.ColourTemperature.ToPercent()), ct);

    public Task Handle(DeviceWentOffline n, CancellationToken ct)
        => bridge.PublishAsync(n.Device, Capability.Availability, KnxDpt.Bool(false), ct);

    public Task Handle(DeviceReconnected n, CancellationToken ct)
        => bridge.PublishAsync(n.Device, Capability.Availability, KnxDpt.Bool(true), ct);
}
