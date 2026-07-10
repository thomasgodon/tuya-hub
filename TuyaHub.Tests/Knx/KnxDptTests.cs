using TuyaHub.Infrastructure.Knx;
using Xunit;

namespace TuyaHub.Tests.Knx;

public class KnxDptTests
{
    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public void Bool_encodes_single_byte(bool value, byte expected)
    {
        Assert.Equal([expected], KnxDpt.Bool(value));
    }

    [Theory]
    [InlineData(0, 0)]   // fan off
    [InlineData(1, 1)]
    [InlineData(6, 6)]   // top speed
    public void Count_encodes_verbatim_byte(int value, byte expected)
    {
        Assert.Equal([expected], KnxDpt.Count(value));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(100, 255)]
    [InlineData(50, 128)]   // round(50 * 255 / 100) = 127.5 -> 128 (away from zero)
    public void Percent_scales_to_255(int percent, byte expected)
    {
        Assert.Equal([expected], KnxDpt.Percent(percent));
    }

    [Fact]
    public void Percent_clamps_out_of_range()
    {
        Assert.Equal([(byte)0], KnxDpt.Percent(-10));
        Assert.Equal([(byte)255], KnxDpt.Percent(150));
    }

    [Theory]
    [InlineData(0, new byte[] { 0x00, 0x00 })]
    [InlineData(1, new byte[] { 0x00, 0x01 })]
    [InlineData(540, new byte[] { 0x02, 0x1C })]   // Wind Calm max timer, big-endian high byte first
    [InlineData(256, new byte[] { 0x01, 0x00 })]
    public void Minutes_encodes_big_endian_uint16(int minutes, byte[] expected)
    {
        Assert.Equal(expected, KnxDpt.Minutes(minutes));
    }
}
