using Aula.Core.Models;

namespace Aula.Cli;

public sealed class CliParseException : Exception
{
    public CliParseException(string message) : base(message)
    {
    }
}

public abstract record CliCommand;

public sealed record ListCommand : CliCommand;

public sealed record InfoCommand(string Model) : CliCommand;

public sealed record EffectsCommand(string Model) : CliCommand;

public sealed record EffectCommand(
    string Model,
    int EffectId,
    int? Brightness,
    int? Speed,
    RgbColor? Color,
    bool Colorful,
    byte? RawFlags = null) : CliCommand;

public sealed record OffCommand(string Model) : CliCommand;

public sealed record ResetCommand(string Model, string? VendorPath = null) : CliCommand;

public sealed record DumpCommand(string Model) : CliCommand;

public sealed record PerKeyCommand(
    string Model,
    RgbColor Color,
    bool FillAll = false,
    int? LedIndex = null,
    IReadOnlyDictionary<string, RgbColor>? KeyColors = null) : CliCommand;

public sealed record ProfileCommand(
    string Action,
    string? Name = null,
    string Model = "f75",
    RgbColor? Color = null,
    bool Colorful = false,
    IReadOnlyDictionary<string, RgbColor>? KeyColors = null) : CliCommand;

public sealed record UpdateCommand(string Action, bool Force = false) : CliCommand;

public sealed record WirelessCommand(string Action, WirelessEffectCommand? Effect = null) : CliCommand;

public sealed record WirelessEffectCommand(
    int EffectId,
    int? Brightness = null,
    int? Speed = null,
    RgbColor? Color = null,
    bool Colorful = false) : CliCommand;

public sealed record HelpCommand : CliCommand;

