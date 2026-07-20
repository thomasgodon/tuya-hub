namespace TuyaHub.Domain.ValueObjects;

/// <summary>
/// Tuya local wire-protocol version. <c>3.1</c>/<c>3.3</c> are served by the TuyaNet codec;
/// <c>3.4</c> and <c>3.5</c> are served by tuya-hub's own codec, which performs the session-key
/// negotiation handshake those versions require (3.4 keeps the <c>55AA</c> frame but uses
/// HMAC-SHA256 + a session-keyed AES-ECB payload; 3.5 uses the <c>6699</c> frame + AES-GCM).
/// </summary>
public readonly record struct ProtocolVersion
{
    public const string V31 = "3.1";
    public const string V33 = "3.3";
    public const string V34 = "3.4";
    public const string V35 = "3.5";

    public string Value { get; }

    private ProtocolVersion(string value) => Value = value;

    /// <summary>True for versions that require the 3.4/3.5 session-key negotiation handshake.</summary>
    public bool RequiresSessionHandshake => Value is V34 or V35;

    public static ProtocolVersion Create(string value)
    {
        var normalized = (value ?? string.Empty).Trim();

        return normalized switch
        {
            V31 or V33 or V34 or V35 => new ProtocolVersion(normalized),
            _ => throw new ArgumentException(
                $"Unsupported Tuya protocol version '{value}'. Supported: {V31}, {V33}, {V34}, {V35}.",
                nameof(value)),
        };
    }

    public override string ToString() => Value;
}
