using Microsoft.Extensions.Options;
using TuyaHub.Domain;
using TuyaHub.Domain.ValueObjects;

namespace TuyaHub.Infrastructure.Options;

/// <summary>
/// Config-backed <see cref="IDeviceRegistry"/>: builds one <see cref="Device"/> aggregate per
/// enabled device declared in <see cref="TuyaOptions"/>. Connection secrets (id / local key /
/// version) are validated here via their value objects so bad configuration fails fast at startup;
/// the transport settings themselves stay in the Tuya ACL's options rather than on the aggregate.
/// </summary>
internal sealed class ConfigurationDeviceRegistry : IDeviceRegistry
{
    private readonly Dictionary<DeviceName, Device> _devices;

    public ConfigurationDeviceRegistry(IOptions<TuyaOptions> options)
    {
        _devices = new Dictionary<DeviceName, Device>();

        foreach (var device in options.Value.Devices.Where(d => d.Enabled))
        {
            var name = DeviceName.Create(device.Name);

            // Validate connection secrets eagerly (throws on malformed configuration).
            _ = DeviceId.Create(device.DeviceId);
            _ = LocalKey.Create(device.LocalKey);
            _ = ProtocolVersion.Create(device.ProtocolVersion);

            if (!_devices.TryAdd(name, new Device(name)))
            {
                throw new InvalidOperationException($"Duplicate device name '{device.Name}' in configuration.");
            }
        }
    }

    public IReadOnlyCollection<Device> Devices => _devices.Values;

    public Device? Find(DeviceName name) => _devices.GetValueOrDefault(name);
}
