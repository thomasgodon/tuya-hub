namespace TuyaHub.Application.Dashboard;

/// <summary>
/// Flat, serialization-friendly projection of the whole hub for the dashboard: the KNX bus state,
/// every configured device's current state, and any devices discovered on the LAN but not yet
/// configured. Serialized to camelCase JSON and pushed over SSE on each device state change. The
/// configured-device projection carries no secrets; the <see cref="Discovered"/> entries intentionally
/// include the device id (gwId) and IP — both needed for an operator to add the device to the config,
/// and both broadcast in the clear on the LAN — but never the local key (which no beacon carries).
/// </summary>
public sealed class DashboardSnapshot
{
    public DateTimeOffset Timestamp { get; init; }
    public KnxConnectionDto Knx { get; init; } = new();
    public DeviceDto[] Devices { get; init; } = [];

    /// <summary>Devices seen broadcasting on the LAN that are not in the configured device list.</summary>
    public DiscoveredDeviceDto[] Discovered { get; init; } = [];
}

public sealed class KnxConnectionDto
{
    public bool Enabled { get; init; }
    public bool Connected { get; init; }
}

public sealed class DeviceDto
{
    public string Name { get; init; } = string.Empty;

    /// <summary>The device's profile id (e.g. "wind-calm"); the UI picks its renderer from this.</summary>
    public string ProfileId { get; init; } = string.Empty;

    public bool Online { get; init; }

    /// <summary>Populated for the Wind Calm profile (its bespoke card). Empty for other profiles.</summary>
    public FanDto Fan { get; init; } = new();
    public LightDto Light { get; init; } = new();

    /// <summary>Generic labelled capability values for profiles rendered by the capability-driven card.</summary>
    public CapabilitySection[] Sections { get; init; } = [];
}

/// <summary>One labelled capability value on the generic dashboard card.</summary>
public sealed class CapabilitySection
{
    public string Label { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
}

public sealed class FanDto
{
    public bool Power { get; init; }

    /// <summary>Speed on the KNX status scale: 0 when off, otherwise the level 1..6.</summary>
    public int SpeedStatus { get; init; }

    /// <summary>Blade direction: "Forward" (summer) or "Reverse" (winter).</summary>
    public string Direction { get; init; } = string.Empty;

    public int TimerMinutes { get; init; }
    public bool TimerRunning { get; init; }
}

/// <summary>
/// A Tuya device discovered broadcasting on the LAN but not present in the configured device list.
/// Everything here comes from the device's UDP discovery beacon (no local key involved).
/// </summary>
public sealed class DiscoveredDeviceDto
{
    /// <summary>Tuya device id (gwId) — the value the operator puts in <c>TuyaOptions.Devices[].DeviceId</c>.</summary>
    public string DeviceId { get; init; } = string.Empty;

    public string IpAddress { get; init; } = string.Empty;

    /// <summary>Advertised Tuya local protocol version (e.g. "3.3").</summary>
    public string ProtocolVersion { get; init; } = string.Empty;

    /// <summary>Tuya product key / category identifier, when advertised.</summary>
    public string ProductKey { get; init; } = string.Empty;
}

public sealed class LightDto
{
    public bool Power { get; init; }
    public int BrightnessPercent { get; init; }
    public int BrightnessDp { get; init; }

    /// <summary>Colour-temperature step as a percentage (0 / 50 / 100).</summary>
    public int CctPercent { get; init; }

    /// <summary>Colour-temperature step on the device scale (0 / 500 / 1000).</summary>
    public int CctStep { get; init; }
}
