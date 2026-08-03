using Aula.Core;
using Aula.Core.Updating;

namespace Aula.Cli;

public static class Program
{
    public static int Main(string[] args) => Run(args, new CliRunner());

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
            Console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }
        catch (AulaException ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }
}
