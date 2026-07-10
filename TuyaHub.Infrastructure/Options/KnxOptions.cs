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
}
