using Aula.Core.Models;

namespace Aula.Core.Tests;

public class KeyboardConfigTests
{
    [Fact]
    public void Parse_ReadsKnownFields()
    {
        var config = KeyboardConfig.Parse(F75ReportTests.BuildConfigResponse(), ModelConfig.F75);

        Assert.Equal(10, config.EffectId);
        Assert.False(config.CustomMode);
        Assert.Equal(1, config.SideLightEffect);
        Assert.Equal(0, config.BatteryLightEffect);
    }

    [Fact]
    public void GetParams_ReadsPerEffectBrightnessAndSpeed()
    {
        var config = KeyboardConfig.Parse(F75ReportTests.BuildConfigResponse(), ModelConfig.F75);

        EffectParams? marquee = config.GetParams(10);
        Assert.NotNull(marquee);
        Assert.Equal(3, marquee.Value.Brightness);
        Assert.Equal(4, marquee.Value.Speed);
        Assert.True(marquee.Value.Colorful);

        EffectParams? staticFx = config.GetParams(1);
        Assert.NotNull(staticFx);
        Assert.Equal(4, staticFx.Value.Brightness);
        Assert.Equal(0, staticFx.Value.Speed);
    }

    [Fact]
    public void GetParams_ReturnsNull_ForOutOfRange()
    {
        var config = KeyboardConfig.Parse(F75ReportTests.BuildConfigResponse(), ModelConfig.F75);

        Assert.Null(config.GetParams(0));
        Assert.Null(config.GetParams(99));
    }

    [Fact]
    public void Parse_Throws_OnShortResponse()
    {
        Assert.Throws<AulaProtocolException>(() => KeyboardConfig.Parse(new byte[10], ModelConfig.F75));
    }

    [Fact]
    public void Parse_Throws_OnBadHeader()
    {
        var response = F75ReportTests.BuildConfigResponse();
        response[1] = 0x99;

        Assert.Throws<AulaProtocolException>(() => KeyboardConfig.Parse(response, ModelConfig.F75));
    }
}
