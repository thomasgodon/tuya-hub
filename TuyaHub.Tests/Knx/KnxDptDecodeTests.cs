using TuyaHub.Infrastructure.Knx;
using Xunit;

namespace TuyaHub.Tests.Knx;

public class KnxDptDecodeTests
{
    [Theory]
    [InlineData(0x00, false)]
    [InlineData(0x01, true)]
    public void DecodeBool_reads_low_bit(byte payload, bool expected)
    {
        Assert.Equal(expected, KnxDpt.DecodeBool([payload]));
    }

    [Fact]
    public void DecodeBool_empty_payload_is_false()
    {
        Assert.False(KnxDpt.DecodeBool([]));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Bool_round_trips(bool value)
    {
        Assert.Equal(value, KnxDpt.DecodeBool(KnxDpt.Bool(value).Value));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(100, 100)]
    [InlineData(50, 50)]   // encode 50 -> 128, decode 128 -> round(50.19) = 50
    public void Percent_round_trips(int percent, int expected)
    {
        Assert.Equal(expected, KnxDpt.DecodePercent(KnxDpt.Percent(percent).Value));
    }

    [Fact]
    public void DecodePercent_empty_payload_is_zero()
    {
        Assert.Equal(0, KnxDpt.DecodePercent([]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(256)]
    [InlineData(540)]   // Wind Calm max timer
    public void Minutes_round_trips(int minutes)
    {
        Assert.Equal(minutes, KnxDpt.DecodeMinutes(KnxDpt.Minutes(minutes).Value));
    }

    [Fact]
    public void DecodeMinutes_short_payload_is_zero()
    {
        Assert.Equal(0, KnxDpt.DecodeMinutes([0x01]));
    }

    [Theory]
    [InlineData(0x09, true)]    // up, step 1
    [InlineData(0x0F, true)]    // up, step 7 (step width ignored — still a single +1)
    [InlineData(0x01, false)]   // down, step 1
    [InlineData(0x05, false)]   // down, step 5
    public void DecodeDimStep_reads_direction(byte payload, bool expectedUp)
    {
        Assert.Equal(expectedUp, KnxDpt.DecodeDimStep([payload]));
    }

    [Theory]
    [InlineData(0x00)]   // down break
    [InlineData(0x08)]   // up break (direction bit set, step code 0)
    public void DecodeDimStep_break_is_null(byte payload)
    {
        Assert.Null(KnxDpt.DecodeDimStep([payload]));
    }

    [Fact]
    public void DecodeDimStep_empty_payload_is_null()
    {
        Assert.Null(KnxDpt.DecodeDimStep([]));
    }
}
