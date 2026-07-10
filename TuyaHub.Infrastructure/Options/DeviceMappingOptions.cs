namespace TuyaHub.Infrastructure.Options;

/// <summary>
/// Per-device KNX group-address mapping, keyed by <see cref="TuyaDeviceOptions.Name"/>. Bound from
/// the <c>DeviceMappings</c> configuration section (a dictionary of device name → mapping).
/// </summary>
public sealed class DeviceMappingOptions : Dictionary<string, DeviceMapping>;

/// <summary>
/// The command and status group addresses for one device. Command (KNX → Tuya) and status
/// (Tuya → KNX) are always separate addresses. An empty string disables that function for this
/// device (the mapping entry is ignored).
/// </summary>
public sealed class DeviceMapping
{
    // Fan
    public string FanPowerCommand { get; set; } = "";
    public string FanPowerStatus { get; set; } = "";
    public string FanSpeedStep { get; set; } = "";       // DPT 3.007 (relative dim)
    public string FanSpeedStatus { get; set; } = "";     // DPT 5.010 (count 0..6)
    public string FanDirectionCommand { get; set; } = "";
    public string FanDirectionStatus { get; set; } = "";
    public string FanTimerCommand { get; set; } = "";    // DPT 7.006 (minutes)
    public string FanTimerStatus { get; set; } = "";

    // Light
    public string LightPowerCommand { get; set; } = "";
    public string LightPowerStatus { get; set; } = "";
    public string LightBrightnessCommand { get; set; } = "";  // DPT 5.001 (%)
    public string LightBrightnessStatus { get; set; } = "";
    public string LightCctCommand { get; set; } = "";         // DPT 5.001 (%), snapped to 3 steps
    public string LightCctStatus { get; set; } = "";

    /// <summary>Optional availability object (DPT 1.001): 1 = online. Empty disables it.</summary>
    public string AvailabilityStatus { get; set; } = "";
}
