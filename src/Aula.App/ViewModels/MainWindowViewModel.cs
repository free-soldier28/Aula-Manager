using CommunityToolkit.Mvvm.ComponentModel;
using Aula.App.Services;

namespace Aula.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly KeyboardSession _session = new();

    public DeviceViewModel Device { get; }
    public LightingViewModel Lighting { get; }
    public ProfilesViewModel Profiles { get; }

    public MainWindowViewModel()
    {
        Device = new DeviceViewModel(_session);
        Lighting = new LightingViewModel(_session);
        Profiles = new ProfilesViewModel(_session);
        _session.Changed += OnSessionChanged;
    }

    [ObservableProperty]
    private int _selectedTab;

    private void OnSessionChanged()
    {
        Lighting.RefreshFromDevice();
        Profiles.RefreshList();
    }
}
