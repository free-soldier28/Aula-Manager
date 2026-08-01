using Aula.Cli;
using Aula.Core;
using Aula.Core.Abstractions;
using Aula.Core.Devices;
using Aula.Core.Models;
using Aula.Core.Services;

namespace Aula.Cli;

public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            return Execute(CliCommandParser.Parse(args));
        }
        catch (CliParseException ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            Console.Error.WriteLine("Run 'aula help' for usage.");
            return 2;
        }
        catch (AulaDeviceNotFoundException ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }
        catch (AulaException ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }

    private static int Execute(CliCommand command) => command switch
    {
        HelpCommand => PrintHelp(),
        ListCommand => RunList(),
        InfoCommand c => RunInfo(c),
        EffectsCommand c => RunEffects(c),
        EffectCommand c => RunEffect(c),
        OffCommand c => RunEffect(new EffectCommand(c.Model, 0, null, null, null, false)),
        PerKeyCommand c => RunPerKey(c),
        DumpCommand c => RunDump(c),
        _ => 0,
    };

    private static int RunList()
    {
        IReadOnlyList<DeviceInfo> devices = new HidDeviceScanner().ScanAll();
        if (devices.Count == 0)
        {
            Console.WriteLine("No AULA devices found.");
            return 1;
        }

        foreach (DeviceInfo device in devices)
        {
            Console.WriteLine(
                $"{device.VendorId:X4}:{device.ProductId:X4}  {device.DisplayName}  feature={device.MaxFeatureReportLength}  {device.DevicePath}");
        }

        return 0;
    }

    private static int RunInfo(InfoCommand c)
    {
        using IAulaKeyboard keyboard = OpenKeyboard(c.Model);

        Console.WriteLine($"Device      : {keyboard.Info.DisplayName}");
        Console.WriteLine($"VID:PID     : {keyboard.Info.VendorId:X4}:{keyboard.Info.ProductId:X4}");
        Console.WriteLine($"Serial      : {keyboard.Info.SerialNumber ?? "-"}");

        if (keyboard is ISinowealthDiagnostics diagnostics)
        {
            byte[] model = diagnostics.QueryModel();
            Console.WriteLine($"Model       : 0x{model[8]:X2}  (psd {model[12]:X2}:{model[13]:X2})");
            Console.WriteLine($"Model raw   : {Convert.ToHexString(model)}");
        }

        return 0;
    }

    private static int RunEffects(EffectsCommand c)
    {
        var model = ModelConfig.Resolve(c.Model);
        foreach (LedEffect effect in model.Effects)
        {
            Console.WriteLine(
                $"{effect.Id,2}  {effect.Name,-15} speed={effect.HasSpeed,-5} brightness={effect.HasBrightness,-5} color={effect.HasColor}");
        }

        return 0;
    }

    private static int RunEffect(EffectCommand c)
    {
        using IAulaKeyboard keyboard = OpenKeyboard(c.Model);
        var config = new LightingConfig(c.EffectId, c.Brightness, c.Speed, c.Color, c.Colorful, c.RawFlags);

        keyboard.Lighting.Apply(config);

        string colorText = c.Color is { } color ? color.ToHex() : c.Colorful ? "colorful" : "-";
        string flagsText = c.RawFlags is { } f ? $" flags=0x{f:X2}" : "";
        Console.WriteLine($"Applied effect '{config.EffectId}' brightness={config.Brightness?.ToString() ?? "-"} " +
                          $"speed={config.Speed?.ToString() ?? "-"} color={colorText}{flagsText}");
        return 0;
    }

    private static int RunPerKey(PerKeyCommand c)
    {
        const int LedCount = 126;

        using IAulaKeyboard keyboard = OpenKeyboard(c.Model);

        var colors = new RgbColor[LedCount];
        for (int i = 0; i < LedCount; i++)
        {
            colors[i] = new RgbColor(0, 0, 0);
        }

        if (c.KeyColors is { Count: > 0 } keyColors)
        {
            IKeyboardLayout layout = keyboard.Layout;
            foreach ((string key, RgbColor color) in keyColors)
            {
                int index = layout.GetLedIndex(key);
                if (index < 0)
                {
                    throw new AulaException($"Unknown key '{key}'. Known keys: {string.Join(", ", layout.Keys)}");
                }

                colors[index] = color;
            }

            if (c.FillAll)
            {
                for (int i = 0; i < LedCount; i++)
                {
                    if (colors[i] == default)
                    {
                        colors[i] = c.Color;
                    }
                }
            }
        }
        else if (c.LedIndex is int led)
        {
            if (led < 0 || led >= LedCount)
            {
                throw new AulaException($"LED index {led} out of range 0-{LedCount - 1}.");
            }

            colors[led] = c.Color;
        }
        else
        {
            for (int i = 0; i < LedCount; i++)
            {
                colors[i] = c.Color;
            }
        }

        var config = new LightingConfig(EffectId: 21, PerKeyColors: colors);
        keyboard.Lighting.Apply(config);

        string detail = c.KeyColors is { Count: > 0 } keys
            ? string.Join(", ", keys.Select(kv => $"{kv.Key}={kv.Value.ToHex()}"))
            : c.LedIndex is { } idx
                ? $"LED {idx} = {c.Color.ToHex()}"
                : $"{LedCount} LEDs = {c.Color.ToHex()}";
        Console.WriteLine($"Applied per-key custom mode ({detail})");
        return 0;
    }

    private static int RunDump(DumpCommand c)
    {
        using IAulaKeyboard keyboard = OpenKeyboard(c.Model);
        KeyboardConfig config = keyboard.Lighting.ReadConfig();

        Console.WriteLine($"Effect      : {config.EffectId}");
        Console.WriteLine($"Custom mode : {config.CustomMode}");
        Console.WriteLine($"Side light  : {config.SideLightEffect}");
        Console.WriteLine($"Battery     : {config.BatteryLightEffect}");

        if (config.GetParams(config.EffectId) is { } p)
        {
            Console.WriteLine($"Brightness  : {p.Brightness}");
            Console.WriteLine($"Speed       : {p.Speed}");
            Console.WriteLine($"Colorful    : {p.Colorful}");
        }

        Console.WriteLine($"Raw ({config.Raw.Length} bytes):");
        Console.WriteLine(FormatHex(config.Raw));

        if (keyboard is ISinowealthDiagnostics diagnostics)
        {
            byte[] profile = diagnostics.ReadColorProfileRaw();
            Console.WriteLine($"Color profile ({profile.Length} bytes):");
            Console.WriteLine(FormatHex(profile));
        }

        return 0;
    }

    private static IAulaKeyboard OpenKeyboard(string modelId) =>
        new KeyboardDeviceFactory().Open(modelId);

    private static string FormatHex(byte[] bytes)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < bytes.Length; i++)
        {
            if (i % 16 == 0)
            {
                if (i > 0)
                {
                    sb.AppendLine();
                }

                sb.Append($"{i:X4}: ");
            }

            sb.Append(bytes[i].ToString("X2")).Append(' ');
        }

        return sb.ToString().TrimEnd();
    }

    private static int PrintHelp()
    {
        Console.WriteLine("""
            AulaManager — AULA keyboard lighting controller

            Usage: aula <command> [options]

            Commands:
              list                   List detected AULA devices
              info [--model ID]      Show device info and model bytes
              effects [--model ID]   List supported lighting effects
              effect <name|id>       Apply a lighting effect
                     [--brightness N]   0-9
                     [--speed N]        0-4
                     [--color #RRGGBB]  single color (also: --color R G B)
                     [--colorful]       rainbow/colorful mode
                     [--model ID]
              off [--model ID]       Turn lighting off
              perkey                Set per-key colors (custom mode)
                     [--color #RRGGBB]  fill base color (also: --color R G B)
                     [--fill-all]       fill all LEDs with base color
                     [--led N]          set single LED index
                     KEY=#RRGGBB ...    set colors per key name (e.g. w=ff0000 space=00ff00)
                     [--model ID]
              dump [--model ID]      Read and print current keyboard config
              help                   Show this help

            Models: f75 (default), f87
            """);
        return 0;
    }
}
