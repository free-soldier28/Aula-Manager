using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Aula.Core.Models;
using Aula.Core.Updating;

namespace Aula.App.ViewModels;

public partial class UpdateViewModel : ObservableObject
{
    private readonly UpdateService _service;

    [ObservableProperty]
    private string _currentVersion = ProductInfo.VersionString;

    [ObservableProperty]
    private string _status = "Checking for updates…";

    [ObservableProperty]
    private bool _isChecking;

    [ObservableProperty]
    private bool _hasUpdate;

    [ObservableProperty]
    private bool _isInstalling;

    [ObservableProperty]
    private string _releaseNotes = string.Empty;

    public UpdateViewModel(UpdateService? service = null)
    {
        _service = service ?? new UpdateService();
    }

    [RelayCommand]
    private async Task CheckAsync()
    {
        IsChecking = true;
        HasUpdate = false;
        Status = "Checking for updates…";
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            UpdateInfo info = await _service.CheckAsync(cts.Token);
            if (info.IsAvailable)
            {
                HasUpdate = true;
                Status = $"New version {info.LatestVersion} is available (current {info.CurrentVersion}).";
                ReleaseNotes = info.ReleaseNotes ?? string.Empty;
            }
            else
            {
                Status = $"You are up to date (v{info.CurrentVersion}).";
                ReleaseNotes = string.Empty;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            Status = $"Could not check for updates: {ex.Message}";
        }
        finally
        {
            IsChecking = false;
        }
    }

    [RelayCommand]
    private async Task InstallAsync()
    {
        if (!HasUpdate)
        {
            return;
        }

        IsInstalling = true;
        Status = "Downloading update…";
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            UpdateInfo info = await _service.CheckAsync(cts.Token);
            if (!info.IsAvailable)
            {
                Status = "Update is no longer available.";
                HasUpdate = false;
                return;
            }

            var installer = new UpdateInstaller();
            string zip = await _service.DownloadToFileAsync(info, installer.StagingDirectory, cts.Token);
            await installer.InstallAsync(zip, cts.Token);
            Status = "Update staged. The app will restart to apply it.";
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            Status = $"Install failed: {ex.Message}";
        }
        finally
        {
            IsInstalling = false;
        }
    }
}
