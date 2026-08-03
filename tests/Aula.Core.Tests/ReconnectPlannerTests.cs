using Aula.Core.Devices;
using Aula.Core.Tests.TestHelpers;

namespace Aula.Core.Tests;

public class ReconnectPlannerTests
{
    private static DeviceInfo Device(string path) =>
        new(path, AulaDeviceIds.VendorSinoWealth, AulaDeviceIds.ProductF75F87Wired, "SN", "AULA F75", 520);

    [Fact]
    public void Decide_NoDeviceAndNoConnection_Keep()
    {
        ReconnectAction action = ReconnectPlanner.Decide(Array.Empty<DeviceInfo>(), null);

        Assert.Equal(ReconnectAction.Keep, action);
    }

    [Fact]
    public void Decide_DevicePresentButNotConnected_Open()
    {
        var devices = new[] { Device("path://f75") };

        ReconnectAction action = ReconnectPlanner.Decide(devices, null);

        Assert.Equal(ReconnectAction.Open, action);
    }

    [Fact]
    public void Decide_ConnectedDeviceStillPresent_Keep()
    {
        var devices = new[] { Device("path://f75") };

        ReconnectAction action = ReconnectPlanner.Decide(devices, "path://f75");

        Assert.Equal(ReconnectAction.Keep, action);
    }

    [Fact]
    public void Decide_ConnectedDeviceGone_Release()
    {
        var devices = new[] { Device("path://f75") };

        ReconnectAction action = ReconnectPlanner.Decide(devices, "path://gone");

        Assert.Equal(ReconnectAction.Release, action);
    }

    [Fact]
    public void Decide_AllDevicesGone_Release()
    {
        ReconnectAction action = ReconnectPlanner.Decide(Array.Empty<DeviceInfo>(), "path://gone");

        Assert.Equal(ReconnectAction.Release, action);
    }
}
