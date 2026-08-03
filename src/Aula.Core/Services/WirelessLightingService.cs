using Aula.Core.Abstractions;
using Aula.Core.Logging;
using Aula.Core.Models;
using Aula.Core.Protocol;
using Microsoft.Extensions.Logging;

namespace Aula.Core.Services;

/// <summary>
/// Lighting controller for the 2.4 GHz receiver protocol (Report ID 0x13).
/// Applies an effect via the documented 4-phase sequence:
///   read config -> write config -> write color palette -> save.
/// Per-key mode writes effect 21 + the 28-fragment per-key color map.
/// </summary>
public sealed class WirelessLightingService : ILightingController
{
    private const int MaxBrightness = 9;
    private const int MaxSpeed = 4;
    private const byte ColorfulMode = 0x07;
    private const byte SingleColorMode = 0x00;
    private const int PerKeyEffect = 21;
    private const int ConfigFragmentCount = 10;
    private const int PaletteFragmentCount = 37;

    private readonly WirelessProtocol _protocol;
    private readonly ModelConfig _model;
    private readonly ILogger<WirelessLightingService> _log;

    public WirelessLightingService(WirelessProtocol protocol, ModelConfig model)
    {
        _protocol = protocol;
        _model = model;
        _log = AulaLogging.Logger<WirelessLightingService>();
    }

    public KeyboardConfig ReadConfig()
    {
        byte[]?[] fragments = _protocol.ReadConfig();
        return new WirelessConfig(fragments, _model);
    }

    public void Apply(LightingConfig config)
    {
        if (FindEffect(config.EffectId) is null)
        {
            throw new AulaProtocolException($"Unknown effect id {config.EffectId}.");
        }

        _log.LogInformation(
            "Applying wireless effect {EffectId} brightness={Brightness} speed={Speed} color={Color} colorful={Colorful}",
            config.EffectId,
            config.Brightness?.ToString() ?? "-",
            config.Speed?.ToString() ?? "-",
            config.Color?.ToHex() ?? "-",
            config.Colorful);

        byte[]?[] read = _protocol.ReadConfig();
        bool gotConfig = read.All(f => f is not null);

        var writeFragments = new List<byte[]>(ConfigFragmentCount);
        for (int seq = 0; seq < ConfigFragmentCount; seq++)
        {
            writeFragments.Add(
                gotConfig
                    ? ModifyConfigFragment(read[seq]!, seq, config)
                    : BuildConfigFragmentFromTemplate(seq, config));
        }

        _protocol.WriteConfig(writeFragments);

        if (config.PerKeyColors is not null)
        {
            byte[][] map = BuildPerKeyFragments(config.PerKeyColors);
            _protocol.WriteBatch(map);
        }
        else
        {
            byte[][] palette = BuildPaletteFragments(config.Color);
            _protocol.WriteBatch(palette);
        }

        Save();
    }

    public void TurnOff() => Apply(new LightingConfig(EffectId: 0));

    public void Reset()
    {
        _log.LogInformation("Resetting wireless lighting config to factory defaults");
        var writeFragments = new List<byte[]>(ConfigFragmentCount);
        for (int seq = 0; seq < ConfigFragmentCount; seq++)
        {
            writeFragments.Add(
                WirelessFrame.Build(WirelessFrame.CmdWrite, WirelessFrame.SubCmdConfig, (byte)seq, ConfigTemplate[seq]));
        }

        _protocol.WriteConfig(writeFragments);

        byte[][] palette = BuildPaletteFragments(null);
        _protocol.WriteBatch(palette);
        Save();
    }

    public LedEffect? FindEffect(int id) => _model.Effects.FirstOrDefault(e => e.Id == id);

