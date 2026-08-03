namespace Aula.Core.Devices;

public enum ReconnectAction
{
    /// <summary>Nothing to do — current device still present, or no device and none available.</summary>
    Keep,

    /// <summary>No keyboard connected but an AULA device is present — (re)open it.</summary>
    Open,

    /// <summary>The currently connected device disappeared — release it.</summary>
    Release,
}

/// <summary>
/// Pure decision logic for hotplug auto-reconnect: given the currently scanned
/// AULA devices and the path of the connected keyboard (if any), decide whether
/// to open, keep or release. Kept in Core so it is unit-testable without hardware.
/// </summary>
public static class ReconnectPlanner
{
    public static ReconnectAction Decide(
        IReadOnlyList<DeviceInfo> presentDevices,
        string? currentDevicePath)
    {
        if (currentDevicePath is null)
        {
            return presentDevices.Count > 0 ? ReconnectAction.Open : ReconnectAction.Keep;
        }

        return presentDevices.Any(d => d.DevicePath == currentDevicePath)
            ? ReconnectAction.Keep
            : ReconnectAction.Release;
    }
}
