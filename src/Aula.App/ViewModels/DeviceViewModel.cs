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

    [ObservableProperty]
    private string _connectionType = "-";

    public DeviceViewModel(KeyboardSession session)
    {
        _session = session;
        _session.Changed += RefreshFromDevice;
    }

    [RelayCommand]
    private void Refresh()
    {
        try
        {
            _session.Refresh();
            RefreshFromDevice();
        }
        catch (Exception ex)
        {
            Status = "Refresh failed: " + ex.Message;
        }
    }

    private void RefreshFromDevice()
    {
        IAulaKeyboard? keyboard = _session.Current;
        IsConnected = keyboard is not null;

        if (keyboard is null)
        {
            Status = _session.Error ?? "No AULA keyboard detected.";
            DeviceName = VidPid = Serial = ModelId = ModelRaw = ConnectionType = "-";
            return;
        }

        DeviceInfo info = keyboard.Info;
        Status = "Connected";
        DeviceName = string.IsNullOrWhiteSpace(info.DisplayName) ? "n/a" : info.DisplayName;
        VidPid = $"{info.VendorId:X4}:{info.ProductId:X4}";
        Serial = string.IsNullOrWhiteSpace(info.SerialNumber) ? "n/a" : info.SerialNumber;
        ModelId = keyboard.Model.Id;
        ConnectionType = ResolveConnectionType(info);

        try
        {
            ModelRaw = keyboard is ISinowealthDiagnostics d
                ? Convert.ToHexString(d.QueryModel())
                : "-";
        }
        catch (Exception ex)
        {
            ModelRaw = "error: " + ex.Message;
        }
    }

    private static string ResolveConnectionType(DeviceInfo info)
    {
        if (info.VendorId == AulaDeviceIds.VendorSinoWealth
            && info.ProductId == AulaDeviceIds.ProductF75F87Wired)
        {
            return "Wired (USB)";
        }

        if (info.VendorId == AulaDeviceIds.VendorWireless)
        {
            return info.ProductId == AulaDeviceIds.ProductWireless
                ? "Wireless (2.4 GHz)"
                : "Bluetooth";
        }

        return "Unknown";
    }
}
