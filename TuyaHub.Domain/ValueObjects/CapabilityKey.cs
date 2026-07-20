namespace TuyaHub.Domain.ValueObjects;

/// <summary>
/// Stable identifier for one capability of a device (e.g. <c>"FanPower"</c>, <c>"LightCct"</c>).
/// The generic currency (<see cref="DeviceCommand"/> / <see cref="DeviceReport"/> bags and
/// <see cref="TuyaHub.Domain.Events.DeviceCapabilityChanged"/>) keys on this rather than on a fixed
/// enum, so a new device type declares its own capability keys without editing shared code. Wind Calm's
/// keys are defined once in <see cref="TuyaHub.Domain.WindCalmCapabilities"/> and match the historical
/// capability names 1:1.
/// </summary>
public readonly record struct CapabilityKey
{
    public string Value { get; }

    public CapabilityKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Capability key must not be empty.", nameof(value));
        }

        Value = value;
    }

    public override string ToString() => Value;
}
