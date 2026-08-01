using Aula.Core.Models;

namespace Aula.Core.Tests;

public class RgbColorTests
{
    [Theory]
    [InlineData("#FF0000", 0xFF, 0x00, 0x00)]
    [InlineData("#00ff00", 0x00, 0xFF, 0x00)]
    [InlineData("0000FF", 0x00, 0x00, 0xFF)]
    [InlineData("#FFFFFF", 0xFF, 0xFF, 0xFF)]
    public void FromHex_Parses(string hex, byte r, byte g, byte b)
    {
        var color = RgbColor.FromHex(hex);
        Assert.Equal(new RgbColor(r, g, b), color);
    }

    [Theory]
    [InlineData("#FF00")]
    [InlineData("GGFF00")]
    [InlineData("12345")]
    [InlineData("")]
    public void FromHex_Throws_OnInvalid(string hex)
    {
        Assert.Throws<FormatException>(() => RgbColor.FromHex(hex));
    }

    [Fact]
    public void ToHex_RoundTrips()
    {
        var color = new RgbColor(0x12, 0xAB, 0xEF);
        Assert.Equal("#12ABEF", color.ToHex());
        Assert.Equal(color, RgbColor.FromHex(color.ToHex()));
    }

    [Fact]
    public void ToArray_ReturnsRgbBytes()
    {
        var color = new RgbColor(10, 20, 30);
        Assert.Equal(new byte[] { 10, 20, 30 }, color.ToArray());
    }
}
