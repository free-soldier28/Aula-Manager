using Aula.Core.Logging;
using Microsoft.Extensions.Logging;

namespace Aula.Core.Devices;

public sealed class HidDeviceScanner : IHidDeviceScanner
{
    private readonly IHidDeviceList _deviceList;
    private readonly ILogger<HidDeviceScanner> _log;

    public HidDeviceScanner(IHidDeviceList? deviceList = null)
    {
        _deviceList = deviceList ?? HidSharpDeviceList.Local;
        _log = AulaLogging.Logger<HidDeviceScanner>();
    }

    public IReadOnlyList<DeviceInfo> Scan(int vendorId, int productId)
    {
        IReadOnlyList<DeviceInfo> devices = _deviceList.GetHidDevices(vendorId, productId)
            .Select(ToInfo)
            .ToList();
        _log.LogDebug("Scan(0x{Vendor:X4}:0x{Product:X4}) found {Count} device(s)", vendorId, productId, devices.Count);
        return devices;
    }

    public IReadOnlyList<DeviceInfo> ScanAll()
    {
        IReadOnlyList<DeviceInfo> devices = _deviceList.GetHidDevices()
            .Where(d => (d.VendorID == AulaDeviceIds.VendorSinoWealth && d.ProductID == AulaDeviceIds.ProductF75F87Wired)
                     || (d.VendorID == AulaDeviceIds.VendorWireless && d.ProductID == AulaDeviceIds.ProductWireless))
            .Select(ToInfo)
            .ToList();
        _log.LogDebug("ScanAll found {Count} known AULA device(s)", devices.Count);
        return devices;
    }

    private DeviceInfo ToInfo(IHidDevice d)
    {
        string? serial = SafeString(d.GetSerialNumber, "serial", d.DevicePath);
        string? name = SafeString(d.GetProductName, "name", d.DevicePath);
        int featureLength = SafeInt(d.GetMaxFeatureReportLength, "feature", d.DevicePath);
        int inputLength = SafeInt(d.GetMaxInputReportLength, "input", d.DevicePath);
        int outputLength = SafeInt(d.GetMaxOutputReportLength, "output", d.DevicePath);
        return new DeviceInfo(d.DevicePath, d.VendorID, d.ProductID, serial, name, featureLength, inputLength, outputLength);
    }

    private string? SafeString(Func<string?> getter, string field, string path)
    {
        try
        {
            return getter();
        }
        catch (Exception ex)
        {
            _log.LogDebug("Failed to read {Field} for {Path}: {Message}", field, path, ex.Message);
            return null;
        }
    }

    private int SafeInt(Func<int> getter, string field, string path)
    {
        try
        {
            return getter();
        }
        catch (Exception ex)
        {
            _log.LogDebug("Failed to read {Field} for {Path}: {Message}", field, path, ex.Message);
            return 0;
        }
    }
}

public static class DevicePicker
{
    public static DeviceInfo? PickBest(IEnumerable<DeviceInfo> devices)
    {
        DeviceInfo[] list = devices as DeviceInfo[] ?? devices.ToArray();

        DeviceInfo? wireless = list
            .Where(d => d.VendorId == AulaDeviceIds.VendorWireless
                        && d.ProductId == AulaDeviceIds.ProductWireless)
            .Where(d => d.MaxOutputReportLength > 0)
            .OrderByDescending(d => d.MaxOutputReportLength)
            .ThenBy(d => d.DevicePath, StringComparer.Ordinal)
            .FirstOrDefault();
        if (wireless is not null)
        {
            return wireless;
        }

        return list
            .Where(d => d.MaxFeatureReportLength > 0)
            .OrderByDescending(d => d.MaxFeatureReportLength)
            .ThenBy(d => d.DevicePath, StringComparer.Ordinal)
            .FirstOrDefault();
    }
}
