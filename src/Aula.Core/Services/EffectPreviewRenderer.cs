using Aula.Core.Abstractions;
using Aula.Core.Models;

namespace Aula.Core.Services;

public readonly record struct PreviewLed(int LedIndex, double X, double Y);

/// <summary>
/// Renders a software approximation of a hardware effect onto the virtual
/// keyboard so the user can preview an effect before applying it.
///
/// When <c>colorful</c> is off the effect is rendered in the picked single
/// color; when it is on a rainbow hue is used — mirroring the hardware which
/// draws the stored color profile in single-color mode and its internal
/// palette in colorful mode.
/// </summary>
public static class EffectPreviewRenderer
{
    public static IReadOnlyList<PreviewLed> BuildPositions(IKeyboardLayout layout)
    {
        var list = new List<PreviewLed>();
        for (int rowIndex = 0; rowIndex < layout.Rows.Count; rowIndex++)
        {
            double x = 0;
            foreach (KeyShape key in layout.Rows[rowIndex])
            {
                int index = layout.GetLedIndex(key.Name);
                if (index >= 0)
                {
                    list.Add(new PreviewLed(index, x, rowIndex));
                }

                x += key.Width + key.Gap;
            }
        }

        return list;
    }

    public static RgbColor[] Render(
        LedEffect effect,
        IReadOnlyList<PreviewLed> leds,
        double time,
        int brightness,
        int speed,
        RgbColor color,
        bool colorful)
    {
        if (leds.Count == 0)
        {
            return Array.Empty<RgbColor>();
        }

        int ledCount = leds.Max(l => l.LedIndex) + 1;
        var result = new RgbColor[ledCount];

        if (effect.Id == 0)
        {
            return result;
        }

        double m = 0.5 + 0.5 * Math.Clamp(speed, 0, 4);
        double t = time * m;
        double bf = Math.Clamp(brightness, 0, 9) / 9.0;
        if (bf <= 0)
        {
            bf = 0.04;
        }

        double maxX = leds.Max(l => l.X);
        double maxY = leds.Max(l => l.Y);
        double centerX = maxX / 2;
        double centerY = maxY / 2;

        for (int i = 0; i < leds.Count; i++)
        {
            PreviewLed led = leds[i];
            switch (effect.Id)
            {
                case 1: // static — solid color
                    result[led.LedIndex] = Scale(color, bf);
                    break;

                case 2: // breathing — period follows the speed slider (12s..3s)
                {
                    double period = 12 - 9 * Math.Clamp(speed, 0, 4) / 4.0;
                    double pulse = 0.3 + 0.7 * (0.5 + 0.5 * Math.Sin(2 * Math.PI * time / period));
                    result[led.LedIndex] = colorful
                        ? FromHsv(Frac(time * 0.1), 1, bf * pulse)
                        : Scale(color, bf * pulse);
                    break;
                }

                case 3: // wave — rainbow by nature
                case 15: // rainbow_wave
                case 17: // cycle
                {
                    double band = Frac(t * 0.6 + led.X * 0.08) < 0.35 ? 1.0 : 0.15;
                    result[led.LedIndex] = FromHsv(Frac(t * 0.6 + led.X * 0.08), 1, bf * band);
                    break;
                }

                case 4: // spectrum
                case 16: // prism
                {
                    double band = Frac(t * 0.4 + led.X * 0.04) < 0.35 ? 1.0 : 0.15;
                    result[led.LedIndex] = colorful
                        ? FromHsv(Frac(t * 0.4 + led.X * 0.04), 1, bf * band)
                        : Scale(color, bf * band);
                    break;
                }

                case 5: // rain
                {
                    double streak = Frac(led.Y * 0.35 + t * 1.2);
                    double f = streak < 0.18 ? 1 : 0.08;
                    result[led.LedIndex] = colorful
                        ? FromHsv(Frac(led.X * 0.05 + t * 0.2), 1, bf * f)
                        : Scale(color, bf * f);
                    break;
                }

                case 6: // color_shift
                    result[led.LedIndex] = colorful
                        ? FromHsv(Frac(t * 0.5), 1, bf)
                        : Scale(color, bf);
                    break;

                case 7: // ripple
                {
                    double dx = led.X - centerX;
                    double dy = (led.Y - maxY) * 2.5;
                    double dist = Math.Sqrt(dx * dx + dy * dy);
                    double radius = Frac(t * 0.8) * 4;
                    double glow = 1 - Math.Min(1, Math.Abs(dist - radius) / 0.6);
                    result[led.LedIndex] = colorful
                        ? FromHsv(Frac(dist * 0.15 + t * 0.3), 1, bf * (0.05 + 0.95 * glow))
                        : Scale(color, bf * (0.05 + 0.95 * glow));
                    break;
                }

                case 8: // starlight
                {
                    int step = (int)Math.Floor(t * 3);
                    double spark = Hash01(led.LedIndex, step);
                    result[led.LedIndex] = spark > 0.9
                        ? colorful
                            ? FromHsv(Hash01(led.LedIndex, step + 1), 1, bf)
                            : Scale(color, bf)
                        : Scale(color, bf * 0.06);
                    break;
                }

                case 9: // snake
                case 10: // marquee
                {
                    double pos = Frac(t * (effect.Id == 9 ? 0.5 : 0.8)) * (maxX + 8) - 4;
                    double f = Math.Abs(led.X - pos) < (effect.Id == 9 ? 3 : 2.5)
                        ? (effect.Id == 9 ? 0.9 : 1)
                        : (effect.Id == 9 ? 0.05 : 0.06);
                    result[led.LedIndex] = colorful
                        ? FromHsv(Frac(pos * 0.05), 1, bf * f)
                        : Scale(color, bf * f);
                    break;
                }

                case 11: // aurora
                    result[led.LedIndex] = colorful
                        ? FromHsv(Frac(t * 0.25 + led.Y * 0.12 + led.X * 0.02), 0.6, bf)
                        : Scale(color, bf * (0.4 + 0.6 * Frac(led.Y * 0.25 + t * 0.4)));
                    break;

                case 12: // laser
                {
                    int step = (int)Math.Floor(t * 4);
                    double flash = Hash01(led.LedIndex, step);
                    result[led.LedIndex] = flash > 0.8
                        ? colorful
                            ? FromHsv(Hash01(led.LedIndex, step + 1), 1, bf)
                            : Scale(color, bf)
                        : Scale(color, bf * 0.03);
                    break;
                }

                case 13: // firework
                {
                    double dx = (led.X - centerX) / 1.2;
                    double dy = (led.Y - centerY) * 3;
                    double dist = Math.Sqrt(dx * dx + dy * dy);
                    double radius = Frac(t * 0.6) * 6;
                    double glow = 1 - Math.Min(1, Math.Abs(dist - radius) / 0.5);
                    result[led.LedIndex] = colorful
                        ? FromHsv(Frac(radius * 0.8), 1, bf * (0.03 + 0.97 * glow))
                        : Scale(color, bf * (0.03 + 0.97 * glow));
                    break;
                }

                case 14: // gradient — single-color only, stays dark when colorful
                    result[led.LedIndex] = colorful
                        ? Scale(color, 0.03)
                        : Scale(color, bf * (0.15 + 0.85 * Frac(t * 0.3 + led.X / (maxX + 1))));
                    break;

                case 18: // tidal
                {
                    double f = Frac(t * 0.5 + led.Y * 0.25) < 0.25 ? 1 : 0.07;
                    result[led.LedIndex] = colorful
                        ? FromHsv(Frac(t * 0.7 + led.Y * 0.15), 1, bf * f)
                        : Scale(color, bf * f);
                    break;
                }

                case 21: // custom is painted by the user, not animated
                default:
                    break;
            }
        }

        return result;
    }

