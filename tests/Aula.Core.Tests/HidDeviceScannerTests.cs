using Aula.Core.Devices;
using Aula.Core.Tests.TestHelpers;

namespace Aula.Core.Tests;

public class HidDeviceScannerTests
{
    [Fact]
    public void Scan_FiltersByVendorAndProduct()
    {
        var f75 = new FakeHidDevice { DevicePath = "path://f75" };
        var other = new FakeHidDevice
        {
            DevicePath = "path://other",
            VendorID = 0x1234,
            ProductID = 0x5678,
        };
        var list = new FakeHidDeviceList { Devices = { f75, other } };
        var scanner = new HidDeviceScanner(list);

        var result = scanner.Scan(AulaDeviceIds.VendorSinoWealth, AulaDeviceIds.ProductF75F87Wired);

        DeviceInfo device = Assert.Single(result);
        Assert.Equal("path://f75", device.DevicePath);
    }

    [Fact]
    public void ScanAll_FiltersToKnownAulaDevices()
    {
        var f75 = new FakeHidDevice { DevicePath = "path://f75" };
        var wireless = new FakeHidDevice
        {
            DevicePath = "path://dongle",
            VendorID = AulaDeviceIds.VendorWireless,
            ProductID = AulaDeviceIds.ProductWireless,
        };
        var unrelated = new FakeHidDevice
        {
            DevicePath = "path://other",
            VendorID = 0x9999,
            ProductID = 0x1111,
        };
        var scanner = new HidDeviceScanner(new FakeHidDeviceList { Devices = { f75, wireless, unrelated } });

        var result = scanner.ScanAll();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, d => d.DevicePath == "path://f75");
        Assert.Contains(result, d => d.DevicePath == "path://dongle");
    }

    [Fact]
    public void Scan_GetterThrows_ReturnsNullsAndZeroLengths()
    {
        var flaky = new FakeHidDevice
        {
            SerialGetter = () => throw new InvalidOperationException("serial"),
            NameGetter = () => throw new InvalidOperationException("name"),
            FeatureGetter = () => throw new InvalidOperationException("feature"),
            InputGetter = () => throw new InvalidOperationException("input"),
            OutputGetter = () => throw new InvalidOperationException("output"),
        };
        var scanner = new HidDeviceScanner(new FakeHidDeviceList { Devices = { flaky } });

        DeviceInfo device = Assert.Single(scanner.Scan(flaky.VendorID, flaky.ProductID));

        Assert.Null(device.SerialNumber);
        Assert.Null(device.ProductName);
        Assert.Equal("AULA (258A:010C)", device.DisplayName);
        Assert.Equal(0, device.MaxFeatureReportLength);
        Assert.Equal(0, device.MaxInputReportLength);
        Assert.Equal(0, device.MaxOutputReportLength);
    }
}
