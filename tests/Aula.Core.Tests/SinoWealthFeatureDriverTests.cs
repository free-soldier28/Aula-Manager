using Aula.Core.Abstractions;
using Aula.Core.Devices;
using Aula.Core.Drivers;
using Aula.Core.Models;
using Aula.Core.Tests.TestHelpers;

namespace Aula.Core.Tests;

public class SinoWealthFeatureDriverTests
{
    private static DeviceInfo F75Device() =>
        new("path://f75", AulaDeviceIds.VendorSinoWealth, AulaDeviceIds.ProductF75F87Wired, "SN", "AULA F75", 520);

    [Fact]
    public void Matches_OnlyItsVidPid()
    {
        var driver = new SinoWealthFeatureDriver(ModelConfig.F75, new FakeTransportFactory());

        Assert.True(driver.Matches(F75Device()));
        Assert.False(driver.Matches(new DeviceInfo("path://x", 0x1234, 0x5678, null, null, 520)));
    }

    [Fact]
    public void Open_ReturnsKeyboardWithLighting()
    {
        FakeTransport? opened = null;
        var factory = new FakeTransportFactory(device =>
        {
            opened = new FakeTransport();
            return opened;
        });
        var driver = new SinoWealthFeatureDriver(ModelConfig.F75, factory);

        using IAulaKeyboard keyboard = driver.Open(F75Device());

        Assert.Equal("f75", keyboard.Model.Id);
        Assert.True(opened!.IsOpen);
        Assert.True(keyboard.Capabilities.HasLighting);
        Assert.False(keyboard.Capabilities.HasPerKeyRgb);
    }

    [Fact]
    public void Open_DisposeClosesTransport()
    {
        FakeTransport? opened = null;
        var factory = new FakeTransportFactory(device =>
        {
            opened = new FakeTransport();
            return opened;
        });
        var driver = new SinoWealthFeatureDriver(ModelConfig.F75, factory);

        var keyboard = driver.Open(F75Device());
        keyboard.Dispose();

        Assert.False(opened!.IsOpen);
    }

    [Fact]
    public void Open_ExposesSinowealthDiagnostics()
    {
        var factory = new FakeTransportFactory();
        var driver = new SinoWealthFeatureDriver(ModelConfig.F75, factory);

        using var keyboard = driver.Open(F75Device());

        Assert.IsAssignableFrom<ISinowealthDiagnostics>(keyboard);
    }
}
