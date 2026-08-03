using Aula.Core.Drivers;
using Aula.Core.Services;
using Microsoft.Extensions.Logging;

namespace Aula.Cli.Tests;

public class ProgramTests
{
    [Fact]
    public void Main_Help_ReturnsZero()
    {
        int result = Program.Main(new[] { "help" });

        Assert.Equal(0, result);
    }

    [Fact]
    public void Main_UnknownCommand_ReturnsTwo()
    {
        int result = Program.Main(new[] { "bogus-command" });

        Assert.Equal(2, result);
    }

    [Fact]
    public void Main_NoArguments_ReturnsZero()
    {
        int result = Program.Main(Array.Empty<string>());

        Assert.Equal(0, result);
    }

    [Fact]
    public void Run_DeviceNotFound_ReturnsOne()
    {
        var scanner = new FakeScanner();
        var runner = new CliRunner(
            scanner: scanner,
            factory: new KeyboardDeviceFactory(scanner, new DriverRegistry()));

        int result = Program.Run(new[] { "effect", "wave" }, runner);

        Assert.Equal(1, result);
    }

    [Fact]
    public void Run_AulaException_ReturnsOne()
    {
        var scanner = new FakeScanner();
        var runner = new CliRunner(
            scanner: scanner,
            factory: new KeyboardDeviceFactory(scanner, new DriverRegistry()));

        int result = Program.Run(new[] { "wireless", "effect", "3" }, runner);

        Assert.Equal(1, result);
    }

    [Fact]
    public void Run_UnexpectedException_ReturnsOne()
    {
        var scanner = new ThrowingScanner();
        var runner = new CliRunner(
            scanner: scanner,
            factory: new KeyboardDeviceFactory(scanner, new DriverRegistry()));

        int result = Program.Run(new[] { "list" }, runner);

        Assert.Equal(1, result);
    }

    [Theory]
    [InlineData("trace", LogLevel.Trace)]
    [InlineData("debug", LogLevel.Debug)]
    [InlineData("info", LogLevel.Information)]
    [InlineData("information", LogLevel.Information)]
    [InlineData("warn", LogLevel.Warning)]
    [InlineData("warning", LogLevel.Warning)]
    [InlineData("error", LogLevel.Error)]
    [InlineData("critical", LogLevel.Critical)]
    [InlineData("fatal", LogLevel.Critical)]
    [InlineData("none", LogLevel.None)]
    [InlineData("off", LogLevel.None)]
    [InlineData("TRACE", LogLevel.Trace)]
    public void ParseLevel_ValidValues(string value, LogLevel expected)
    {
        Assert.Equal(expected, Program.ParseLevel(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("verbose")]
    public void ParseLevel_UnknownOrBlank_FallsBackToInformation(string? value)
    {
        Assert.Equal(LogLevel.Information, Program.ParseLevel(value));
    }
}
