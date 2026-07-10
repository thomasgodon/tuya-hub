namespace TuyaHub.Domain.ValueObjects;

/// <summary>
/// Tuya local wire-protocol version. The MVP targets Wind Calm on <c>3.3</c>; <c>3.1</c> is the
/// only other version the codec supports. 3.4/3.5 require a session handshake and are out of scope.
/// </summary>
public readonly record struct ProtocolVersion
{
    public const string V31 = "3.1";
    public const string V33 = "3.3";

    public string Value { get; }

    private ProtocolVersion(string value) => Value = value;

    public static ProtocolVersion Create(string value)
    {
        var normalized = (value ?? string.Empty).Trim();

        return normalized switch
        {
            V31 or V33 => new ProtocolVersion(normalized),
            _ => throw new ArgumentException(
                $"Unsupported Tuya protocol version '{value}'. Supported: {V31}, {V33}.", nameof(value)),
        };
    }

    public override string ToString() => Value;
}
