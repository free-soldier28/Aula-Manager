using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Aula.App.ViewModels;
using Aula.App.Views;
using Aula.Core.Logging;
using Aula.Core.Updating;
using Microsoft.Extensions.Logging;

namespace Aula.App;

public partial class App : Application
{
    private static readonly ILogger<App> Log = AulaLogging.Logger<App>();

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            DisableAvaloniaDataAnnotationValidation();
            var mainViewModel = new MainWindowViewModel();
            desktop.MainWindow = new MainWindow
            {
                DataContext = mainViewModel,
            };
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            CheckForUpdateOnStartup(desktop, mainViewModel);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Log.LogCritical(e.ExceptionObject as Exception ?? new Exception("Unhandled exception"), "Unhandled application exception");
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.LogError(e.Exception, "Unobserved task exception");
        e.SetObserved();
    }

    private static async void CheckForUpdateOnStartup(
        IClassicDesktopStyleApplicationLifetime desktop,
        MainWindowViewModel viewModel)
    {
        await viewModel.Update.CheckCommand.ExecuteAsync(null);
        if (!viewModel.Update.HasUpdate || desktop.MainWindow is null)
        {
            return;
        }

        var dialog = new UpdateWindow
        {
            DataContext = viewModel.Update,
        };
        await dialog.ShowDialog(desktop.MainWindow);
    }

    private static void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove = BindingPlugins.DataValidators.ToList();
        foreach (var item in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(item);
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}

