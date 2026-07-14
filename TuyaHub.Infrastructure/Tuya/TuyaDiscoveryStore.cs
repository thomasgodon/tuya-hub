using System.Collections.Concurrent;

namespace TuyaHub.Infrastructure.Tuya;

/// <summary>
/// One Tuya device seen on the LAN via its UDP discovery beacon. Carries only what a beacon advertises
/// (no local key — that never appears in a beacon); enough for an operator to configure the device.
/// </summary>
internal sealed record DiscoveredTuyaDevice(
    string DeviceId,
    string IpAddress,
    string ProtocolVersion,
    string ProductKey,
    DateTimeOffset LastSeen);

/// <summary>
/// Thread-safe, in-memory inventory of Tuya devices discovered on the LAN, keyed by device id (gwId).
/// Populated from <see cref="TuyaDiscoveryService"/> (whose scanner raises events on background threads)
/// and read by the dashboard snapshot projection. Entries are pruned once their beacon stops arriving,
/// so the list reflects currently-reachable devices (UC-01). Holds no configuration and no secrets.
/// </summary>
internal sealed class TuyaDiscoveryStore
{
    private readonly ConcurrentDictionary<string, DiscoveredTuyaDevice> _devices = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Records a beacon. Always refreshes the last-seen stamp; returns <c>true</c> only when a visible
    /// field changed (a newly-seen device, or a changed IP / protocol version / product key), so the
    /// caller can republish just when the displayed list actually differs. <paramref name="now"/> is
    /// passed in so the store stays free of ambient time.
    /// </summary>
    public bool Upsert(string deviceId, string ipAddress, string protocolVersion, string productKey, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            return false;

        var entry = new DiscoveredTuyaDevice(deviceId, ipAddress ?? string.Empty, protocolVersion ?? string.Empty, productKey ?? string.Empty, now);

        var changed = false;
        _devices.AddOrUpdate(
            deviceId,
            _ => { changed = true; return entry; },
            (_, existing) =>
            {
                changed = existing.IpAddress != entry.IpAddress
                    || existing.ProtocolVersion != entry.ProtocolVersion
                    || existing.ProductKey != entry.ProductKey;
                return entry; // always adopt the fresh LastSeen
            });

        return changed;
    }

    /// <summary>Current discovered devices (a stable copy, safe to enumerate).</summary>
    public IReadOnlyCollection<DiscoveredTuyaDevice> Snapshot() => _devices.Values.ToArray();

    /// <summary>
    /// Drops entries whose last beacon is older than <paramref name="cutoff"/> (a device powered off or
    /// moved off the segment). Returns <c>true</c> when anything was removed so the caller can republish.
    /// </summary>
    public bool PruneOlderThan(DateTimeOffset cutoff)
    {
        var removed = false;
        foreach (var kvp in _devices)
        {
            if (kvp.Value.LastSeen < cutoff && _devices.TryRemove(kvp.Key, out _))
                removed = true;
        }

        return removed;
    }
}
