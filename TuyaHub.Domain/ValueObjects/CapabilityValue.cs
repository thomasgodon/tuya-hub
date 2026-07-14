namespace TuyaHub.Domain.ValueObjects;

/// <summary>
/// A domain-neutral scalar carried by <see cref="TuyaHub.Domain.Events.DeviceCapabilityChanged"/>: a
/// bool, an int, or a string. Kept protocol-agnostic on purpose — the aggregate emits the plain scalar
/// a capability's status represents (on/off, a level 0..6, a percentage 0..100, minutes, …) and each
/// profile's capability binding decides how to encode it onto the wire (the KNX DPT choice lives in the
/// ACL, never here).
/// </summary>
public readonly record struct CapabilityValue
{
    public enum ValueKind { Bool, Int, Text }

    private readonly object _value;

    private CapabilityValue(ValueKind kind, object value)
    {
        Kind = kind;
        _value = value;
    }

    public ValueKind Kind { get; }

    public static CapabilityValue Bool(bool value) => new(ValueKind.Bool, value);
    public static CapabilityValue Int(int value) => new(ValueKind.Int, value);
    public static CapabilityValue Text(string value) => new(ValueKind.Text, value);

    public bool AsBool() => (bool)_value;
    public int AsInt() => (int)_value;
    public string AsText() => (string)_value;

    /// <summary>The boxed underlying value, for generic consumers (e.g. the dashboard projection).</summary>
    public object Raw => _value;

    public override string ToString() => _value.ToString() ?? string.Empty;
}
