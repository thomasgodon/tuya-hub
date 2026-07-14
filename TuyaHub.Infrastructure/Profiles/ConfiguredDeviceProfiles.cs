using Microsoft.Extensions.Options;
using TuyaHub.Domain.ValueObjects;
using TuyaHub.Infrastructure.Options;

namespace TuyaHub.Infrastructure.Profiles;

/// <summary>
/// Resolves the <see cref="DeviceProfile"/> for a configured device by name — the association the KNX
/// ACL needs to map each device's capabilities to its group addresses and encode its status. Built once
/// from <see cref="TuyaOptions"/> (all declared devices, enabled or not, so their mappings still wire).
/// An unknown name falls back to the historical default profile, matching the pre-refactor behaviour
/// where any device mapping was treated as Wind Calm.
/// </summary>
internal sealed class ConfiguredDeviceProfiles
{
    private readonly Dictionary<DeviceName, DeviceProfile> _byDevice = new();
    private readonly DeviceProfile _default;

    public ConfiguredDeviceProfiles(IOptions<TuyaOptions> options, IDeviceProfileRegistry profiles)
    {
        _default = profiles.Get(WindCalmProfile.ProfileId);

        foreach (var device in options.Value.Devices)
        {
            _byDevice[DeviceName.Create(device.Name)] = profiles.Get(device.Profile);
        }
    }

    public DeviceProfile For(DeviceName device) => _byDevice.GetValueOrDefault(device) ?? _default;
}
