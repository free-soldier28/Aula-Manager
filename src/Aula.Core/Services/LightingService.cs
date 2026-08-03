using Aula.Core.Abstractions;
using Aula.Core.Models;
using Aula.Core.Protocol;

namespace Aula.Core.Services;

public sealed class LightingService : ILightingController
{
    private const int MaxBrightness = 9;
    private const int MaxSpeed = 4;
    private const byte ColorfulFlag = 0x07;
    private const byte SingleColorFlag = 0x00;

    private readonly SinowealthProtocol _protocol;
    private readonly ModelConfig _model;

    public LightingService(SinowealthProtocol protocol)
    {
        _protocol = protocol;
        _model = protocol.Model;
    }

    public KeyboardConfig ReadConfig() => KeyboardConfig.Parse(_protocol.ReadConfigRaw(), _model);

    public void Apply(LightingConfig config)
    {
        LedEffect? effect = FindEffect(config.EffectId) ?? throw new AulaProtocolException(
            $"Unknown effect id {config.EffectId}.");

        var raw = _protocol.ReadConfigRaw();

        raw[_model.CustomModeOffset] = config.PerKeyColors is not null ? (byte)0x01 : (byte)0x00;
        raw[_model.EffectIdOffset] = (byte)config.EffectId;

        int paramBase = _model.EffectParamsBase + _model.EffectParamsStride * config.EffectId;

        if (effect.HasBrightness && config.Brightness is int brightness)
        {
            raw[paramBase] = (byte)Math.Clamp(brightness, 0, MaxBrightness);
        }

        if (config.RawFlags is byte rawFlags)
        {
            raw[paramBase + 1] = rawFlags;
        }
        else if (effect.HasSpeed && config.Speed is int speed)
        {
            byte flags = config.Colorful ? ColorfulFlag : SingleColorFlag;
            raw[paramBase + 1] = (byte)((Math.Clamp(speed, 0, MaxSpeed) << 4) | flags);
        }

        if (effect.HasColor && config.Color is not null && !effect.HasSpeed)
        {
            raw[paramBase + 1] = config.Colorful ? ColorfulFlag : SingleColorFlag;
        }

        _protocol.WriteConfigRaw(raw);

        if (config.PerKeyColors is not null)
        {
            _protocol.WritePerKeyColors(config.PerKeyColors);
        }
        else if (config.Color is RgbColor color)
        {
            _protocol.WriteColorProfile(color);
        }
    }

    public void Reset()
    {
        byte[] raw = _protocol.ReadConfigRaw();

        raw[_model.CustomModeOffset] = 0x00;
        raw[_model.EffectIdOffset] = 0x01;
        raw[_model.SideLightOffset] = 0x00;
        raw[_model.BatteryLightOffset] = 0x00;

        int paramBase = _model.EffectParamsBase + _model.EffectParamsStride * 1;
        raw[paramBase] = 0x09;
        raw[paramBase + 1] = 0x00;

        _protocol.WriteConfigRaw(raw);
        _protocol.WriteColorProfile(RgbColor.FromRgb(255, 255, 255));
    }

    public void TurnOff() => Apply(new LightingConfig(EffectId: 0));

    public LedEffect? FindEffect(int id) => _model.Effects.FirstOrDefault(e => e.Id == id);

    public LedEffect? FindEffect(string name) =>
        _model.Effects.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
}
