using Microsoft.Extensions.Logging;

namespace Aula.Core.Logging;

/// <summary>
/// Formats log events as single-line records and writes them to a
/// <see cref="TextWriter"/>. Shared by the CLI (stderr) and the GUI (file),
/// so both front-ends get the same compact, greppable format.
/// </summary>
public sealed class TextWriterLogger : ILogger
{
    private readonly TextWriter _writer;
    private readonly string _categoryName;
    private readonly Func<string, LogLevel, bool> _filter;
    private readonly object _gate = new();

    public TextWriterLogger(
        TextWriter writer,
        string categoryName,
        LogLevel minimumLevel = LogLevel.Information)
        : this(writer, categoryName, (_, level) => level >= minimumLevel)
    {
    }

    public TextWriterLogger(
        TextWriter writer,
        string categoryName,
        Func<string, LogLevel, bool> filter)
    {
        _writer = writer;
        _categoryName = categoryName;
        _filter = filter;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) =>
        logLevel != LogLevel.None && _filter(_categoryName, logLevel);

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        string message = formatter(state, exception);
        string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{LogLevelCode(logLevel)}] {_categoryName}: {message}";

        if (exception is not null)
        {
            line += Environment.NewLine + exception;
        }

        lock (_gate)
        {
            _writer.WriteLine(line);
            _writer.Flush();
        }
    }

    private static string LogLevelCode(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRC",
        LogLevel.Debug => "DBG",
        LogLevel.Information => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error => "ERR",
        LogLevel.Critical => "CRT",
        _ => "NON",
    };
}