public static class CliCommandParser
{
    public static CliCommand Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return new HelpCommand();
        }

        string verb = args[0].ToLowerInvariant();
        string[] rest = args[1..];

        switch (verb)
        {
            case "help":
            case "--help":
            case "-h":
                return new HelpCommand();

            case "list":
            case "ls":
                return new ListCommand();

            case "info":
                return new InfoCommand(GetModel(rest));

            case "effects":
                return new EffectsCommand(GetModel(rest));

            case "dump":
                return new DumpCommand(GetModel(rest));

            case "perkey":
                return ParsePerKey(rest);

            case "profile":
                return ParseProfile(rest);

            case "update":
                return ParseUpdate(rest);

            case "wireless":
                return ParseWireless(rest);

            case "off":
                return new OffCommand(GetModel(rest));

            case "reset":
                return ParseReset(rest);

            case "effect":
                return ParseEffect(rest);

            default:
                throw new CliParseException($"Unknown command '{verb}'. Run 'aula help' for usage.");
        }
    }

    private static WirelessCommand ParseWireless(string[] args)
    {
        if (args.Length == 0)
        {
            return new WirelessCommand("read");
        }

        string action = args[0].ToLowerInvariant();
        switch (action)
        {
            case "scan":
                return new WirelessCommand("scan");
            case "read":
                return new WirelessCommand("read");
            case "effect":
                return ParseWirelessEffect(args[1..]);
            default:
                throw new CliParseException($"Unknown wireless action '{action}'. Use scan, read or effect.");
        }
    }

    private static WirelessCommand ParseWirelessEffect(string[] args)
    {
        if (args.Length == 0)
        {
            throw new CliParseException("Usage: aula wireless effect <name|id> [options]");
        }

        int effectId = ResolveEffectId(args[0]);
        string[] rest = args[1..];

        int? brightness = null;
        int? speed = null;
        RgbColor? color = null;
        bool colorful = false;

        for (int i = 0; i < rest.Length; i++)
        {
            string arg = rest[i];
            switch (arg.ToLowerInvariant())
            {
                case "--brightness":
                case "-b":
                    brightness = ParseInt(rest, ref i, arg);
                    break;
                case "--speed":
                case "-s":
                    speed = ParseInt(rest, ref i, arg);
                    break;
                case "--color":
                case "-c":
                    color = ParseColor(rest, ref i);
                    break;
                case "--colorful":
                    colorful = true;
                    break;
                default:
                    throw new CliParseException($"Unknown option '{arg}'.");
            }
        }

        return new WirelessCommand("effect", new WirelessEffectCommand(effectId, brightness, speed, color, colorful));
    }

    private static ResetCommand ParseReset(string[] args)
    {
        string model = "f75";
        string? vendorPath = null;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg.ToLowerInvariant())
            {
                case "--model":
                case "-m":
                    model = Next(args, ref i, arg);
                    break;
                case "--vendor":
                    vendorPath = Next(args, ref i, arg);
                    break;
                default:
                    throw new CliParseException($"Unknown option '{arg}' for reset.");
            }
        }

        return new ResetCommand(model, vendorPath);
    }

    private static EffectCommand ParseEffect(string[] args)
    {
        if (args.Length == 0)
        {
            throw new CliParseException("Usage: aula effect <name|id> [options]");
        }

        string effectRef = args[0];
        string[] rest = args[1..];

        int effectId = ResolveEffectId(effectRef);
        int? brightness = null;
        int? speed = null;
        RgbColor? color = null;
        bool colorful = false;
        byte? rawFlags = null;
        string model = "f75";

        for (int i = 0; i < rest.Length; i++)
        {
            string arg = rest[i];
            switch (arg.ToLowerInvariant())
            {
                case "--brightness":
                case "-b":
                    brightness = ParseInt(rest, ref i, arg);
                    break;
                case "--speed":
                case "-s":
                    speed = ParseInt(rest, ref i, arg);
                    break;
                case "--colorful":
                    colorful = true;
                    break;
                case "--color":
                case "-c":
                    color = ParseColor(rest, ref i);
                    break;
                case "--raw-flags":
                    rawFlags = ParseByte(rest, ref i, arg);
                    break;
                case "--model":
                case "-m":
                    model = Next(rest, ref i, arg);
                    break;
                default:
                    throw new CliParseException($"Unknown option '{arg}'.");
            }
        }

        return new EffectCommand(model, effectId, brightness, speed, color, colorful, rawFlags);
    }

    private static string GetModel(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] is "--model" or "-m")
            {
                return Next(args, ref i, args[i]);
            }
        }

        return "f75";
    }

    private static ProfileCommand ParseProfile(string[] args)
    {
        if (args.Length == 0)
        {
            throw new CliParseException("Usage: aula profile <save|load|apply|list|delete> <name> [options]");
        }

        string action = args[0].ToLowerInvariant();
        if (action is not ("save" or "load" or "apply" or "list" or "delete"))
        {
            throw new CliParseException(
                $"Unknown profile action '{action}'. Use save, load, apply, list or delete.");
        }

        string? name = null;
        string model = "f75";
        RgbColor? color = null;
        bool colorful = false;
        var keyColors = new Dictionary<string, RgbColor>();

        string[] rest = args[1..];
        for (int i = 0; i < rest.Length; i++)
        {
            string arg = rest[i];
            string lowered = arg.ToLowerInvariant();
            switch (lowered)
            {
                case "--model":
                case "-m":
                    model = Next(rest, ref i, arg);
                    break;
                case "--color":
                case "-c":
                    color = ParseColor(rest, ref i);
                    break;
                case "--colorful":
                    colorful = true;
                    break;
                default:
                    if (name is null && !lowered.StartsWith('-'))
                    {
                        name = arg;
                    }
                    else if (TryParseKeyColor(arg, out string key, out RgbColor keyColor))
                    {
                        keyColors[key] = keyColor;
                    }
                    else
                    {
                        throw new CliParseException($"Unknown option '{arg}'.");
                    }

                    break;
            }
        }

        if (action is "save" or "load" or "apply" or "delete" && name is null)
        {
            throw new CliParseException($"Usage: aula profile {action} <name> [options]");
        }

        return new ProfileCommand(action, name, model, color, colorful, keyColors);
    }

    private static UpdateCommand ParseUpdate(string[] args)
    {
        if (args.Length == 0)
        {
            throw new CliParseException("Usage: aula update <check|install> [--force]");
        }

        string action = args[0].ToLowerInvariant();
        if (action is not ("check" or "install"))
        {
            throw new CliParseException($"Unknown update action '{action}'. Use check or install.");
        }

        bool force = args.Contains("--force", StringComparer.OrdinalIgnoreCase);
        return new UpdateCommand(action, force);
    }

    private static PerKeyCommand ParsePerKey(string[] args)
    {
        if (args.Length == 0)
        {
            throw new CliParseException("Usage: aula perkey [--color R G B | --color #RRGGBB] [--fill-all] [--model ID]");
        }

        RgbColor? color = null;
        bool fillAll = false;
        int? ledIndex = null;
        string model = "f75";
        var keyColors = new Dictionary<string, RgbColor>();

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            string lowered = arg.ToLowerInvariant();
            switch (lowered)
            {
                case "--color":
                case "-c":
                    color = ParseColor(args, ref i);
                    break;
                case "--fill-all":
                    fillAll = true;
                    break;
                case "--led":
                    ledIndex = ParseInt(args, ref i, arg);
                    break;
                case "--model":
                case "-m":
                    model = Next(args, ref i, arg);
                    break;
                default:
                    if (TryParseKeyColor(arg, out string key, out RgbColor keyColor))
                    {
                        keyColors[key] = keyColor;
                    }
                    else
                    {
                        throw new CliParseException($"Unknown option '{arg}'.");
                    }

                    break;
            }
        }

        if (keyColors.Count > 0)
        {
            return new PerKeyCommand(model, color ?? RgbColor.FromRgb(255, 255, 255), fillAll, ledIndex, keyColors);
        }

        return new PerKeyCommand(model, color ?? RgbColor.FromRgb(255, 255, 255), fillAll, ledIndex);
    }

    private static bool TryParseKeyColor(string arg, out string key, out RgbColor color)
    {
        int eq = arg.IndexOf('=');
        if (eq <= 0 || eq == arg.Length - 1)
        {
            key = string.Empty;
            color = default;
            return false;
        }

        key = arg[..eq];
        string hex = arg[(eq + 1)..];
        if (hex.StartsWith('#'))
        {
            hex = hex[1..];
        }

        if (hex.Length == 6 && hex.All(Uri.IsHexDigit))
        {
            try
            {
                color = RgbColor.FromHex("#" + hex);
                return true;
            }
            catch (FormatException)
            {
                // fall through to false
            }
        }

        key = string.Empty;
        color = default;
        return false;
    }

    private static int ResolveEffectId(string effectRef)
    {
        if (int.TryParse(effectRef, out int id))
        {
            if (EffectLibrary.FindById(id) is not null)
            {
                return id;
            }

            throw new CliParseException($"Unknown effect id '{id}'. Run 'aula effects' to list effects.");
        }

        return EffectLibrary.FindByName(effectRef)?.Id
            ?? throw new CliParseException($"Unknown effect '{effectRef}'. Run 'aula effects' to list effects.");
    }

    private static int ParseInt(string[] args, ref int i, string flag)
    {
        string value = Next(args, ref i, flag);
        if (!int.TryParse(value, out int result))
        {
            throw new CliParseException($"Invalid number '{value}' for {flag}.");
        }

        return result;
    }

    private static byte ParseByte(string[] args, ref int i, string flag)
    {
        string value = Next(args, ref i, flag);
        string hex = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value[2..] : value;
        if (!byte.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out byte result))
        {
            throw new CliParseException($"Invalid byte '{value}' for {flag}. Use hex, e.g. 0x20.");
        }

        return result;
    }

    private static RgbColor ParseColor(string[] args, ref int i)
    {
        string value = Next(args, ref i, "--color");
        if (value.StartsWith('#'))
        {
            try
            {
                return RgbColor.FromHex(value);
            }
            catch (FormatException ex)
            {
                throw new CliParseException(ex.Message);
            }
        }

        if (value.Length == 6 && value.All(Uri.IsHexDigit))
        {
            return RgbColor.FromHex("#" + value);
        }

        if (int.TryParse(value, out int r) &&
            i + 2 < args.Length &&
            int.TryParse(args[i + 1], out int g) &&
            int.TryParse(args[i + 2], out int b))
        {
            i += 2;
            return RgbColor.FromRgb(r, g, b);
        }

        throw new CliParseException($"Invalid color '{value}'. Use --color #RRGGBB, --color RRGGBB or --color R G B.");
    }

    private static string Next(string[] args, ref int i, string flag)
    {
        if (i + 1 >= args.Length)
        {
            throw new CliParseException($"Missing value for {flag}.");
        }

        return args[++i];
    }
}
