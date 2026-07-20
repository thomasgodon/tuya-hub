namespace TuyaHub.Infrastructure.Options;

/// <summary>
/// Per-device KNX group-address mapping, keyed by <see cref="TuyaDeviceOptions.Name"/>. Bound from
/// the <c>DeviceMappings</c> configuration section (a dictionary of device name → mapping).
/// </summary>
public sealed class DeviceMappingOptions : Dictionary<string, DeviceMapping>;

/// <summary>
/// The command and status group addresses for one device, as a map of capability mapping-key → group
/// address (e.g. <c>"FanPower" → "1/1/1"</c>). The valid keys are declared by the device's
/// profile capability bindings (<c>StatusMappingKey</c> / <c>CommandMappingKey</c>); command
/// (KNX → device) and status (device → KNX) are always separate keys. A missing or empty/whitespace
/// value disables that function for this device. Kept as a string-keyed dictionary (rather than fixed
/// properties) so a new device type contributes its own keys without editing this type; the JSON and
/// double-underscore env-var binding are unchanged (e.g. <c>DeviceMappings__Name__FanPower</c>).
/// </summary>
public sealed class DeviceMapping : Dictionary<string, string>;
