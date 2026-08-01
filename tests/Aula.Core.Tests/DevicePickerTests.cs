using Aula.Core.Devices;

namespace Aula.Core.Tests;

public class DevicePickerTests
{
    private static DeviceInfo Device(int featureLength, string path) =>
        new(path, 0x258A, 0x010C, null, "AULA", featureLength);

    [Fact]
    public void PickBest_PrefersFeatureReportCapableDevice()
    {
        var devices = new[]
        {
            Device(0, "keyboard"),
            Device(520, "vendor"),
        };

        Assert.Equal("vendor", DevicePicker.PickBest(devices)?.DevicePath);
    }

    [Fact]
    public void PickBest_SelectsLargestFeatureReport()
    {
        var devices = new[]
        {
            Device(128, "small"),
            Device(520, "large"),
        };

        Assert.Equal("large", DevicePicker.PickBest(devices)?.DevicePath);
    }

    [Fact]
    public void PickBest_ReturnsNull_WhenNoFeatureDevice()
    {
        Assert.Null(DevicePicker.PickBest(new[] { Device(0, "keyboard") }));
        Assert.Null(DevicePicker.PickBest(Array.Empty<DeviceInfo>()));
    }
}
