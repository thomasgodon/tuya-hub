namespace TuyaHub.Domain.ValueObjects;

/// <summary>
/// Per-device secret used for local AES-128 encryption on the Tuya 3.3 protocol.
/// Obtained once via the Tuya IoT platform; used only locally thereafter.
/// </summary>
public readonly record struct LocalKey
{
    public string Value { get; }

    private LocalKey(string value) => Value = value;

    public static LocalKey Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Local key must not be empty.", nameof(value));
        }

        return new LocalKey(value.Trim());
    }

    /// <summary>Redacted representation so the secret is never logged verbatim.</summary>
    public override string ToString() => "****";
}
