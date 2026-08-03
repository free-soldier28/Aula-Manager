using Aula.Core;
using Aula.Core.Abstractions;
using Aula.Core.Devices;
using Aula.Core.Drivers;
using Aula.Core.Logging;
using Aula.Core.Models;
using Aula.Core.Protocol;
using Aula.Core.Services;
using Aula.Core.Updating;
using Microsoft.Extensions.Logging;

namespace Aula.Cli;

/// <summary>
/// Executes parsed CLI commands. Dependencies are injectable so the full
/// command surface can be exercised in tests with fake devices/services.
/// </summary>
public sealed class CliRunner
{
    private readonly IHidDeviceScanner _scanner;
    private readonly KeyboardDeviceFactory _factory;
    private readonly ITransportFactory _transportFactory;
    private readonly ProfileService _profileService;
    private readonly UpdateService _updateService;
    private readonly UpdateInstaller _updateInstaller;
    private readonly TextWriter _out;
    private readonly TextWriter _err;
    private readonly ILogger<CliRunner> _log;

    public CliRunner(
        IHidDeviceScanner? scanner = null,
        KeyboardDeviceFactory? factory = null,
        ProfileService? profileService = null,
        UpdateService? updateService = null,
        UpdateInstaller? updateInstaller = null,
        ITransportFactory? transportFactory = null,
        TextWriter? @out = null,
        TextWriter? err = null)
    {
        _scanner = scanner ?? new HidDeviceScanner();
        _factory = factory ?? new KeyboardDeviceFactory();
        _profileService = profileService ?? new ProfileService();
        _updateService = updateService ?? new UpdateService();
        _updateInstaller = updateInstaller ?? new UpdateInstaller();
        _transportFactory = transportFactory ?? new HidSharpTransportFactory();
        _out = @out ?? Console.Out;
        _err = err ?? Console.Error;
        _log = AulaLogging.Logger<CliRunner>();
    }

    public int Run(string[] args)
    {
        CliCommand command = CliCommandParser.Parse(args);
        _log.LogDebug("Running command {Command}", command.GetType().Name);
        return Run(command);
    }

    public int Run(CliCommand command) => command switch
    {
        HelpCommand => PrintHelp(),
        ListCommand => RunList(),
        InfoCommand c => RunInfo(c),
        EffectsCommand c => RunEffects(c),
        EffectCommand c => RunEffect(c),
        OffCommand c => RunEffect(new EffectCommand(c.Model, 0, null, null, null, false)),
        ResetCommand c => RunReset(c),
        PerKeyCommand c => RunPerKey(c),
        ProfileCommand c => RunProfile(c),
        UpdateCommand c => RunUpdate(c).GetAwaiter().GetResult(),
        DumpCommand c => RunDump(c),
        WirelessCommand c => RunWireless(c),
        _ => 0,
    };

    public int RunList()
    {
        IReadOnlyList<DeviceInfo> devices = _scanner.ScanAll();
        _log.LogInformation("Found {Count} AULA device(s)", devices.Count);
        if (devices.Count == 0)
        {
            _out.WriteLine("No AULA devices found.");
            return 1;
        }

        foreach (DeviceInfo device in devices)
        {
            _out.WriteLine(
                $"{device.VendorId:X4}:{device.ProductId:X4}  {device.DisplayName}  " +
                $"feature={device.MaxFeatureReportLength}  input={device.MaxInputReportLength}  output={device.MaxOutputReportLength}  {device.DevicePath}");
        }

        return 0;
    }

    public int RunWireless(WirelessCommand c)
    {
        return c.Action switch
        {
            "scan" => WirelessScan(),
            "read" => WirelessRead(),
            "effect" => WirelessEffect(c.Effect!),
            _ => Print($"Unknown wireless action '{c.Action}'. Use scan, read or effect.", error: true),
        };
    }

    public int WirelessScan()
    {
        IReadOnlyList<DeviceInfo> devices = _scanner.ScanAll()
            .Where(d => d.VendorId == AulaDeviceIds.VendorWireless && d.ProductId == AulaDeviceIds.ProductWireless)
            .ToList();

        if (devices.Count == 0)
        {
            _out.WriteLine("No 2.4G receiver found.");
            return 1;
        }

        _out.WriteLine($"2.4G receiver ({devices.Count} collection(s)):");
        foreach (DeviceInfo device in devices)
        {
            _out.WriteLine(
                $"  feature={device.MaxFeatureReportLength,-2} input={device.MaxInputReportLength,-3} output={device.MaxOutputReportLength,-3}  {device.DevicePath}");
        }

        return 0;
    }

