namespace TuyaHub.Application.Dashboard;

/// <summary>
/// Flat, serialization-friendly projection of the whole hub for the dashboard: the KNX bus state and
/// every configured device's current state. Serialized to camelCase JSON and pushed over SSE on each
/// device state change. Carries no secrets (no IP / device id / local key).
/// </summary>
public sealed class DashboardSnapshot
{
    public DateTimeOffset Timestamp { get; init; }
    public KnxConnectionDto Knx { get; init; } = new();
    public DeviceDto[] Devices { get; init; } = [];
}

public sealed class KnxConnectionDto
{
    public bool Enabled { get; init; }
    public bool Connected { get; init; }
}

public sealed class DeviceDto
{
    public string Name { get; init; } = string.Empty;
    public bool Online { get; init; }
    public FanDto Fan { get; init; } = new();
    public LightDto Light { get; init; } = new();
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
