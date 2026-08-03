using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aula.Core.Logging;

/// <summary>
/// Ambient logger factory for the application. Core services resolve loggers
/// through this holder, so they stay free of hard-wired sinks. Tests run with
/// a silent null factory unless they configure their own.
/// </summary>
public static class AulaLogging
{
    private static ILoggerFactory _factory = NullLoggerFactory.Instance;
    private static readonly object Gate = new();

    /// <summary>Gets the active logger factory (never null).</summary>
    public static ILoggerFactory Factory
    {
        get
        {
            lock (Gate)
            {
                return _factory;
            }
        }
    }

    /// <summary>Replaces the active logger factory. Pass null to restore the silent default.</summary>
    public static void Configure(ILoggerFactory? factory)
    {
        lock (Gate)
        {
            _factory = factory ?? NullLoggerFactory.Instance;
        }
    }

    /// <summary>Returns a typed logger for <typeparamref name="T"/>.</summary>
    public static ILogger<T> Logger<T>() => Factory.CreateLogger<T>();

    /// <summary>Returns a logger for the given category name.</summary>
    public static ILogger Logger(string categoryName) => Factory.CreateLogger(categoryName);
}
