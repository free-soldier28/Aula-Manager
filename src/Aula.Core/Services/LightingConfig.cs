using Aula.Core.Models;

namespace Aula.Core.Services;

public sealed record LightingConfig(
    int EffectId,
    int? Brightness = null,
    int? Speed = null,
    RgbColor? Color = null,
    bool Colorful = false,
    byte? RawFlags = null)
{
    public static LightingConfig Off => new(EffectId: 0);
}