    private static double Frac(double x) => x - Math.Floor(x);

    private static RgbColor Scale(RgbColor c, double f)
    {
        f = Math.Clamp(f, 0, 1);
        return new RgbColor((byte)(c.R * f), (byte)(c.G * f), (byte)(c.B * f));
    }

    private static RgbColor FromHsv(double h, double s, double v)
    {
        h = Frac(h);
        double r, g, b;
        int i = (int)(h * 6);
        double f = h * 6 - i;
        double p = v * (1 - s);
        double q = v * (1 - f * s);
        double t = v * (1 - (1 - f) * s);
        switch (i % 6)
        {
            case 0: r = v; g = t; b = p; break;
            case 1: r = q; g = v; b = p; break;
            case 2: r = p; g = v; b = t; break;
            case 3: r = p; g = q; b = v; break;
            case 4: r = t; g = p; b = v; break;
            default: r = v; g = p; b = q; break;
        }

        return new RgbColor((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }

    private static double Hash01(int seed, int salt)
    {
        uint x = (uint)(seed * 2654435761u) + (uint)salt;
        x ^= x >> 16;
        x *= 0x7feb352d;
        x ^= x >> 15;
        x *= 0x846ca68b;
        x ^= x >> 16;
        return x / (double)uint.MaxValue;
    }
}
