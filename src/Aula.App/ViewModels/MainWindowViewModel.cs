using CommunityToolkit.Mvvm.ComponentModel;
using Aula.App.Services;

namespace Aula.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly KeyboardSession _session = new();

    public DeviceViewModel Device { get; }
    public LightingViewModel Lighting { get; }
    public PerKeyViewModel PerKey { get; }
    public ProfilesViewModel Profiles { get; }
    public UpdateViewModel Update { get; }

    public MainWindowViewModel()
    {
        Device = new DeviceViewModel(_session);
        Lighting = new LightingViewModel(_session);
        PerKey = new PerKeyViewModel(_session);
        Profiles = new ProfilesViewModel(_session);
        Update = new UpdateViewModel();
        _session.Changed += OnSessionChanged;
        _session.Open();
    }

    [ObservableProperty]
    private int _selectedTab;

    [ObservableProperty]
    private bool _isDeviceConnected;

    private void OnSessionChanged()
    {
        IsDeviceConnected = _session.IsConnected;
        Lighting.RefreshFromDevice();
        PerKey.RefreshFromDevice();
        Profiles.RefreshList();
    }
}
