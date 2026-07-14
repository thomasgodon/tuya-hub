using TuyaHub.Infrastructure.Tuya;
using Xunit;

namespace TuyaHub.Tests.Tuya;

public class TuyaDiscoveryStoreTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Upsert_a_new_device_reports_a_change_and_appears_in_the_snapshot()
    {
        var store = new TuyaDiscoveryStore();

        var changed = store.Upsert("bf123", "192.168.0.50", "3.3", "keyabc", T0);

        Assert.True(changed);
        var device = Assert.Single(store.Snapshot());
        Assert.Equal("bf123", device.DeviceId);
        Assert.Equal("192.168.0.50", device.IpAddress);
        Assert.Equal("3.3", device.ProtocolVersion);
        Assert.Equal("keyabc", device.ProductKey);
    }

    [Fact]
    public void Re_seeing_a_device_with_identical_fields_reports_no_change()
    {
        var store = new TuyaDiscoveryStore();
        store.Upsert("bf123", "192.168.0.50", "3.3", "keyabc", T0);

        var changed = store.Upsert("bf123", "192.168.0.50", "3.3", "keyabc", T0.AddSeconds(5));

        Assert.False(changed);
        Assert.Single(store.Snapshot());
    }

    [Fact]
    public void A_changed_ip_reports_a_change()
    {
        var store = new TuyaDiscoveryStore();
        store.Upsert("bf123", "192.168.0.50", "3.3", "keyabc", T0);

        var changed = store.Upsert("bf123", "192.168.0.99", "3.3", "keyabc", T0.AddSeconds(5));

        Assert.True(changed);
        Assert.Equal("192.168.0.99", Assert.Single(store.Snapshot()).IpAddress);
    }

    [Fact]
    public void Upsert_ignores_a_blank_device_id()
    {
        var store = new TuyaDiscoveryStore();

        var changed = store.Upsert("  ", "192.168.0.50", "3.3", "keyabc", T0);

        Assert.False(changed);
        Assert.Empty(store.Snapshot());
    }

    [Fact]
    public void PruneOlderThan_drops_stale_entries_and_keeps_fresh_ones()
    {
        var store = new TuyaDiscoveryStore();
        store.Upsert("stale", "192.168.0.10", "3.3", "", T0);
        store.Upsert("fresh", "192.168.0.20", "3.3", "", T0.AddSeconds(60));

        var removed = store.PruneOlderThan(T0.AddSeconds(30));

        Assert.True(removed);
        Assert.Equal("fresh", Assert.Single(store.Snapshot()).DeviceId);
    }

    [Fact]
    public void PruneOlderThan_reports_no_removal_when_all_entries_are_fresh()
    {
        var store = new TuyaDiscoveryStore();
        store.Upsert("fresh", "192.168.0.20", "3.3", "", T0.AddSeconds(60));

        Assert.False(store.PruneOlderThan(T0.AddSeconds(30)));
        Assert.Single(store.Snapshot());
    }
}
