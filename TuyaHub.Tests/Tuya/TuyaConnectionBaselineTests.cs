using TuyaHub.Infrastructure.Tuya;
using Xunit;

namespace TuyaHub.Tests.Tuya;

/// <summary>
/// The connect-time baseline reconcile: <see cref="TuyaConnection.ComputeBaselineWrites"/> writes only the
/// DPs whose current device value differs from the desired baseline, so an already-correct DP (e.g. a
/// buzzer already off) is never re-written — some firmware answers every DP write with a confirmation beep.
/// </summary>
public class TuyaConnectionBaselineTests
{
    private static readonly Dictionary<string, object> BuzzerOff = new() { ["66"] = false };

    [Fact]
    public void No_write_when_the_device_already_matches_the_baseline()
    {
        // DP 66 already false (Wind Calm 3.5 persists this) → nothing to write → no beep.
        var current = new Dictionary<int, object> { [20] = false, [66] = false };

        var writes = TuyaConnection.ComputeBaselineWrites(BuzzerOff, current);

        Assert.Empty(writes);
    }

    [Fact]
    public void Writes_only_the_differing_dp_when_the_state_is_wrong()
    {
        // Buzzer currently on → write {66:false} once to correct it (the single, intended beep).
        var current = new Dictionary<int, object> { [20] = false, [66] = true };

        var writes = TuyaConnection.ComputeBaselineWrites(BuzzerOff, current);

        Assert.Equal(false, Assert.Contains("66", writes));
        Assert.Single(writes);
    }

    [Fact]
    public void Skips_a_dp_the_device_does_not_report()
    {
        // 3.4 XW-FAN-215-D never reports DP 66 → can't confirm it needs changing → skip (was a no-op anyway).
        var current = new Dictionary<int, object> { [20] = false, [60] = false, [62] = 1 };

        var writes = TuyaConnection.ComputeBaselineWrites(BuzzerOff, current);

        Assert.Empty(writes);
    }
}
