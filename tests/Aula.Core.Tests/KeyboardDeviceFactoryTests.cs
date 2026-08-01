using Aula.Core.Abstractions;
using Aula.Core.Devices;
using Aula.Core.Drivers;
using Aula.Core.Models;
using Aula.Core.Services;
using Aula.Core.Tests.TestHelpers;

namespace Aula.Core.Tests;

public class KeyboardDeviceFactoryTests
{
    private static DeviceInfo F75Device() =>
        new("path://f75", AulaDeviceIds.VendorSinoWealth, AulaDeviceIds.ProductF75F87Wired, "SN", "AULA F75", 520);

    [Fact]
    public void TryOpen_ReturnsNull_WhenNoDevice()
    {
        var factory = CreateFactory(new FakeScanner());

        Assert.Null(factory.TryOpen());
    }

    [Fact]
    public void TryOpen_OpensKeyboardWithDetectedModel()
    {
        var scanner = new FakeScanner();
        scanner.Devices.Add(F75Device());
        var factory = CreateFactory(scanner);

        using IAulaKeyboard? keyboard = factory.TryOpen();

        Assert.NotNull(keyboard);
        Assert.Equal("f75", keyboard.Model.Id);
        Assert.True(keyboard.Lighting is not null);
        Assert.True(keyboard.Capabilities.HasLighting);
    }

    [Fact]
    public void Open_Throws_WhenNoDevice()
    {
        var factory = CreateFactory(new FakeScanner());

        Assert.Throws<AulaDeviceNotFoundException>(() => factory.Open());
    }

    [Fact]
    public void Open_WithModelId_ForcesDriver()
    {
        var scanner = new FakeScanner();
        scanner.Devices.Add(F75Device());
        var factory = CreateFactory(scanner);

        using IAulaKeyboard keyboard = factory.Open("f87");

        Assert.Equal("f87", keyboard.Model.Id);
    }

    private static KeyboardDeviceFactory CreateFactory(IHidDeviceScanner scanner)
    {
        var registry = new DriverRegistry();
        registry.Register(new SinoWealthFeatureDriver(ModelConfig.F75, new FakeTransportFactory()));
        registry.Register(new SinoWealthFeatureDriver(ModelConfig.F87, new FakeTransportFactory()));
        return new KeyboardDeviceFactory(scanner, registry);
    }
}
