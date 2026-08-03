using Aula.Core;
using Aula.Core.Devices;
using Aula.Core.Drivers;
using Aula.Core.Protocol;
using Aula.Core.Tests.TestHelpers;

namespace Aula.Core.Tests;

public class HidSharpTransportFactoryTests
{
    [Fact]
    public void Create_FindsDeviceByPath_ReturnsTransport()
    {
        var device = new FakeHidDevice { DevicePath = "path://f75" };
        device.SerialGetter = () => "SN123";
        device.NameGetter = () => "AULA F75";
        var factory = new HidSharpTransportFactory(new FakeHidDeviceList { Devices = { device } });

        using IHidTransport transport = factory.Create(new DeviceInfo("path://f75", 0, 0, null, null, 0));

        Assert.IsType<HidSharpTransport>(transport);
        Assert.Equal("path://f75", transport.Info.DevicePath);
        Assert.Equal("SN123", transport.Info.SerialNumber);
    }

    [Fact]
    public void Create_NoMatchingDevice_Throws()
    {
        var factory = new HidSharpTransportFactory(new FakeHidDeviceList());

        Assert.Throws<AulaDeviceNotFoundException>(() =>
            factory.Create(new DeviceInfo("path://missing", 0, 0, null, null, 0)));
    }
}
