using System.Text.Json;
using Microsoft.Extensions.Options;
using TuyaHub.Application.Dashboard;
using TuyaHub.Application.Dashboard.Options;
using TuyaHub.Domain;
using TuyaHub.Domain.ValueObjects;
using TuyaHub.Infrastructure.Knx;
using TuyaHub.Infrastructure.Options;
using TuyaHub.Infrastructure.Profiles;
using TuyaHub.Infrastructure.Tuya;

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
    private readonly TuyaDiscoveryStore _discovery;
    private readonly ConfiguredDeviceProfiles _profiles;
    private readonly KnxOptions _knxOptions;
    private readonly TuyaOptions _tuyaOptions;
    private readonly DashboardOptions _dashboardOptions;

    public DashboardSnapshotPublisher(
        IDeviceRegistry registry,
        IDeviceSnapshotBroadcaster broadcaster,
        KnxBridge knxBridge,
        TuyaDiscoveryStore discovery,
        ConfiguredDeviceProfiles profiles,
        IOptions<KnxOptions> knxOptions,
        IOptions<TuyaOptions> tuyaOptions,
        IOptions<DashboardOptions> dashboardOptions)
    {
        _registry = registry;
        _broadcaster = broadcaster;
        _knxBridge = knxBridge;
        _discovery = discovery;
        _profiles = profiles;
        _knxOptions = knxOptions.Value;
        _tuyaOptions = tuyaOptions.Value;
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

        // Discovered devices that aren't already configured (matched by Tuya device id / gwId).
        var configuredIds = _tuyaOptions.Devices
            .Select(d => d.DeviceId)
            .Where(id => string.IsNullOrWhiteSpace(id) is false)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var discovered = _discovery.Snapshot()
            .Where(d => configuredIds.Contains(d.DeviceId) is false)
            .OrderBy(d => d.IpAddress, StringComparer.OrdinalIgnoreCase)
            .ThenBy(d => d.DeviceId, StringComparer.OrdinalIgnoreCase)
            .Select(d => new DiscoveredDeviceDto
            {
                DeviceId = d.DeviceId,
                IpAddress = d.IpAddress,
                ProtocolVersion = d.ProtocolVersion,
                ProductKey = d.ProductKey
            })
            .ToArray();

        var snapshot = new DashboardSnapshot
        {
            Timestamp = DateTimeOffset.UtcNow,
            Knx = new KnxConnectionDto { Enabled = _knxOptions.Enabled, Connected = _knxBridge.IsConnected },
            Devices = devices,
            Discovered = discovered
        };

        _broadcaster.Publish(JsonSerializer.Serialize(snapshot, JsonOptions));
    }

    private DeviceDto ToDto(DeviceStateSnapshot state) => Project(_profiles.For(state.Name), state);

    // Internal-static so the projection is unit-testable without the full publisher graph.
    internal static DeviceDto Project(DeviceProfile profile, DeviceStateSnapshot state)
    {
        return new DeviceDto
        {
            Name = state.Name.Value,
            ProfileId = profile.ProfileId,
            Online = state.IsOnline,
            // Populated for Wind Calm's bespoke card; harmless defaults for other profiles.
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
            },
            Sections = BuildSections(profile, state)
        };
    }

    // Generic capability-driven card: one labelled row per capability whose profile binding declares a
    // dashboard field and whose value is present in the snapshot. Empty for Wind Calm (no dashboard fields).
    private static CapabilitySection[] BuildSections(DeviceProfile profile, DeviceStateSnapshot state)
    {
        var sections = new List<CapabilitySection>();

        foreach (var binding in profile.Capabilities)
        {
            if (binding.Dashboard is not { } field || state.Capabilities.TryGetValue(binding.Key, out var value) is false)
            {
                continue;
            }

            sections.Add(new CapabilitySection { Label = field.Label, Value = Format(value, field.Unit) });
        }

        return [.. sections];
    }

    private static string Format(CapabilityValue value, string? unit) => value.Kind switch
    {
        CapabilityValue.ValueKind.Bool => value.AsBool() ? "On" : "Off",
        CapabilityValue.ValueKind.Int => unit is null ? value.AsInt().ToString() : $"{value.AsInt()} {unit}",
        _ => value.AsText(),
    };
}
