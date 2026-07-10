namespace TuyaHub.Domain.ValueObjects;

/// <summary>
/// Stable identifier for a device that ties its Tuya connection to its KNX mapping.
/// Corresponds to <c>TuyaOptions.Devices[].Name</c> and the key in <c>DeviceMappings</c>.
/// </summary>
public readonly record struct DeviceName
{
    public string Value { get; }

    private DeviceName(string value) => Value = value;

    public static DeviceName Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Device name must not be empty.", nameof(value));
        }

        return new DeviceName(value.Trim());
    }

    public override string ToString() => Value;
}
