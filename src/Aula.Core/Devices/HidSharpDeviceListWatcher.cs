using HidSharp;

namespace Aula.Core.Devices;

/// <summary>Default <see cref="IHidDeviceListWatcher"/> backed by HidSharp's <c>DeviceList.Local</c>.</summary>
public sealed class HidSharpDeviceListWatcher : IHidDeviceListWatcher
{
    public event EventHandler? DeviceListChanged;

    public void Start() => DeviceList.Local.Changed += OnDeviceListChanged;

    public void Stop() => DeviceList.Local.Changed -= OnDeviceListChanged;

    public void Dispose() => Stop();

    private void OnDeviceListChanged(object? sender, DeviceListChangedEventArgs e) =>
        DeviceListChanged?.Invoke(this, EventArgs.Empty);
}
