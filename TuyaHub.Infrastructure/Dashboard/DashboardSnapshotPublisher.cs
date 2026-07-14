using System.Text.Json;
using Microsoft.Extensions.Options;
using TuyaHub.Application.Dashboard;
using TuyaHub.Application.Dashboard.Options;
using TuyaHub.Domain;
using TuyaHub.Infrastructure.Knx;
using TuyaHub.Infrastructure.Options;

namespace TuyaHub.Infrastructure.Dashboard;

/// <summary>
/// Builds the full <see cref="DashboardSnapshot"/> (all configured devices plus the KNX bus state)
/// from the device registry and publishes it to the SSE broadcaster. Shared by the event handler
/// (publishes on every state change) and the startup initializer (seeds an initial snapshot so a
/// fresh browser tab isn't blank before the first device event). Reads the KNX connection state from
/// the <see cref="KnxBridge"/>, so it lives in Infrastructure. Carries no connection secrets.
/// </summary>
internal sealed class DashboardSnapshotPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IDeviceRegistry _registry;
    private readonly IDeviceSnapshotBroadcaster _broadcaster;
    private readonly KnxBridge _knxBridge;
    private readonly KnxOptions _knxOptions;
    private readonly DashboardOptions _dashboardOptions;

    public DashboardSnapshotPublisher(
        IDeviceRegistry registry,
        IDeviceSnapshotBroadcaster broadcaster,
        KnxBridge knxBridge,
        IOptions<KnxOptions> knxOptions,
        IOptions<DashboardOptions> dashboardOptions)
    {
        _registry = registry;
        _broadcaster = broadcaster;
        _knxBridge = knxBridge;
        _knxOptions = knxOptions.Value;
        _dashboardOptions = dashboardOptions.Value;
    }

    /// <summary>Projects the current hub state to a snapshot and publishes it. No-op when disabled.</summary>
    public void PublishCurrent()
    {
        if (_dashboardOptions.Enabled is false)
            return;

        var devices = _registry.Devices
            .Select(device => device.CaptureState())
            .OrderBy(state => state.Name.Value, StringComparer.OrdinalIgnoreCase)
            .Select(ToDto)
            .ToArray();

        var snapshot = new DashboardSnapshot
        {
            Timestamp = DateTimeOffset.UtcNow,
            Knx = new KnxConnectionDto { Enabled = _knxOptions.Enabled, Connected = _knxBridge.IsConnected },
            Devices = devices
        };

        _broadcaster.Publish(JsonSerializer.Serialize(snapshot, JsonOptions));
    }

    private static DeviceDto ToDto(DeviceStateSnapshot state) => new()
    {
        Name = state.Name.Value,
        Online = state.IsOnline,
        Fan = new FanDto
        {
            Power = state.FanPower,
            SpeedStatus = state.FanSpeedStatus,
            Direction = state.FanDirection.ToString(),
            TimerMinutes = state.FanTimerMinutes,
            TimerRunning = state.FanTimerRunning
        },
        Light = new LightDto
        {
            Power = state.LightPower,
            BrightnessPercent = state.LightBrightnessPercent,
            BrightnessDp = state.LightBrightnessDp,
            CctPercent = state.LightCctPercent,
            CctStep = state.LightCctDp
        }
    };
}
