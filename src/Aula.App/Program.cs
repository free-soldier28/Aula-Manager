using Avalonia;

namespace Aula.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) => App.BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);
}
