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
    public void TryOpen_OpensKeyboardOverWirelessDongle()
    {
        var scanner = new FakeScanner();
        scanner.Devices.Add(new DeviceInfo(
            "path://dongle", AulaDeviceIds.VendorWireless, AulaDeviceIds.ProductWireless, "SN", "2.4G Wireless Receiver", 0, 20, 20));
        var factory = CreateFactory(scanner);

        using IAulaKeyboard? keyboard = factory.TryOpen();

        Assert.NotNull(keyboard);
        Assert.Equal("f75", keyboard.Model.Id);
        Assert.True(keyboard.Capabilities.HasWireless);
        Assert.True(keyboard.Capabilities.HasPerKeyRgb);
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

    [Fact]
    public void TryOpen_ReturnsNull_WhenNoDriverMatches()
    {
        var scanner = new FakeScanner();
        scanner.Devices.Add(new DeviceInfo(
            "path://unknown", 0x1234, 0x5678, "SN", "Unknown HID Device", 0));
        var factory = CreateFactory(scanner);

        Assert.Null(factory.TryOpen());
    }

    [Fact]
    public void TryOpen_ReturnsNull_WhenNoDriverMatchesModelId()
    {
        var scanner = new FakeScanner();
        scanner.Devices.Add(F75Device());
        var factory = CreateFactory(scanner);

        Assert.Null(factory.TryOpen("f100"));
    }

    [Fact]
    public void PresentDevices_ReturnsScannedDevices()
    {
        var scanner = new FakeScanner();
        scanner.Devices.Add(F75Device());
        scanner.Devices.Add(new DeviceInfo(
            "path://dongle", AulaDeviceIds.VendorWireless, AulaDeviceIds.ProductWireless, "SN", "2.4G Wireless Receiver", 0, 20, 20));
        var factory = CreateFactory(scanner);

        IReadOnlyList<DeviceInfo> devices = factory.PresentDevices();

        Assert.Equal(2, devices.Count);
    }

    private static KeyboardDeviceFactory CreateFactory(IHidDeviceScanner scanner)
    {
        var registry = new DriverRegistry();
        registry.Register(new SinoWealthFeatureDriver(ModelConfig.F75, new FakeTransportFactory()));
        registry.Register(new SinoWealthFeatureDriver(ModelConfig.F87, new FakeTransportFactory()));
        registry.Register(new WirelessSinoWealthDriver(ModelConfig.F75, new FakeTransportFactory()));
        return new KeyboardDeviceFactory(scanner, registry);
    }
}
