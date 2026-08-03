using Aula.Core.Abstractions;
using Aula.Core.Devices;
using Aula.Core.Drivers;
using Aula.Core.Models;
using Aula.Core.Tests.TestHelpers;

namespace Aula.Core.Tests;

public class WirelessDriverTests
{
    private static readonly DeviceInfo WirelessDevice = new(
        "path://wireless", AulaDeviceIds.VendorWireless, AulaDeviceIds.ProductWireless, "SN", "2.4G receiver",
        MaxFeatureReportLength: 64, MaxInputReportLength: 64, MaxOutputReportLength: 64);

    private static readonly DeviceInfo WiredDevice = new(
        "path://f75", AulaDeviceIds.VendorSinoWealth, AulaDeviceIds.ProductF75F87Wired, "SN", "AULA F75",
        MaxFeatureReportLength: 520);

    [Fact]
    public void WirelessDriver_MatchesOnlyWirelessDevice()
    {
        var driver = new WirelessSinoWealthDriver(ModelConfig.F75, new FakeTransportFactory());

        Assert.True(driver.Matches(WirelessDevice));
        Assert.False(driver.Matches(WiredDevice));
    }

    [Fact]
    public void WirelessDriver_ExposesWirelessCapabilities()
    {
        var driver = new WirelessSinoWealthDriver(ModelConfig.F75, new FakeTransportFactory());

        Assert.True(driver.Capabilities.HasWireless);
        Assert.True(driver.Capabilities.HasPerKeyRgb);
        Assert.Equal("f75", driver.Model.Id);
    }

    [Fact]
    public void Open_ReturnsWirelessKeyboard_AndOpensTransport()
    {
        var transport = new FakeTransport();
        var driver = new WirelessSinoWealthDriver(ModelConfig.F75, new FakeTransportFactory(_ => transport));

        using IAulaKeyboard keyboard = driver.Open(WirelessDevice);

        Assert.True(transport.IsOpen);
        Assert.IsType<WirelessAulaKeyboard>(keyboard);
        Assert.True(keyboard.Capabilities.HasWireless);
        Assert.Equal("f75", keyboard.Model.Id);
        Assert.Same(F75Layout.Instance, keyboard.Layout);
        Assert.Same(transport.Info, keyboard.Info);
        Assert.NotNull(keyboard.Lighting);
    }

    [Fact]
    public void Dispose_DisposesTransport()
    {
        var transport = new FakeTransport();
        var keyboard = new WirelessAulaKeyboard(
            transport, ModelConfig.F75,
            new KeyboardCapabilities(HasLighting: true, HasPerKeyRgb: true, HasWireless: true));

        keyboard.Dispose();

        Assert.False(transport.IsOpen);
    }
}
