using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Aula.App.Services;
using Aula.Core.Models;

namespace Aula.App.ViewModels;

public partial class ProfilesViewModel : ObservableObject
{
    private readonly KeyboardSession _session;
    private readonly ProfileService _profiles = new();

    [ObservableProperty]
    private IReadOnlyList<string> _profilesList = Array.Empty<string>();

    [ObservableProperty]
    private string? _selectedProfile;

    [ObservableProperty]
    private string _newProfileName = string.Empty;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _message = string.Empty;

    public ProfilesViewModel(KeyboardSession session)
    {
        _session = session;
        _session.Changed += OnSessionChanged;
    }

    public void RefreshList()
    {
        IsConnected = _session.IsConnected;
        ProfilesList = _profiles.List();
    }

    private void OnSessionChanged() => RefreshList();

    partial void OnNewProfileNameChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        SelectedProfile = value;
    }

    [RelayCommand]
    private void Refresh() => RefreshList();

    [RelayCommand]
    private void Save()
    {
        if (_session.Current is not { } keyboard)
        {
            Message = "No device connected.";
            return;
        }

        string name = NewProfileName.Trim();
        if (name.Length == 0)
        {
            Message = "Enter a profile name.";
            return;
        }

        try
        {
            _profiles.Save(name, KeyboardProfile.FromCurrent(name, keyboard));
            RefreshList();
            SelectedProfile = name;
            Message = $"Saved profile '{name}'.";
        }
        catch (Exception ex)
        {
            Message = $"Failed to save profile: {ex.Message}";
        }
    }

    [RelayCommand]
    private void Apply()
    {
        if (_session.Current is null)
        {
            Message = "No device connected.";
            return;
        }

        if (SelectedProfile is null)
        {
            Message = "Select a profile first.";
            return;
        }

        try
        {
            _profiles.Apply(SelectedProfile, _session.Current);
            Message = $"Applied profile '{SelectedProfile}'.";
        }
        catch (Exception ex)
        {
            Message = $"Failed to apply profile: {ex.Message}";
        }
    }

    [RelayCommand]
    private void Delete()
    {
        if (SelectedProfile is null)
        {
            Message = "Select a profile first.";
            return;
        }

        try
        {
            _profiles.Delete(SelectedProfile);
            RefreshList();
            Message = $"Deleted profile '{SelectedProfile}'.";
        }
        catch (Exception ex)
        {
            Message = $"Failed to delete profile: {ex.Message}";
        }
    }
}
