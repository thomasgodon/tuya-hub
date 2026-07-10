namespace TuyaHub.Infrastructure.Options;

/// <summary>
/// Tuya-side settings: the poll cadence and the static list of devices. No LAN discovery — every
/// device is declared here.
/// </summary>
public sealed class TuyaOptions
{
    /// <summary>How often each device is polled (DP_QUERY) to catch changes made via the RF remote.</summary>
    public int PollIntervalSeconds { get; set; } = 10;

    /// <summary>How often a heartbeat is sent to keep the persistent socket alive (module drops idle sockets ~30 s).</summary>
    public int HeartbeatIntervalSeconds { get; set; } = 10;

    public List<TuyaDeviceOptions> Devices { get; set; } = [];
}

/// <summary>One statically-configured Wind Calm device.</summary>
public sealed class TuyaDeviceOptions
{
    /// <summary>Stable key that ties this device to its <see cref="DeviceMapping"/> (by name).</summary>
    public string Name { get; set; } = default!;

    public bool Enabled { get; set; } = true;

    public string IpAddress { get; set; } = default!;

    public string DeviceId { get; set; } = default!;

    public string LocalKey { get; set; } = default!;

    /// <summary>Tuya local protocol version; Wind Calm uses <c>3.3</c>.</summary>
    public string ProtocolVersion { get; set; } = "3.3";

    /// <summary>Tuya local port; virtually always 6668.</summary>
    public int Port { get; set; } = 6668;
}
