using System.Globalization;
using Aula.Core.Logging;
using Microsoft.Extensions.Logging;

namespace Aula.Core.Tests;

[CollectionDefinition("AmbientLogging", DisableParallelization = true)]
public sealed class AmbientLoggingCollection
{
}

[Collection("AmbientLogging")]
public class AulaLoggingTests
{
    [Fact]
    public void Logger_ReturnsTypedLogger_FromConfiguredFactory()
    {
        var writer = new StringWriter();
        AulaLogging.Configure(new LoggerFactoryBuilder(writer).Build());

        ILogger<AulaLoggingTests> logger = AulaLogging.Logger<AulaLoggingTests>();
        logger.LogInformation("hello {Name}", "world");
        AulaLogging.Configure(null);

        Assert.Contains("AulaLoggingTests", writer.ToString());
        Assert.Contains("hello world", writer.ToString());
    }

    [Fact]
    public void Logger_WithStringCategory_UsesCategoryName()
    {
        var writer = new StringWriter();
        AulaLogging.Configure(new LoggerFactoryBuilder(writer).Build());

        AulaLogging.Logger("My.Named.Category").LogInformation("hi");
        AulaLogging.Configure(null);

        Assert.Contains("My.Named.Category", writer.ToString());
    }

    [Fact]
    public void Configure_Null_RestoresSilentDefault()
    {
        AulaLogging.Configure(null);

        var writer = new StringWriter();
        AulaLogging.Configure(new LoggerFactoryBuilder(writer).Build());
        AulaLogging.Configure(null);

        AulaLogging.Logger<AulaLoggingTests>().LogInformation("nope");

        Assert.Equal(string.Empty, writer.ToString());
    }

    private sealed class LoggerFactoryBuilder
    {
        private readonly TextWriter _writer;

        public LoggerFactoryBuilder(TextWriter writer) => _writer = writer;

        public ILoggerFactory Build() =>
            Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder
                .SetMinimumLevel(LogLevel.Trace)
                .AddProvider(new TextWriterLoggerProvider(_writer, LogLevel.Trace)));
    }
}

public class TextWriterLoggerTests
{
    [Fact]
    public void Log_WritesFormattedLine_WithLevelAndCategory()
    {
        var writer = new StringWriter();
        var logger = new TextWriterLogger(writer, "Cat.One", LogLevel.Information);

        logger.LogInformation("message {Value}", 42);

        string output = writer.ToString();
        Assert.Contains("[INF]", output);
        Assert.Contains("Cat.One", output);
        Assert.Contains("message 42", output);
    }

    [Fact]
    public void Log_BelowMinimumLevel_IsSuppressed()
    {
        var writer = new StringWriter();
        var logger = new TextWriterLogger(writer, "Cat", LogLevel.Warning);

        logger.LogDebug("hidden");
        logger.LogError("shown");

        string output = writer.ToString();
        Assert.DoesNotContain("hidden", output);
        Assert.Contains("shown", output);
    }

    [Fact]
    public void Log_NullCategoryIsNotFiltered()
    {
        var writer = new StringWriter();
        var logger = new TextWriterLogger(writer, "Cat", (category, level) => category is not null);

        logger.LogInformation("ok");

        Assert.Contains("ok", writer.ToString());
    }

    [Fact]
    public void Log_WithException_AppendsExceptionText()
    {
        var writer = new StringWriter();
        var logger = new TextWriterLogger(writer, "Cat", LogLevel.Information);

        logger.LogError(new InvalidOperationException("boom"), "failed");

        string output = writer.ToString();
        Assert.Contains("failed", output);
        Assert.Contains("InvalidOperationException", output);
        Assert.Contains("boom", output);
    }

    [Theory]
    [InlineData(LogLevel.Trace, "[TRC]")]
    [InlineData(LogLevel.Debug, "[DBG]")]
    [InlineData(LogLevel.Warning, "[WRN]")]
    [InlineData(LogLevel.Critical, "[CRT]")]
    public void Log_Levels_WriteMatchingLevelCode(LogLevel level, string code)
    {
        var writer = new StringWriter();
        var logger = new TextWriterLogger(writer, "Cat", LogLevel.Trace);

        logger.Log(level, "message");

        Assert.Contains(code, writer.ToString());
    }

    [Fact]
    public void Log_UnknownLevel_WritesFallbackCode()
    {
        var writer = new StringWriter();
        var logger = new TextWriterLogger(writer, "Cat", LogLevel.Trace);

        logger.Log((LogLevel)99, "message");

        Assert.Contains("[NON]", writer.ToString());
    }

    [Fact]
    public void IsEnabled_NoneLevel_ReturnsFalse()
    {
        var logger = new TextWriterLogger(new StringWriter(), "Cat", LogLevel.Trace);

        Assert.False(logger.IsEnabled(LogLevel.None));
    }

    [Fact]
    public void IsEnabled_LevelBelowMinimum_ReturnsFalse()
    {
        var logger = new TextWriterLogger(new StringWriter(), "Cat", LogLevel.Warning);

        Assert.False(logger.IsEnabled(LogLevel.Information));
    }

    [Fact]
    public void BeginScope_ReturnsNull()
    {
        var logger = new TextWriterLogger(new StringWriter(), "Cat", LogLevel.Trace);

        using IDisposable? scope = logger.BeginScope("state");

        Assert.Null(scope);
    }
}

public class TextWriterLoggerProviderTests
{
    [Fact]
    public void CreateLogger_ReturnsLogger_WritingToWriter()
    {
        var writer = new StringWriter();
        var provider = new TextWriterLoggerProvider(writer, LogLevel.Information);

        ILogger logger = provider.CreateLogger("Cat.Provider");

        logger.LogInformation("hello");

        Assert.Contains("hello", writer.ToString());
    }

    [Fact]
    public void FilterConstructor_AppliesPredicate()
    {
        var writer = new StringWriter();
        var provider = new TextWriterLoggerProvider(writer, (category, level) => category == "only-this");

        provider.CreateLogger("other").LogInformation("no");
        provider.CreateLogger("only-this").LogInformation("yes");

        string output = writer.ToString();
        Assert.DoesNotContain("no", output);
        Assert.Contains("yes", output);
    }

    [Fact]
    public void Dispose_FlushesPendingOutput()
    {
        var writer = new StringWriter();
        var provider = new TextWriterLoggerProvider(writer, LogLevel.Information);

        provider.CreateLogger("Cat").LogInformation("buffered");
        provider.Dispose();

        Assert.Contains("buffered", writer.ToString());
    }
}
