using TuyaHub.Domain.ValueObjects;

namespace TuyaHub.Domain;

/// <summary>
/// The fan half of a Wind Calm unit. A state-holding entity within the <see cref="Device"/>
/// aggregate; all transitions and invariants are driven by the aggregate, so its state is only
/// mutable from inside the domain assembly.
/// </summary>
public sealed class FanEndpoint
{
    /// <summary>Fan power (DP 60).</summary>
    public bool Power { get; internal set; }

    /// <summary>Fan speed level 1..6 (DP 62).</summary>
    public SpeedLevel Speed { get; internal set; } = SpeedLevel.Lowest;

    /// <summary>Blade direction (DP 63).</summary>
    public FanDirection Direction { get; internal set; } = ValueObjects.FanDirection.Forward;

    /// <summary>Remaining auto-off countdown (DP 64), owned by the device MCU.</summary>
    public CountdownTimer Timer { get; internal set; } = CountdownTimer.None;

    /// <summary>Confirmation-beep enable (DP 66).</summary>
    public bool Beep { get; internal set; }

    /// <summary>Speed as reported on the KNX 5.010 status GA: 0 when off, otherwise the level.</summary>
    public int SpeedStatus => Power ? Speed.Value : 0;
}
