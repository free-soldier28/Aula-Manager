using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Aula.App.ViewModels;
using Aula.App.Views;
using Aula.Core.Updating;

namespace Aula.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
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

