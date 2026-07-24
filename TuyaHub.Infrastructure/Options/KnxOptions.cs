namespace TuyaHub.Infrastructure.Options;

/// <summary>
/// KNXnet/IP connection settings. Mirrors DsmrHub: <see cref="Enabled"/> false disables the bus
/// entirely, and <see cref="IndividualAddress"/> is kept as a string so an empty/unset value does
/// not fail options binding — it is parsed lazily when connecting.
/// </summary>
public sealed class KnxOptions
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = default!;
    public int Port { get; set; } = 3671;
    public string IndividualAddress { get; set; } = default!;

    /// <summary>
    /// KNXnet/IP NAT mode. When true, the tunnel advertises an empty local endpoint (HPAI 0.0.0.0) so
    /// the gateway routes its replies — the connection-state heartbeat <b>and</b> inbound group telegrams —
    /// back to the actual UDP source instead of a local IP the client claims. Required when running behind
    /// NAT or on a <b>multi-homed / containerised host</b> (e.g. Docker with host networking on a box that
    /// also has docker0/bridge/VPN interfaces), where Falcon can otherwise advertise the wrong local IP:
    /// the handshake still completes (so the bus logs <c>Connected</c>) but heartbeats and inbound commands
    /// go to an unreachable address, so the bus flaps (repeated connect/reconnect) and KNX→Tuya commands
    /// never arrive. Default false — direct single-homed hosts (e.g. a Windows dev box) don't need it.
    /// </summary>
    public bool UseNat { get; set; }

    /// <summary>
    /// Tunnelling transport: <c>Auto</c> (default), <c>Udp</c>, or <c>Tcp</c>. <c>Tcp</c> (KNXnet/IP v2
    /// tunnelling) is connection-oriented and has no HPAI return-path problem at all, so it's the most
    /// robust option in Docker — but only when the gateway supports it. Parsed case-insensitively;
    /// an unset/unrecognised value falls back to <c>Auto</c>.
    /// </summary>
    public string Protocol { get; set; } = "Auto";

    /// <summary>Reconnect backoff (M5): first (and post-success) delay before re-opening a dropped bus.</summary>
    public int ReconnectInitialBackoffSeconds { get; set; } = 1;

    /// <summary>Reconnect backoff (M5): upper cap the exponential delay doubles toward.</summary>
    public int ReconnectMaxBackoffSeconds { get; set; } = 30;
}
