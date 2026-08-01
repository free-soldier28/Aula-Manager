using System.Globalization;

namespace Aula.Core.Models;

public readonly record struct RgbColor(byte R, byte G, byte B)
{
    public static RgbColor FromHex(string hex)
    {
        string s = hex.TrimStart('#');
        if (s.Length != 6)
        {
            throw new FormatException($"Invalid color '{hex}'. Expected #RRGGBB.");
        }

        if (!byte.TryParse(s.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte r) ||
            !byte.TryParse(s.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte g) ||
            !byte.TryParse(s.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
        {
            throw new FormatException($"Invalid color '{hex}'. Expected #RRGGBB.");
        }

        return new RgbColor(r, g, b);
    }

    public static RgbColor FromRgb(int r, int g, int b) => new((byte)r, (byte)g, (byte)b);

    public string ToHex() => $"#{R:X2}{G:X2}{B:X2}";

    public byte[] ToArray() => new[] { R, G, B };
}
