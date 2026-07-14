namespace TuyaHub.Infrastructure.Profiles;

/// <summary>Resolves a <see cref="DeviceProfile"/> by its id (from a device's configured profile).</summary>
internal interface IDeviceProfileRegistry
{
    /// <summary>Returns the profile with the given id, throwing if it is not registered.</summary>
    DeviceProfile Get(string profileId);
}

internal sealed class DeviceProfileRegistry : IDeviceProfileRegistry
{
    private readonly Dictionary<string, DeviceProfile> _profiles;

    public DeviceProfileRegistry(IEnumerable<DeviceProfile> profiles)
        => _profiles = profiles.ToDictionary(p => p.ProfileId, StringComparer.OrdinalIgnoreCase);

    public DeviceProfile Get(string profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId) || _profiles.TryGetValue(profileId, out var profile) is false)
        {
            throw new InvalidOperationException(
                $"Unknown device profile '{profileId}'. Registered profiles: {string.Join(", ", _profiles.Keys)}.");
        }

        return profile;
    }
}
