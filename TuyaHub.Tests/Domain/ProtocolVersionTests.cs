using TuyaHub.Domain.ValueObjects;
using Xunit;

namespace TuyaHub.Tests.Domain;

public class ProtocolVersionTests
{
    [Theory]
    [InlineData("3.1")]
    [InlineData("3.3")]
    [InlineData("3.4")]
    [InlineData("3.5")]
    public void Create_accepts_supported_versions(string value)
    {
        Assert.Equal(value, ProtocolVersion.Create(value).Value);
    }

    [Fact]
    public void Create_trims_whitespace()
    {
        Assert.Equal("3.4", ProtocolVersion.Create("  3.4 ").Value);
    }

    [Theory]
    [InlineData("3.2")]
    [InlineData("3.6")]
    [InlineData("")]
    [InlineData("nonsense")]
    public void Create_rejects_unsupported_versions(string value)
    {
        Assert.Throws<ArgumentException>(() => ProtocolVersion.Create(value));
    }

    [Theory]
    [InlineData("3.1", false)]
    [InlineData("3.3", false)]
    [InlineData("3.4", true)]
    [InlineData("3.5", true)]
    public void RequiresSessionHandshake_is_true_only_for_34_and_35(string value, bool expected)
    {
        Assert.Equal(expected, ProtocolVersion.Create(value).RequiresSessionHandshake);
    }
}