    public LedEffect? FindEffect(string name) =>
        _model.Effects.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));

    private void Save()
    {
        var save = WirelessFrame.Build(
            WirelessFrame.CmdSave, WirelessFrame.SubCmdConfirm, 0, new byte[] { 0x04, 0x07 });
        _protocol.WriteAndAwaitEcho(save);
    }

    private static byte[] ModifyConfigFragment(byte[] fragment, int seq, LightingConfig config)
    {
        byte[] f = (byte[])fragment.Clone();
        f[1] = WirelessFrame.CmdWrite;
        if (seq == 0)
        {
            f[8] = 0x01;
            f[14] = 0x00;
            f[15] = (byte)config.EffectId;
            f[17] = UseCustomColor(config) ? (byte)0x01 : (byte)0x03;
        }

        if (config.EffectId is >= 1 and <= 18)
        {
            (int tableSeq, int offset) = WirelessConfig.EffectTableLocation(config.EffectId);
            if (seq == tableSeq)
            {
                ApplyEffectParams(f, offset, config);
            }
        }

        f[19] = WirelessFrame.Checksum(f);
        return f;
    }

    private static byte[] BuildConfigFragmentFromTemplate(int seq, LightingConfig config)
    {
        byte[] payload = (byte[])ConfigTemplate[seq].Clone();
        if (seq == 0)
        {
            payload[4] = 0x01;
            payload[10] = 0x00;
            payload[11] = (byte)config.EffectId;
            payload[13] = UseCustomColor(config) ? (byte)0x01 : (byte)0x03;
        }

        if (config.EffectId is >= 1 and <= 18)
        {
            (int tableSeq, int offset) = WirelessConfig.EffectTableLocation(config.EffectId);
            if (seq == tableSeq)
            {
                ApplyEffectParams(payload, offset - 4, config);
            }
        }

        return WirelessFrame.Build(WirelessFrame.CmdWrite, WirelessFrame.SubCmdConfig, (byte)seq, payload);
    }

    private static void ApplyEffectParams(byte[] target, int offset, LightingConfig config)
    {
        if (config.Brightness is int brightness)
        {
            target[offset] = (byte)Math.Clamp(brightness, 0, MaxBrightness);
        }

        if (config.RawFlags is byte rawFlags)
        {
            target[offset + 1] = rawFlags;
            return;
        }

        int currentSpeed = (target[offset + 1] >> 4) & 0x0F;
        int currentMode = target[offset + 1] & 0x0F;
        int newSpeed = config.Speed is int speed ? Math.Clamp(speed, 0, MaxSpeed) : currentSpeed;
        int newMode = config.Colorful
            ? ColorfulMode
            : config.Color is not null ? SingleColorMode : currentMode;
        target[offset + 1] = (byte)((newSpeed << 4) | newMode);
    }

    private static bool UseCustomColor(LightingConfig config) =>
        config.PerKeyColors is not null || config.Color is not null || config.Colorful;

    private static byte[][] BuildPaletteFragments(RgbColor? color)
    {
        var fragments = new byte[PaletteFragmentCount][];
        for (int i = 0; i < PaletteFragmentCount; i++)
        {
            byte[] payload;
            if (i < PaletteTemplate.Length)
            {
                payload = (byte[])PaletteTemplate[i].Clone();
            }
            else if (i == PaletteFragmentCount - 1)
            {
                payload = PaletteLast;
            }
            else
            {
                payload = PaletteZeros;
            }

            if (i == 1 && color is { } c)
            {
                payload[8] = c.R;
                payload[9] = c.G;
                payload[10] = c.B;
                payload[12] = 0xFF;
            }

            fragments[i] = WirelessFrame.Build(
                WirelessFrame.CmdColor, WirelessFrame.SubCmdPalette, (byte)i, payload);
        }

        return fragments;
    }

    private static byte[][] BuildPerKeyFragments(IReadOnlyList<RgbColor> colors)
    {
        const int planeFragments = 9;
        const int entriesPerFragment = 14;
        const int totalFragments = planeFragments * 3 + 1;

        var frames = new byte[totalFragments][];
        int seq = 0;

        foreach (PlaneSelector selector in new[] { PlaneSelector.Red, PlaneSelector.Green, PlaneSelector.Blue })
        {
            for (int s = 0; s < planeFragments; s++)
            {
                var payload = new byte[WirelessFrame.PayloadLength];
                payload[0] = 0x0E;
                for (int j = 0; j < entriesPerFragment; j++)
                {
                    int index = s * entriesPerFragment + j;
                    byte value = 0;
                    if (index < colors.Count)
                    {
                        RgbColor c = colors[index];
                        value = selector switch
                        {
                            PlaneSelector.Red => c.R,
                            PlaneSelector.Green => c.G,
                            _ => c.B,
                        };
                    }

                    payload[1 + j] = value;
                }

                frames[seq] = WirelessFrame.Build(
                    WirelessFrame.CmdPerKey, WirelessFrame.SubCmdPerKey, (byte)seq, payload);
                seq++;
            }
        }

        var trailer = new byte[WirelessFrame.PayloadLength];
        trailer[0] = 0x06;
        trailer[3] = 0x5A;
        trailer[4] = 0xA5;
        frames[seq] = WirelessFrame.Build(
            WirelessFrame.CmdPerKey, WirelessFrame.SubCmdPerKey, (byte)seq, trailer);

        return frames;
    }

    private enum PlaneSelector
    {
        Red,
        Green,
        Blue,
    }

    private static readonly byte[][] ConfigTemplate =
    {
        new byte[] { 0x0E, 0x00, 0x03, 0x03, 0x01, 0x00, 0x00, 0x04, 0x04, 0x07, 0x00, 0x00, 0x20, 0x03, 0x00 },
        new byte[] { 0x0E, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x02, 0x01, 0x00, 0xFF, 0x0A, 0x00, 0x00, 0x00 },
        new byte[] { 0x0E, 0x01, 0x00, 0x04, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 },
        new byte[] { 0x0E, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 },
        new byte[] { 0x0E, 0xFF, 0xFF, 0x04, 0x47, 0x04, 0x47, 0x04, 0x47, 0x04, 0x47, 0x04, 0x47, 0x04, 0x47 },
        new byte[] { 0x0E, 0x04, 0x47, 0x04, 0x47, 0x04, 0x47, 0x04, 0x47, 0x04, 0x47, 0x04, 0x47, 0x04, 0x47 },
        new byte[] { 0x0E, 0x04, 0x47, 0x04, 0x47, 0x04, 0x47, 0x04, 0x37, 0x04, 0x37, 0x04, 0x37, 0x04, 0x37 },
        new byte[] { 0x0E, 0x07, 0x47, 0x07, 0x47, 0x07, 0x44, 0x07, 0x44, 0x07, 0x44, 0x07, 0x44, 0x07, 0x44 },
        new byte[] { 0x0E, 0x07, 0x44, 0x07, 0x44, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04 },
        new byte[] { 0x02, 0x5A, 0xA5, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 },
    };

    private static readonly byte[][] PaletteTemplate =
    {
        new byte[] { 0x0E, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 },
        new byte[] { 0x0E, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0x00, 0x00, 0x00, 0xFF, 0x00, 0x00 },
        new byte[] { 0x0E, 0x00, 0xFF, 0xFF, 0xFF, 0x00, 0xFF, 0x00, 0xFF, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF },
        new byte[] { 0x0E, 0xFF, 0x00, 0x00, 0x00, 0xFF, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0x00, 0xFF, 0x00 },
        new byte[] { 0x0E, 0xFF, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0xFF, 0x00, 0x00 },
        new byte[] { 0x0E, 0x00, 0xFF, 0xFF, 0xFF, 0x00, 0xFF, 0x00, 0xFF, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF },
        new byte[] { 0x0E, 0xFF, 0x00, 0x00, 0x00, 0xFF, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0x00, 0xFF, 0x00 },
        new byte[] { 0x0E, 0xFF, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0xFF, 0x00, 0x00 },
        new byte[] { 0x0E, 0x00, 0xFF, 0xFF, 0xFF, 0x00, 0xFF, 0x00, 0xFF, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF },
        new byte[] { 0x0E, 0xFF, 0x00, 0x00, 0x00, 0xFF, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0x00, 0xFF, 0x00 },
        new byte[] { 0x0E, 0xFF, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0xFF, 0x00, 0x00 },
        new byte[] { 0x0E, 0x00, 0xFF, 0xFF, 0xFF, 0x00, 0xFF, 0x00, 0xFF, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF },
        new byte[] { 0x0E, 0xFF, 0x00, 0x00, 0x00, 0xFF, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0x00, 0xFF, 0x00 },
        new byte[] { 0x0E, 0xFF, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0xFF, 0x00, 0x00 },
        new byte[] { 0x0E, 0x00, 0xFF, 0xFF, 0xFF, 0x00, 0xFF, 0x00, 0xFF, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF },
        new byte[] { 0x0E, 0xFF, 0x00, 0x00, 0x00, 0xFF, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0x00, 0xFF, 0x00 },
        new byte[] { 0x0E, 0xFF, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0xFF, 0x00, 0x00 },
        new byte[] { 0x0E, 0x00, 0xFF, 0xFF, 0xFF, 0x00, 0xFF, 0x00, 0xFF, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF },
        new byte[] { 0x0E, 0xFF, 0x00, 0x00, 0x00, 0xFF, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0x00, 0xFF, 0x00 },
        new byte[] { 0x0E, 0xFF, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0xFF, 0x00, 0x00 },
        new byte[] { 0x0E, 0x00, 0xFF, 0xFF, 0xFF, 0x00, 0xFF, 0x00, 0xFF, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF },
    };

    private static readonly byte[] PaletteZeros = { 0x0E, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

    private static readonly byte[] PaletteLast =
    {
        0x08, 0x00, 0x00, 0x5A, 0xA5, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    };
}
