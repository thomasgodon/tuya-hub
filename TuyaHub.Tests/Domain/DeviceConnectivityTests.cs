using TuyaHub.Domain;
using TuyaHub.Domain.Events;
using TuyaHub.Domain.ValueObjects;
using Xunit;

namespace TuyaHub.Tests.Domain;

public class DeviceConnectivityTests
{
    private static Device NewDevice() => new(DeviceName.Create("Fan"));

    [Fact]
    public void A_new_device_starts_offline()
    {
        Assert.False(NewDevice().IsOnline);
    }

    [Fact]
    public void MarkReconnected_transitions_online_and_emits_the_event_once()
    {
        var device = NewDevice();

        var first = device.MarkReconnected();
        Assert.True(device.IsOnline);
        var evt = Assert.Single(first);
        var reconnected = Assert.IsType<DeviceReconnected>(evt);
        Assert.Equal(device.Name, reconnected.Device);

        // Idempotent: already online → no further event.
        Assert.Empty(device.MarkReconnected());
        Assert.True(device.IsOnline);
    }

    [Fact]
    public void MarkOffline_transitions_offline_and_emits_the_event_once()
    {
        var device = NewDevice();
        device.MarkReconnected(); // go online first

        var first = device.MarkOffline();
        Assert.False(device.IsOnline);
        var evt = Assert.Single(first);
        var offline = Assert.IsType<DeviceWentOffline>(evt);
        Assert.Equal(device.Name, offline.Device);

        // Idempotent: already offline → no further event.
        Assert.Empty(device.MarkOffline());
        Assert.False(device.IsOnline);
    }

    [Fact]
    public void MarkOffline_on_a_never_online_device_is_a_no_op()
    {
        var device = NewDevice();

        Assert.Empty(device.MarkOffline());
        Assert.False(device.IsOnline);
    }

    [Fact]
    public void Transitions_alternate_and_emit_each_time()
    {
        var device = NewDevice();

        Assert.IsType<DeviceReconnected>(Assert.Single(device.MarkReconnected()));
        Assert.IsType<DeviceWentOffline>(Assert.Single(device.MarkOffline()));
        Assert.IsType<DeviceReconnected>(Assert.Single(device.MarkReconnected()));
        Assert.IsType<DeviceWentOffline>(Assert.Single(device.MarkOffline()));
    }
}
