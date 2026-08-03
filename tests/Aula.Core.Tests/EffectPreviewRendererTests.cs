using Aula.Core.Abstractions;
using Aula.Core.Models;
using Aula.Core.Services;

namespace Aula.Core.Tests;

public class EffectPreviewRendererTests
{
    private static readonly IReadOnlyList<PreviewLed> Leds = EffectPreviewRenderer.BuildPositions(F75Layout.Instance);

    private static RgbColor[] Render(int effectId, int brightness = 5, int speed = 2, RgbColor color = default, bool colorful = false, double time = 0.5) =>
        EffectPreviewRenderer.Render(
            EffectLibrary.FindById(effectId)!,
            Leds,
            time,
            brightness,
            speed,
            color == default ? new RgbColor(255, 0, 0) : color,
            colorful);

    [Fact]
    public void BuildPositions_ReturnsAllVisualKeys()
    {
        int visualKeys = F75Layout.Instance.Rows.Sum(r => r.Count);
        Assert.Equal(visualKeys, Leds.Count);
    }

    [Fact]
    public void BuildPositions_IndicesAreNonNegativeAndUnique()
    {
        Assert.All(Leds, led => Assert.True(led.LedIndex >= 0));
        Assert.Equal(Leds.Count, Leds.Select(l => l.LedIndex).Distinct().Count());
    }

    [Fact]
    public void Render_Off_ReturnsAllBlack()
    {
        RgbColor[] result = Render(0);

        Assert.All(result, c => Assert.Equal(default, c));
    }

    [Fact]
    public void Render_Static_ReturnsScaledColor()
    {
        RgbColor[] result = Render(1, brightness: 9);

        foreach (PreviewLed led in Leds)
        {
            Assert.Equal(255, result[led.LedIndex].R);
            Assert.Equal(0, result[led.LedIndex].G);
            Assert.Equal(0, result[led.LedIndex].B);
        }
    }

    [Fact]
    public void Render_Static_ScalesWithBrightness()
    {
        RgbColor[] low = Render(1, brightness: 0);
        RgbColor[] high = Render(1, brightness: 9);

        foreach (PreviewLed led in Leds)
        {
            Assert.True(low[led.LedIndex].R < high[led.LedIndex].R);
        }
    }

    [Fact]
    public void Render_ResultCoversEveryLedPosition()
    {
        RgbColor[] result = Render(6);

        Assert.All(Leds, led => Assert.True(led.LedIndex < result.Length));
    }

    [Theory]
    [InlineData(2)] // breathing
    [InlineData(3)] // wave
    [InlineData(4)] // spectrum
    [InlineData(5)] // rain
    [InlineData(6)] // color_shift
    [InlineData(7)] // ripple
    [InlineData(8)] // starlight
    [InlineData(9)] // snake
    [InlineData(10)] // marquee
    [InlineData(11)] // aurora
    [InlineData(12)] // reactive
    [InlineData(13)] // firework
    [InlineData(14)] // gradient
    [InlineData(15)] // rainbow_wave
    [InlineData(16)] // prism
    [InlineData(17)] // cycle
    [InlineData(18)] // tidal
    [InlineData(21)] // custom
    public void Render_KnownEffectIds_ProduceOutput(int effectId)
    {
        RgbColor[] result = Render(effectId);

        Assert.All(Leds, led => Assert.True(led.LedIndex < result.Length));
    }

    [Fact]
    public void Render_AnimatedEffects_ChangeOverTime()
    {
        RgbColor[] first = Render(2, time: 0.0);
        RgbColor[] second = Render(2, time: 0.5);

        Assert.True(ColorsDiffer(first, second));
    }

    [Fact]
    public void Render_CustomIsNotAnimated()
    {
        RgbColor[] a = Render(21, time: 0.0);
        RgbColor[] b = Render(21, time: 2.0);

        Assert.Equal(a, b);
    }

    [Theory]
    [InlineData(1)] // static
    [InlineData(2)] // breathing
    [InlineData(5)] // rain
    [InlineData(6)] // color_shift
    [InlineData(7)] // ripple
    [InlineData(8)] // starlight
    [InlineData(9)] // snake
    [InlineData(10)] // marquee
    [InlineData(11)] // aurora
    [InlineData(12)] // laser
    [InlineData(13)] // firework
    [InlineData(14)] // gradient
    [InlineData(18)] // tidal
    public void Render_SingleColorMode_UsesPickedColor(int effectId)
    {
        RgbColor[] result = Render(effectId, color: new RgbColor(255, 0, 0), colorful: false);

        foreach (PreviewLed led in Leds)
        {
            Assert.Equal(0, result[led.LedIndex].G);
            Assert.Equal(0, result[led.LedIndex].B);
        }
    }

    [Fact]
    public void Render_ColorfulMode_ShiftsHueOverTime()
    {
        RgbColor[] first = Render(6, colorful: true, time: 0.0);
        RgbColor[] second = Render(6, colorful: true, time: 1.0);

        Assert.True(ColorsDiffer(first, second));
    }

    [Fact]
    public void Render_RainbowEffects_UseHuesEvenWithoutColorful()
    {
        RgbColor[] result = Render(15, color: new RgbColor(0, 255, 0), colorful: false);

        bool allGreen = true;
        foreach (PreviewLed led in Leds)
        {
            if (result[led.LedIndex] != new RgbColor(0, 255, 0))
            {
                allGreen = false;
                break;
            }
        }

        Assert.False(allGreen);
    }

    private static bool ColorsDiffer(RgbColor[] a, RgbColor[] b)
    {
        for (int i = 0; i < Math.Min(a.Length, b.Length); i++)
        {
            if (a[i] != b[i])
            {
                return true;
            }
        }

        return false;
    }
}