    public int WirelessRead()
    {
        IReadOnlyList<DeviceInfo> collections = _scanner.ScanAll()
            .Where(d => d.VendorId == AulaDeviceIds.VendorWireless && d.ProductId == AulaDeviceIds.ProductWireless)
            .Where(d => d.MaxOutputReportLength > 0)
            .ToList();

        if (collections.Count == 0)
        {
            _out.WriteLine("No 2.4G receiver collection with an output report found.");
            return 1;
        }

        bool anyResponse = false;

        foreach (DeviceInfo collection in collections)
        {
            _out.WriteLine($"Probing {collection.DevicePath}");
            _out.WriteLine(
                $"  feature={collection.MaxFeatureReportLength}  input={collection.MaxInputReportLength}  output={collection.MaxOutputReportLength}");

            using IHidTransport transport = _transportFactory.Create(collection);
            try
            {
                transport.Open();
            }
            catch (Exception ex)
            {
                _out.WriteLine($"  open failed: {ex.Message}");
                continue;
            }

            var protocol = new WirelessProtocol(transport);
            byte[]?[] config = protocol.ReadConfig();
            int received = config.Count(f => f is not null);
            _out.WriteLine($"  READ config: {received}/10 fragments");

            for (int i = 0; i < config.Length; i++)
            {
                if (config[i] is { } fragment)
                {
                    anyResponse = true;
                    bool valid = WirelessProtocol.HasValidChecksum(fragment);
                    _out.WriteLine($"    [{i:00}] {(valid ? "OK " : "BAD")} {Convert.ToHexString(fragment)}");
                }
            }

            if (received == 0)
            {
                _out.WriteLine("  no response on this collection");
            }
        }

        return anyResponse ? 0 : 1;
    }

    public int WirelessEffect(WirelessEffectCommand c)
    {
        using IAulaKeyboard keyboard = OpenWirelessKeyboard();
        var config = new LightingConfig(c.EffectId, c.Brightness, c.Speed, c.Color, c.Colorful);
        keyboard.Lighting.Apply(config);

        string colorText = c.Color is { } color ? color.ToHex() : c.Colorful ? "colorful" : "-";
        _out.WriteLine($"Wireless: applied effect '{config.EffectId}' brightness={config.Brightness?.ToString() ?? "-"} " +
                       $"speed={config.Speed?.ToString() ?? "-"} color={colorText}");
        return 0;
    }

    private IAulaKeyboard OpenWirelessKeyboard()
    {
        IReadOnlyList<DeviceInfo> collections = _scanner.ScanAll()
            .Where(d => d.VendorId == AulaDeviceIds.VendorWireless && d.ProductId == AulaDeviceIds.ProductWireless)
            .Where(d => d.MaxOutputReportLength > 0)
            .ToList();

        if (collections.Count == 0)
        {
            throw new AulaException("2.4G receiver not found. Pair the dongle and try again.");
        }

        return _factory.TryOpen() ?? throw new AulaException("No AULA device found for the 2.4G receiver.");
    }

    public int RunInfo(InfoCommand c)
    {
        using IAulaKeyboard keyboard = OpenKeyboard(c.Model);
        _log.LogInformation("Querying device info (model {Model})", keyboard.Model.Id);

        _out.WriteLine($"Device      : {keyboard.Info.DisplayName}");
        _out.WriteLine($"VID:PID     : {keyboard.Info.VendorId:X4}:{keyboard.Info.ProductId:X4}");
        _out.WriteLine($"Serial      : {keyboard.Info.SerialNumber ?? "-"}");

        if (keyboard is ISinowealthDiagnostics diagnostics)
        {
            byte[] model = diagnostics.QueryModel();
            _out.WriteLine($"Model       : 0x{model[8]:X2}  (psd {model[12]:X2}:{model[13]:X2})");
            _out.WriteLine($"Model raw   : {Convert.ToHexString(model)}");
        }

        return 0;
    }

    public int RunEffects(EffectsCommand c)
    {
        var model = ModelConfig.Resolve(c.Model);
        foreach (LedEffect effect in model.Effects)
        {
            _out.WriteLine(
                $"{effect.Id,2}  {effect.Name,-15} speed={effect.HasSpeed,-5} brightness={effect.HasBrightness,-5} color={effect.HasColor}");
        }

        return 0;
    }

