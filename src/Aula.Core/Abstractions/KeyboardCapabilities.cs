namespace Aula.Core.Abstractions;

public sealed record KeyboardCapabilities(
    bool HasLighting = true,
    bool HasPerKeyRgb = false,
    bool HasKeyRemap = false,
    bool HasWireless = false,
    bool HasScreen = false);
