using Aula.Core.Abstractions;
using Aula.Core.Devices;
using Aula.Core.Drivers;
using Aula.Core.Models;
using Aula.Core.Tests.TestHelpers;

namespace Aula.Core.Tests;

public class DriverRegistryTests
{
    private static DeviceInfo F75Device() =>
        new("path://f75", AulaDeviceIds.VendorSinoWealth, AulaDeviceIds.ProductF75F87Wired, null, "AULA F75", 520);

    [Fact]
    public void Default_RegistersF75F87AndWireless()
    {
        Assert.NotNull(DriverRegistry.Default.ResolveByModelId("f75"));
        Assert.NotNull(DriverRegistry.Default.ResolveByModelId("f87"));
        Assert.Equal(3, DriverRegistry.Default.Drivers.Count);
    }

    [Fact]
    public void Resolve_ReturnsMatchingDriver()
    {
        var registry = new DriverRegistry();
        registry.Register(new SinoWealthFeatureDriver(ModelConfig.F75, new FakeTransportFactory()));

        IKeyboardDriver? driver = registry.Resolve(F75Device());

        Assert.NotNull(driver);
        Assert.Equal("f75", driver.Model.Id);
    }

    [Fact]
    public void Resolve_ReturnsNull_ForUnknownDevice()
    {
        var registry = new DriverRegistry();
        registry.Register(new SinoWealthFeatureDriver(ModelConfig.F75, new FakeTransportFactory()));

        var unknown = new DeviceInfo("path://x", 0x1234, 0x5678, null, null, 520);

        Assert.Null(registry.Resolve(unknown));
    }

    [Fact]
    public void ResolveByModelId_IsCaseInsensitive()
    {
        var registry = new DriverRegistry();
        registry.Register(new SinoWealthFeatureDriver(ModelConfig.F75, new FakeTransportFactory()));

        Assert.NotNull(registry.ResolveByModelId("F75"));
        Assert.Null(registry.ResolveByModelId("f99"));
    }

    [Fact]
    public void Register_ReplacesDriverWithSameModelId()
    {
        var registry = new DriverRegistry();
        var first = new SinoWealthFeatureDriver(ModelConfig.F75, new FakeTransportFactory());
        var second = new SinoWealthFeatureDriver(ModelConfig.F75, new FakeTransportFactory());

        registry.Register(first);
        registry.Register(second);

        Assert.Single(registry.Drivers);
        Assert.Same(second, registry.ResolveByModelId("f75"));
    }
}
