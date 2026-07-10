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

    /// <summary>Reconnect backoff (M5): first (and post-success) delay before re-opening a dropped bus.</summary>
    public int ReconnectInitialBackoffSeconds { get; set; } = 1;

    /// <summary>Reconnect backoff (M5): upper cap the exponential delay doubles toward.</summary>
    public int ReconnectMaxBackoffSeconds { get; set; } = 30;
}
