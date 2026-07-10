namespace TuyaHub.Domain.ValueObjects;

/// <summary>
/// Tuya device identifier (<c>gwId</c>/<c>devId</c>). Required to talk to a device locally.
/// </summary>
public readonly record struct DeviceId
{
    public string Value { get; }

    private DeviceId(string value) => Value = value;

    public static DeviceId Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Device id must not be empty.", nameof(value));
        }

        return new DeviceId(value.Trim());
    }

    public override string ToString() => Value;
}
