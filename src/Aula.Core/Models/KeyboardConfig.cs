namespace Aula.Core.Models;

public class KeyboardConfig
{
    private const byte CmdReadConfig = 0x84;

    private readonly byte[] _raw;
    private readonly ModelConfig _model;

    public KeyboardConfig(byte[] raw, ModelConfig model)
    {
        _raw = raw;
        _model = model;
    }

    public byte[] Raw => _raw;

    public virtual int EffectId => _raw[_model.EffectIdOffset];

    public virtual bool CustomMode => _raw[_model.CustomModeOffset] == 0x01;

    public virtual int SideLightEffect => _raw[_model.SideLightOffset];

    public virtual int BatteryLightEffect => _raw[_model.BatteryLightOffset];

    public virtual EffectParams? GetParams(int effectId)
    {
        if (effectId < 1)
        {
            return null;
        }

        int offset = _model.EffectParamsBase + _model.EffectParamsStride * effectId;
        if (offset + 1 >= _model.ConfigResponseLength)
        {
            return null;
        }

        return new EffectParams(_raw[offset], _raw[offset + 1]);
    }

    public static KeyboardConfig Parse(byte[] response, ModelConfig model)
    {
        if (response.Length < model.ConfigResponseLength)
        {
            throw new AulaProtocolException(
                $"Config response is {response.Length} bytes, expected {model.ConfigResponseLength}.");
        }

        if (response[0] != model.ReportId || response[1] != CmdReadConfig)
        {
            throw new AulaProtocolException(
                $"Unexpected config response header: {response[0]:X2} {response[1]:X2}.");
        }

        return new KeyboardConfig(response, model);
    }
}

public readonly record struct EffectParams(byte Brightness, byte SpeedFlags)
{
    public int Speed => SpeedFlags >> 4;

    public bool Colorful => (SpeedFlags & 0x0F) == 0x07;

    public int ColorMode => SpeedFlags & 0x0F;
}
