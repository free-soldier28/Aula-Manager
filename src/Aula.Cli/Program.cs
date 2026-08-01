using Aula.Cli;
using Aula.Core;
using Aula.Core.Devices;
using Aula.Core.Models;
using Aula.Core.Protocol;
using Aula.Core.Services;
using HidSharp;

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
        using var session = OpenSession(c.Model);
        byte[] model = session.Protocol.QueryModel();

        Console.WriteLine($"Device      : {session.Transport.Info.DisplayName}");
        Console.WriteLine($"VID:PID     : {session.Transport.Info.VendorId:X4}:{session.Transport.Info.ProductId:X4}");
        Console.WriteLine($"Serial      : {session.Transport.Info.SerialNumber ?? "-"}");
        Console.WriteLine($"Model       : 0x{model[8]:X2}  (psd {model[12]:X2}:{model[13]:X2})");
        Console.WriteLine($"Model raw   : {Convert.ToHexString(model)}");
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
        using var session = OpenSession(c.Model);
        var lighting = new LightingService(session.Protocol);
        var config = new LightingConfig(c.EffectId, c.Brightness, c.Speed, c.Color, c.Colorful);

        lighting.Apply(config);

        string colorText = c.Color is { } color ? color.ToHex() : c.Colorful ? "colorful" : "-";
        Console.WriteLine($"Applied effect '{config.EffectId}' brightness={config.Brightness?.ToString() ?? "-"} " +
                          $"speed={config.Speed?.ToString() ?? "-"} color={colorText}");
        return 0;
    }

    private static int RunDump(DumpCommand c)
    {
        using var session = OpenSession(c.Model);
        var lighting = new LightingService(session.Protocol);
        KeyboardConfig config = lighting.ReadConfig();

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
        return 0;
    }

    private static Session OpenSession(string modelId)
    {
        var model = ModelConfig.Resolve(modelId);
        var scanner = new HidDeviceScanner();
        var devices = scanner.Scan(model.VendorId, model.ProductId);
        DeviceInfo? picked = DevicePicker.PickBest(devices) ?? DevicePicker.PickBest(scanner.ScanAll());

        if (picked is null)
        {
            throw new AulaDeviceNotFoundException();
        }

        HidDevice device = DeviceList.Local.GetHidDevices()
            .FirstOrDefault(d => d.DevicePath == picked.DevicePath)
            ?? throw new AulaDeviceNotFoundException();

        var transport = new HidSharpTransport(device);
        transport.Open();

        return new Session(transport, new SinowealthProtocol(transport, model));
    }

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
                     [--brightness N]   0-4
                     [--speed N]        0-4
                     [--color #RRGGBB]  single color (also: --color R G B)
                     [--colorful]       rainbow/colorful mode
                     [--model ID]
              off [--model ID]       Turn lighting off
              dump [--model ID]      Read and print current keyboard config
              help                   Show this help

            Models: f75 (default), f87
            """);
        return 0;
    }

    private sealed class Session : IDisposable
    {
        public Session(IHidTransport transport, SinowealthProtocol protocol)
        {
            Transport = transport;
            Protocol = protocol;
        }

        public IHidTransport Transport { get; }

        public SinowealthProtocol Protocol { get; }

        public void Dispose() => Transport.Dispose();
    }
}
