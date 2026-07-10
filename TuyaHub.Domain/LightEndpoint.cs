using TuyaHub.Domain.ValueObjects;

namespace TuyaHub.Domain;

/// <summary>
/// The light half of a Wind Calm unit. A state-holding entity within the <see cref="Device"/>
/// aggregate; state is only mutable from inside the domain assembly.
/// </summary>
public sealed class LightEndpoint
{
    /// <summary>Light power (DP 20).</summary>
    public bool Power { get; internal set; }

    /// <summary>Brightness on the device scale 0..1000 (DP 22).</summary>
    public Brightness Brightness { get; internal set; } = Brightness.Off;

    /// <summary>Colour temperature step (DP 23).</summary>
    public ColourTemperature ColourTemperature { get; internal set; } = ValueObjects.ColourTemperature.FromDp(0);
}
