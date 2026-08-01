using Aula.Core.Abstractions;
using Aula.Core.Services;

namespace Aula.Core.Models;

public sealed record KeyboardProfile(
    string Name,
    LightingConfig Lighting,
    IReadOnlyDictionary<string, RgbColor>? KeyColors = null,
    string? Model = null)
{
    public static KeyboardProfile FromCurrent(string name, IAulaKeyboard keyboard)
    {
        var config = keyboard.Lighting.ReadConfig();
        var lighting = new LightingConfig(
            EffectId: config.EffectId,
            Brightness: config.GetParams(config.EffectId)?.Brightness,
            Speed: config.GetParams(config.EffectId)?.Speed,
            Colorful: config.GetParams(config.EffectId)?.Colorful ?? false);

        return new KeyboardProfile(name, lighting, Model: keyboard.Model.Id);
    }
}
