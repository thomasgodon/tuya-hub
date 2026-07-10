using Knx.Falcon;

namespace TuyaHub.Infrastructure.Knx;

/// <summary>
/// One status group address plus its last-known raw value. Ported from DsmrHub's
/// <c>KnxTelegramValue</c>: the cached <see cref="Value"/> is both the source for KNX read responses
/// (FR-7) and the reference for the redundant-write guard (FR-8).
/// </summary>
internal sealed class KnxStatusValue(GroupAddress address)
{
    public GroupAddress Address { get; } = address;

    public byte[]? Value { get; internal set; }

    public override string ToString()
    {
        var value = Value is not null ? string.Join(",", Value) : string.Empty;
        return $"{Address} - {value}";
    }
}
