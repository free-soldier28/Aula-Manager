using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Aula.App.Services;
using Aula.Core.Abstractions;
using Aula.Core.Devices;

namespace Aula.App.ViewModels;

public partial class DeviceViewModel : ObservableObject
{
    private readonly KeyboardSession _session;
    private readonly HidDeviceScanner _scanner = new();

    [ObservableProperty]
    private string _status = "Scanning for devices…";

    [ObservableProperty]
    private string _deviceName = "-";

    [ObservableProperty]
    private string _vidPid = "-";

    [ObservableProperty]
    private string _serial = "-";

    [ObservableProperty]
    private string _modelId = "-";

    [ObservableProperty]
    private string _modelRaw = "-";

    [ObservableProperty]
    private bool _isConnected;

    public DeviceViewModel(KeyboardSession session)
    {
        _session = session;
        _session.Changed += RefreshFromDevice;
    }

    [RelayCommand]
    private void Refresh()
    {
        _session.Refresh();
        RefreshFromDevice();
    }

    private void RefreshFromDevice()
    {
        IAulaKeyboard? keyboard = _session.Current;
        IsConnected = keyboard is not null;

        if (keyboard is null)
        {
            Status = "No AULA keyboard detected.";
            DeviceName = VidPid = Serial = ModelId = ModelRaw = "-";
            return;
        }

        DeviceInfo info = keyboard.Info;
        Status = "Connected";
        DeviceName = info.DisplayName ?? "-";
        VidPid = $"{info.VendorId:X4}:{info.ProductId:X4}";
        Serial = info.SerialNumber ?? "-";
        ModelId = keyboard.Model.Id;

        ModelRaw = keyboard is ISinowealthDiagnostics d
            ? Convert.ToHexString(d.QueryModel())
            : "-";
    }
}