    public int RunEffect(EffectCommand c)
    {
        using IAulaKeyboard keyboard = OpenKeyboard(c.Model);
        var config = new LightingConfig(c.EffectId, c.Brightness, c.Speed, c.Color, c.Colorful, c.RawFlags);

        keyboard.Lighting.Apply(config);

        string colorText = c.Color is { } color ? color.ToHex() : c.Colorful ? "colorful" : "-";
        string flagsText = c.RawFlags is { } f ? $" flags=0x{f:X2}" : "";
        _out.WriteLine($"Applied effect '{config.EffectId}' brightness={config.Brightness?.ToString() ?? "-"} " +
                       $"speed={config.Speed?.ToString() ?? "-"} color={colorText}{flagsText}");
        return 0;
    }

    public int RunReset(ResetCommand c)
    {
        if (!string.IsNullOrWhiteSpace(c.VendorPath))
        {
            return RunVendorReset(c.VendorPath);
        }

        using IAulaKeyboard keyboard = OpenKeyboard(c.Model);
        keyboard.Lighting.Reset();
        _out.WriteLine($"Reset lighting config to factory defaults (static white, custom mode off).");
        return 0;
    }

    public int RunVendorReset(string vendorPath)
    {
        string full = Path.GetFullPath(vendorPath);
        if (!File.Exists(full) && Directory.Exists(full))
        {
            var candidates = Directory.GetFiles(full, "*.exe", SearchOption.TopDirectoryOnly);
            if (candidates.Length == 0)
            {
                _err.WriteLine($"error: no reset tool (.exe) found in: {full}");
                return 1;
            }

            full = candidates[0];
        }

        if (!File.Exists(full))
        {
            _err.WriteLine($"error: reset tool not found: {full}");
            return 1;
        }

        _out.WriteLine($"Launching official reset tool: {full}");
        _out.WriteLine("Follow the tool's on-screen steps. Complete it to fully restore the keyboard.");
        try
        {
            using var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo(full)
                {
                    UseShellExecute = true,
                },
            };
            process.Start();
            process.WaitForExit();
            _out.WriteLine($"Reset tool exited with code {process.ExitCode}.");
            return process.ExitCode;
        }
        catch (Exception ex)
        {
            _err.WriteLine($"error: cannot launch reset tool: {ex.Message}");
            return 1;
        }
    }

    public int RunPerKey(PerKeyCommand c)
    {
        using IAulaKeyboard keyboard = OpenKeyboard(c.Model);
        IKeyboardLayout layout = keyboard.Layout;
        int ledCount = layout.LedCount;

        var colors = new RgbColor[ledCount];
        for (int i = 0; i < ledCount; i++)
        {
            colors[i] = new RgbColor(0, 0, 0);
        }

        if (c.KeyColors is { Count: > 0 } keyColors)
        {
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
                for (int i = 0; i < ledCount; i++)
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
            if (led < 0 || led >= ledCount)
            {
                throw new AulaException($"LED index {led} out of range 0-{ledCount - 1}.");
            }

            colors[led] = c.Color;
        }
        else
        {
            for (int i = 0; i < ledCount; i++)
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
                : $"{ledCount} LEDs = {c.Color.ToHex()}";
        _out.WriteLine($"Applied per-key custom mode ({detail})");
        return 0;
    }

    public int RunProfile(ProfileCommand c)
    {
        switch (c.Action)
        {
            case "list":
                foreach (string name in _profileService.List())
                {
                    _out.WriteLine(name);
                }

                return 0;

            case "save":
            {
                using IAulaKeyboard keyboard = OpenKeyboard(c.Model);
                var profile = KeyboardProfile.FromCurrent(c.Name!, keyboard);
                _profileService.Save(c.Name!, profile);
                _out.WriteLine($"Saved profile '{c.Name}' (effect {profile.Lighting.EffectId}).");
                return 0;
            }

            case "delete":
                return _profileService.Delete(c.Name!)
                    ? Print($"Deleted profile '{c.Name}'.")
                    : Print($"Profile '{c.Name}' not found.", error: true);

            case "apply":
            case "load":
            {
                using IAulaKeyboard keyboard = OpenKeyboard(c.Model);
                _profileService.Apply(c.Name!, keyboard);
                _out.WriteLine($"Applied profile '{c.Name}'.");
                return 0;
            }

            default:
                throw new AulaException($"Unsupported profile action '{c.Action}'.");
        }
    }

    public async Task<int> RunUpdate(UpdateCommand c)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        try
        {
            UpdateInfo info = await _updateService.CheckAsync(cts.Token);

            switch (c.Action)
            {
                case "check":
                    return PrintUpdateCheck(info);

                case "install":
                    if (!info.IsAvailable)
                    {
                        _out.WriteLine("You are up to date.");
                        return 0;
                    }

                    if (!c.Force)
                    {
                        _out.WriteLine(
                            $"New version {info.LatestVersion} available (current {info.CurrentVersion}). " +
                            "Run 'aula update install --force' to install.");
                        return 0;
                    }

                    _out.WriteLine($"Downloading {info.AssetName} ({info.LatestVersion})…");
                    string zip = await _updateService.DownloadToFileAsync(info, _updateInstaller.StagingDirectory, cts.Token);
                    await _updateInstaller.InstallAsync(zip, cts.Token);
                    _out.WriteLine("Update staged. Restarting to apply…");
                    return 0;

                default:
                    throw new AulaException($"Unsupported update action '{c.Action}'.");
            }
        }
        catch (OperationCanceledException)
        {
            _err.WriteLine("error: update check timed out (no network?).");
            return 1;
        }
        catch (HttpRequestException ex)
        {
            _err.WriteLine($"error: cannot reach GitHub: {ex.Message}");
            return 1;
        }
        catch (AulaException ex)
        {
            _err.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }

    public int PrintUpdateCheck(UpdateInfo info)
    {
        if (!info.IsAvailable)
        {
            _out.WriteLine($"AulaManager {info.CurrentVersion} is up to date.");
            return 0;
        }

        _out.WriteLine($"Version       : {info.CurrentVersion}");
        _out.WriteLine($"Latest        : {info.LatestVersion}");
        _out.WriteLine($"Published     : {info.PublishedAt?.LocalDateTime.ToString("yyyy-MM-dd HH:mm") ?? "-"}");
        _out.WriteLine($"Download      : {info.DownloadUrl}");
        _out.WriteLine();
        _out.WriteLine("Release notes:");
        _out.WriteLine(info.ReleaseNotes);
        return 0;
    }

    public int RunDump(DumpCommand c)
    {
        using IAulaKeyboard keyboard = OpenKeyboard(c.Model);
        _log.LogInformation("Dumping config (model {Model})", keyboard.Model.Id);
        KeyboardConfig config = keyboard.Lighting.ReadConfig();

        _out.WriteLine($"Effect      : {config.EffectId}");
        _out.WriteLine($"Custom mode : {config.CustomMode}");
        _out.WriteLine($"Side light  : {config.SideLightEffect}");
        _out.WriteLine($"Battery     : {config.BatteryLightEffect}");

        if (config.GetParams(config.EffectId) is { } p)
        {
            _out.WriteLine($"Brightness  : {p.Brightness}");
            _out.WriteLine($"Speed       : {p.Speed}");
            _out.WriteLine($"Colorful    : {p.Colorful}");
        }

        _out.WriteLine($"Raw ({config.Raw.Length} bytes):");
        _out.WriteLine(FormatHex(config.Raw));

        if (keyboard is ISinowealthDiagnostics diagnostics)
        {
            byte[] profile = diagnostics.ReadColorProfileRaw();
            _out.WriteLine($"Color profile ({profile.Length} bytes):");
            _out.WriteLine(FormatHex(profile));
        }

        return 0;
    }

    public IAulaKeyboard OpenKeyboard(string modelId) => _factory.Open(modelId);

    public int Print(string message, bool error = false)
    {
        (error ? _err : _out).WriteLine(message);
        return error ? 1 : 0;
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

    public int PrintHelp()
    {
        _out.WriteLine("""
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
              reset [--model ID]     Reset lighting config to factory defaults
              reset --vendor <path>  Run the official AULA reset tool (full restore)
                     [--model ID]
              perkey                Set per-key colors (custom mode)
                     [--color #RRGGBB]  fill base color (also: --color R G B)
                     [--fill-all]       fill all LEDs with base color
                     [--led N]          set single LED index
                     KEY=#RRGGBB ...    set colors per key name (e.g. w=ff0000 space=00ff00)
                     [--model ID]
              dump [--model ID]      Read and print current keyboard config
              profile list           List saved profiles
              profile save <name>    Save current lighting as a profile
              profile apply <name>   Apply a saved profile to the keyboard
                     [--model ID]
              profile load <name>    Alias for apply
              profile delete <name>  Delete a saved profile
              update check           Check GitHub for a new version
              update install         Download and install the new version
                     [--force]       apply immediately (skips confirmation)
              wireless scan          List 2.4G receiver collections
              wireless read          Read and print the 2.4G lighting config
              wireless effect <id>   Apply an effect over the 2.4G receiver
                     [--brightness N]   0-9
                     [--speed N]        0-4
                     [--color #RRGGBB]  single color (also: --color R G B)
                     [--colorful]       rainbow/colorful mode
              help                   Show this help

            Models: f75 (default), f87
            """);
        return 0;
    }
}
