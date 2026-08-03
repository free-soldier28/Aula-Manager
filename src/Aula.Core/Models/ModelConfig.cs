using Aula.Core.Abstractions;

namespace Aula.Core.Models;

public sealed record ModelConfig(
    string Id,
    int VendorId,
    int ProductId,
    byte ReportId,
    int ReportLength,
    int ConfigHeaderLength,
    int ConfigResponseLength,
    int EffectIdOffset,
    int CustomModeOffset,
    int SideLightOffset,
    int BatteryLightOffset,
    int EffectParamsBase,
    int EffectParamsStride,
    IReadOnlyList<LedEffect> Effects,
    IKeyboardLayout Layout)
{
    public static readonly ModelConfig F75 = new(
        "f75",
        VendorId: 0x258A,
        ProductId: 0x010C,
        ReportId: 0x06,
        ReportLength: 520,
        ConfigHeaderLength: 8,
        ConfigResponseLength: 136,
        EffectIdOffset: 18,
        CustomModeOffset: 17,
        SideLightOffset: 26,
        BatteryLightOffset: 36,
        EffectParamsBase: 64,
        EffectParamsStride: 2,
        EffectLibrary.Default,
        Layout: F75Layout.Instance);

    public static readonly ModelConfig F87 = F75 with { Id = "f87" };

    public static readonly IReadOnlyDictionary<string, ModelConfig> Known = new Dictionary<string, ModelConfig>
    {
        [F75.Id] = F75,
        [F87.Id] = F87,
    };

    public static ModelConfig Resolve(string? id) =>
        string.IsNullOrWhiteSpace(id) ? F75 : (Known.TryGetValue(id, out var m) ? m : F75);
}
