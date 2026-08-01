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
    bool Colorful) : CliCommand;

public sealed record OffCommand(string Model) : CliCommand;

public sealed record DumpCommand(string Model) : CliCommand;

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

            case "off":
                return new OffCommand(GetModel(rest));

            case "effect":
                return ParseEffect(rest);

            default:
                throw new CliParseException($"Unknown command '{verb}'. Run 'aula help' for usage.");
        }
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
                case "--model":
                case "-m":
                    model = Next(rest, ref i, arg);
                    break;
                default:
                    throw new CliParseException($"Unknown option '{arg}'.");
            }
        }

        return new EffectCommand(model, effectId, brightness, speed, color, colorful);
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

        if (int.TryParse(value, out int r) &&
            i + 2 < args.Length &&
            int.TryParse(args[i + 1], out int g) &&
            int.TryParse(args[i + 2], out int b))
        {
            i += 2;
            return RgbColor.FromRgb(r, g, b);
        }

        throw new CliParseException($"Invalid color '{value}'. Use --color #RRGGBB or --color R G B.");
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
