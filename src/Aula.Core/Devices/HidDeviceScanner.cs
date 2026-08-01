using HidSharp;

namespace Aula.Core.Devices;

public sealed class HidDeviceScanner : IHidDeviceScanner
{
    public IReadOnlyList<DeviceInfo> Scan(int vendorId, int productId)
    {
        return DeviceList.Local.GetHidDevices(vendorId, productId)
            .Select(ToInfo)
            .ToList();
    }

    public IReadOnlyList<DeviceInfo> ScanAll()
    {
        return DeviceList.Local.GetHidDevices()
            .Where(d => (d.VendorID == AulaDeviceIds.VendorSinoWealth && d.ProductID == AulaDeviceIds.ProductF75F87Wired)
                     || (d.VendorID == AulaDeviceIds.VendorWireless && d.ProductID == AulaDeviceIds.ProductWireless))
            .Select(ToInfo)
            .ToList();
    }

    private static DeviceInfo ToInfo(HidDevice d)
    {
        string? serial = SafeString(d.GetSerialNumber);
        string? name = SafeString(d.GetProductName);
        int featureLength = SafeInt(d.GetMaxFeatureReportLength);
        return new DeviceInfo(d.DevicePath, d.VendorID, d.ProductID, serial, name, featureLength);
    }

    private static string? SafeString(Func<string?> getter)
    {
        try
        {
            return getter();
        }
        catch
        {
            return null;
        }
    }

    private static int SafeInt(Func<int> getter)
    {
        try
        {
            return getter();
        }
        catch
        {
            return 0;
        }
    }
}

public static class DevicePicker
{
    public static DeviceInfo? PickBest(IEnumerable<DeviceInfo> devices) =>
        devices
            .Where(d => d.MaxFeatureReportLength > 0)
            .OrderByDescending(d => d.MaxFeatureReportLength)
            .ThenBy(d => d.DevicePath, StringComparer.Ordinal)
            .FirstOrDefault();
}
