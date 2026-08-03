namespace Aula.Core.Devices;

/// <summary>
/// Notifies when the system HID device list changes (device connect/disconnect).
/// Wrapped behind an interface so reconnect logic can be tested without hardware.
/// </summary>
public interface IHidDeviceListWatcher : IDisposable
{
    event EventHandler? DeviceListChanged;

    void Start();

    void Stop();
}
