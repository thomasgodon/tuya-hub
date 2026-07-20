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

    /// <summary>
    /// Liveness watchdog (M5): if no inbound bytes (STATUS push, poll reply, or heartbeat ack) arrive
    /// within this window, the connection is treated as dead and force-reconnected. Must exceed
    /// <see cref="HeartbeatIntervalSeconds"/>; ≈ the module's ~30 s idle-drop.
    /// </summary>
    public int LivenessTimeoutSeconds { get; set; } = 30;

    /// <summary>TCP connect timeout per attempt.</summary>
    public int ConnectTimeoutSeconds { get; set; } = 5;

    /// <summary>Reconnect backoff: first (and post-success) delay before retrying a dropped device.</summary>
    public int ReconnectInitialBackoffSeconds { get; set; } = 1;

    /// <summary>Reconnect backoff: upper cap the exponential delay doubles toward.</summary>
    public int ReconnectMaxBackoffSeconds { get; set; } = 30;

    public List<TuyaDeviceOptions> Devices { get; set; } = [];
}

/// <summary>One statically-configured device.</summary>
public sealed class TuyaDeviceOptions
{
    /// <summary>Stable key that ties this device to its <see cref="DeviceMapping"/> (by name).</summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// Device type / profile id (see the profile registry). Defaults to <c>wind-calm</c> so existing
    /// configurations that omit it keep working; set it to a different profile to add another device type.
    /// </summary>
    public string Profile { get; set; } = "wind-calm";

    public bool Enabled { get; set; } = true;

    public string IpAddress { get; set; } = default!;

    public string DeviceId { get; set; } = default!;

    public string LocalKey { get; set; } = default!;

    /// <summary>Tuya local protocol version; Wind Calm uses <c>3.3</c>.</summary>
    public string ProtocolVersion { get; set; } = "3.3";

    /// <summary>Tuya local port; virtually always 6668.</summary>
    public int Port { get; set; } = 6668;
}
