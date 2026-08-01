namespace Aula.Core.Devices;

public interface IHidDeviceScanner
{
    IReadOnlyList<DeviceInfo> Scan(int vendorId, int productId);

    IReadOnlyList<DeviceInfo> ScanAll();
}
