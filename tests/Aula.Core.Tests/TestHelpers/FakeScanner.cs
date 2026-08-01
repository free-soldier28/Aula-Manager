using Aula.Core.Devices;

namespace Aula.Core.Tests.TestHelpers;

public sealed class FakeScanner : IHidDeviceScanner
{
    public List<DeviceInfo> Devices { get; } = new();

    public IReadOnlyList<DeviceInfo> Scan(int vendorId, int productId) =>
        Devices.Where(d => d.VendorId == vendorId && d.ProductId == productId).ToList();

    public IReadOnlyList<DeviceInfo> ScanAll() => Devices.ToList();
}
