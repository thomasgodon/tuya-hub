using TuyaHub.Domain.ValueObjects;
using Xunit;

namespace TuyaHub.Tests.Domain;

public class SpeedLevelTests
{
    [Theory]
    [InlineData(0, 0)]     // off
    [InlineData(1, 17)]
    [InlineData(2, 33)]
    [InlineData(3, 50)]
    [InlineData(4, 67)]
    [InlineData(5, 83)]
    [InlineData(6, 100)]
    public void ToPercent_maps_status_level_to_percentage(int level, int expectedPercent)
        => Assert.Equal(expectedPercent, SpeedLevel.ToPercent(level));

    [Theory]
    [InlineData(1, 1)]
    [InlineData(16, 1)]
    [InlineData(17, 2)]
    [InlineData(33, 2)]
    [InlineData(34, 3)]
    [InlineData(50, 3)]
    [InlineData(51, 4)]
    [InlineData(66, 4)]
    [InlineData(67, 5)]
    [InlineData(83, 5)]
    [InlineData(84, 6)]
    [InlineData(100, 6)]
    [InlineData(200, 6)]   // clamps above 100
    public void FromPercent_maps_percentage_into_the_six_bands(int percent, int expectedLevel)
        => Assert.Equal(SpeedLevel.Create(expectedLevel), SpeedLevel.FromPercent(percent));
}
