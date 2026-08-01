namespace Aula.Core.Models;

public static class EffectLibrary
{
    public static readonly IReadOnlyList<LedEffect> Default = new LedEffect[]
    {
        new(0, "off", HasSpeed: false, HasBrightness: false, HasColor: false),
        new(1, "static", HasSpeed: false, HasBrightness: true, HasColor: true),
        new(2, "breathing", HasSpeed: true, HasBrightness: true, HasColor: true),
        new(3, "wave", HasSpeed: true, HasBrightness: true, HasColor: false),
        new(4, "spectrum", HasSpeed: true, HasBrightness: true, HasColor: true),
        new(5, "ripple", HasSpeed: true, HasBrightness: true, HasColor: true),
        new(6, "reactive", HasSpeed: true, HasBrightness: true, HasColor: true),
        new(7, "starlight", HasSpeed: true, HasBrightness: true, HasColor: true),
        new(8, "rain", HasSpeed: true, HasBrightness: true, HasColor: true),
        new(9, "snake", HasSpeed: true, HasBrightness: true, HasColor: true),
        new(10, "marquee", HasSpeed: true, HasBrightness: true, HasColor: true),
        new(11, "aurora", HasSpeed: true, HasBrightness: true, HasColor: true),
        new(12, "laser", HasSpeed: true, HasBrightness: true, HasColor: true),
        new(13, "firework", HasSpeed: true, HasBrightness: true, HasColor: true),
        new(14, "gradient", HasSpeed: true, HasBrightness: true, HasColor: true),
        new(15, "rainbow_wave", HasSpeed: true, HasBrightness: true, HasColor: false),
        new(16, "prism", HasSpeed: true, HasBrightness: true, HasColor: false),
        new(17, "cycle", HasSpeed: true, HasBrightness: true, HasColor: false),
        new(18, "tidal", HasSpeed: true, HasBrightness: true, HasColor: true),
        new(21, "custom", HasSpeed: false, HasBrightness: false, HasColor: false),
    };

    public static LedEffect? FindById(int id) => Default.FirstOrDefault(e => e.Id == id);

    public static LedEffect? FindByName(string name) =>
        Default.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
}
