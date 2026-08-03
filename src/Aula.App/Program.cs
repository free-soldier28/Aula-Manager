using Avalonia;
using Aula.Core.Logging;
using Microsoft.Extensions.Logging;

namespace Aula.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        ConfigureFileLogging();
        App.BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    private static void ConfigureFileLogging()
    {
        try
        {
            string logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".aula",
                "logs");
            Directory.CreateDirectory(logDir);
            string logFile = Path.Combine(logDir, $"aula-{DateTime.Now:yyyyMMdd}.log");
            var writer = new StreamWriter(logFile, append: true) { AutoFlush = true };
            LogLevel level = ParseLevel(Environment.GetEnvironmentVariable("AULA_LOG_LEVEL"));
            var factory = LoggerFactory.Create(builder => builder
                .SetMinimumLevel(level)
                .AddProvider(new TextWriterLoggerProvider(writer, level)));
            AulaLogging.Configure(factory);
            AulaLogging.Logger("Aula.App").LogInformation("App starting (version {Version})", Aula.Core.Models.ProductInfo.VersionString);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error: cannot initialize logging: {ex.Message}");
        }
    }

    private static LogLevel ParseLevel(string? value)
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
}
