using Microsoft.Extensions.Options;
using TuyaHub.Domain;
using TuyaHub.Domain.ValueObjects;
using TuyaHub.Infrastructure.Profiles;

namespace TuyaHub.Infrastructure.Options;

/// <summary>
/// Config-backed <see cref="IDeviceRegistry"/>: builds one aggregate per enabled device declared in
/// <see cref="TuyaOptions"/>, via its configured <see cref="DeviceProfile"/> factory. Connection secrets
/// (id / local key / version) are validated here via their value objects so bad configuration fails fast
/// at startup; the transport settings themselves stay in the Tuya ACL's options rather than on the aggregate.
/// </summary>
internal sealed class ConfigurationDeviceRegistry : IDeviceRegistry
{
    private readonly Dictionary<DeviceName, IDevice> _devices;

    public ConfigurationDeviceRegistry(IOptions<TuyaOptions> options, IDeviceProfileRegistry profiles)
    {
        _devices = new Dictionary<DeviceName, IDevice>();

        foreach (var device in options.Value.Devices.Where(d => d.Enabled))
        {
            var name = DeviceName.Create(device.Name);

            // Validate connection secrets eagerly (throws on malformed configuration).
            _ = DeviceId.Create(device.DeviceId);
            _ = LocalKey.Create(device.LocalKey);
            _ = ProtocolVersion.Create(device.ProtocolVersion);

            var profile = profiles.Get(device.Profile);

            if (!_devices.TryAdd(name, profile.CreateAggregate(name)))
            {
                throw new InvalidOperationException($"Duplicate device name '{device.Name}' in configuration.");
            }
        }
    }

    public IReadOnlyCollection<IDevice> Devices => _devices.Values;

    public IDevice? Find(DeviceName name) => _devices.GetValueOrDefault(name);
}
