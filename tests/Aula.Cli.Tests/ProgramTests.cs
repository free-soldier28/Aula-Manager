using Aula.Core.Drivers;
using Aula.Core.Services;

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
}
