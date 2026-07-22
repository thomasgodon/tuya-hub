using TuyaHub.Infrastructure.Knx;
using Xunit;

namespace TuyaHub.Tests.Knx;

public class KnxDptTests
{
    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public void Bool_encodes_one_bit_group_value(bool value, byte expected)
    {
        var gv = KnxDpt.Bool(value);

        // DPT 1.001 must be a 1-bit "short" value — an 8-bit response is discarded by readers, so a
        // byte[]-backed GroupValue (SizeInBit 8) is exactly the bug this asserts against.
        Assert.Equal(1, gv.SizeInBit);
        Assert.Equal([expected], gv.Value);
    }

    [Theory]
    [InlineData(0, 0)]   // fan off
    [InlineData(1, 1)]
    [InlineData(6, 6)]   // top speed
    public void Count_encodes_verbatim_byte(int value, byte expected)
    {
        var gv = KnxDpt.Count(value);

        Assert.Equal(8, gv.SizeInBit);
        Assert.Equal([expected], gv.Value);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(100, 255)]
    [InlineData(50, 128)]   // round(50 * 255 / 100) = 127.5 -> 128 (away from zero)
    public void Percent_scales_to_255(int percent, byte expected)
    {
        var gv = KnxDpt.Percent(percent);

        Assert.Equal(8, gv.SizeInBit);
        Assert.Equal([expected], gv.Value);
    }

    [Fact]
    public void Percent_clamps_out_of_range()
    {
        Assert.Equal([(byte)0], KnxDpt.Percent(-10).Value);
        Assert.Equal([(byte)255], KnxDpt.Percent(150).Value);
    }

    [Theory]
    [InlineData(0, new byte[] { 0x00, 0x00 })]
    [InlineData(1, new byte[] { 0x00, 0x01 })]
    [InlineData(540, new byte[] { 0x02, 0x1C })]   // Wind Calm max timer, big-endian high byte first
    [InlineData(256, new byte[] { 0x01, 0x00 })]
    public void Minutes_encodes_big_endian_uint16(int minutes, byte[] expected)
    {
        var gv = KnxDpt.Minutes(minutes);

        Assert.Equal(16, gv.SizeInBit);
        Assert.Equal(expected, gv.Value);
    }
}
