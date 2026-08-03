using Aula.Core;
using Aula.Core.Logging;
using Aula.Core.Updating;
using Microsoft.Extensions.Logging;

namespace Aula.Cli;

public static class Program
{
    public static int Main(string[] args)
    {
        ConfigureLogging();
        return Run(args, new CliRunner());
    }

    public static int Run(string[] args, CliRunner runner)
    {
        try
        {
            return runner.Run(args);
        }
        catch (CliParseException ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            Console.Error.WriteLine("Run 'aula help' for usage.");
            return 2;
        }
        catch (AulaDeviceNotFoundException ex)
        {
            Log.Error(ex);
            Console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }
        catch (AulaException ex)
        {
            Log.Error(ex);
            Console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            Log.Critical(ex);
            Console.Error.WriteLine($"error: unexpected failure: {ex.Message}");
            return 1;
        }
    }

    private static void ConfigureLogging()
    {
        LogLevel level = ParseLevel(Environment.GetEnvironmentVariable("AULA_LOG_LEVEL"));
        var factory = LoggerFactory.Create(builder => builder
            .SetMinimumLevel(level)
            .AddProvider(new TextWriterLoggerProvider(Console.Error, level)));
        AulaLogging.Configure(factory);
    }

    internal static LogLevel ParseLevel(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "trace" => LogLevel.Trace,
            "debug" => LogLevel.Debug,
            "info" or "information" => LogLevel.Information,
            "warn" or "warning" => LogLevel.Warning,
            "error" => LogLevel.Error,
            "critical" or "fatal" => LogLevel.Critical,
            "none" or "off" => LogLevel.None,
            _ => LogLevel.Information,
        };
    }

    private static class Log
    {
        public static void Error(Exception ex) =>
            AulaLogging.Logger("Aula.Cli.Program").LogError(ex, "Command failed: {Message}", ex.Message);

        public static void Critical(Exception ex) =>
            AulaLogging.Logger("Aula.Cli.Program").LogCritical(ex, "Unexpected failure: {Message}", ex.Message);
    }
}
