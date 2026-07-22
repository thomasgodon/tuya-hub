using Knx.Falcon;

namespace TuyaHub.Infrastructure.Knx;

/// <summary>
/// One status group address plus its last-known <see cref="GroupValue"/>. Ported from DsmrHub's
/// <c>KnxTelegramValue</c>: the cached <see cref="Value"/> is both the source for KNX read responses
/// (FR-7) and the reference for the redundant-write guard (FR-8). Holding a <see cref="GroupValue"/>
/// (not a raw <c>byte[]</c>) preserves the DPT bit size, so a 1-bit boolean is answered as a 1-bit
/// "short" value rather than a malformed 8-bit response.
/// </summary>
internal sealed class KnxStatusValue(GroupAddress address)
{
    public GroupAddress Address { get; } = address;

    public GroupValue? Value { get; internal set; }

    public override string ToString() => $"{Address} - {Value?.ToString() ?? string.Empty}";
}
