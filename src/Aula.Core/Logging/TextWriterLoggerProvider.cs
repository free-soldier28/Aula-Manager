using Microsoft.Extensions.Logging;

namespace Aula.Core.Logging;

/// <summary>
/// Wraps a <see cref="TextWriterLogger"/> as an <see cref="ILoggerProvider"/>
/// so it can be registered with a standard <see cref="LoggerFactory"/>.
/// </summary>
public sealed class TextWriterLoggerProvider : ILoggerProvider
{
    private readonly TextWriter _writer;
    private readonly Func<string, LogLevel, bool> _filter;

    public TextWriterLoggerProvider(
        TextWriter writer,
        LogLevel minimumLevel = LogLevel.Information)
        : this(writer, (_, level) => level >= minimumLevel)
    {
    }

    public TextWriterLoggerProvider(
        TextWriter writer,
        Func<string, LogLevel, bool> filter)
    {
        _writer = writer;
        _filter = filter;
    }

    public ILogger CreateLogger(string categoryName) =>
        new TextWriterLogger(_writer, categoryName, _filter);

    public void Dispose()
    {
        _writer.Flush();
    }
}
