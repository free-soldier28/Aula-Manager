using Aula.Core.Abstractions;
using Aula.Core.Devices;
using Aula.Core.Drivers;
using Aula.Core.Logging;
using Microsoft.Extensions.Logging;

namespace Aula.Core.Services;

public sealed class KeyboardDeviceFactory
{
    private readonly IHidDeviceScanner _scanner;
    private readonly DriverRegistry _registry;
    private readonly ILogger<KeyboardDeviceFactory> _log;

    public KeyboardDeviceFactory(IHidDeviceScanner scanner, DriverRegistry registry)
    {
        _scanner = scanner;
        _registry = registry;
        _log = AulaLogging.Logger<KeyboardDeviceFactory>();
    }

    public KeyboardDeviceFactory()
        : this(new HidDeviceScanner(), DriverRegistry.Default)
    {
    }

    public IAulaKeyboard? TryOpen(string? modelId = null)
    {
        DeviceInfo? picked = PickDevice();
        if (picked is null)
        {
            _log.LogWarning("No AULA device found while scanning");
            return null;
        }

        IKeyboardDriver? driver = modelId is null
            ? _registry.Resolve(picked)
            : _registry.ResolveByModelId(modelId);

        if (driver is null)
        {
            _log.LogWarning("No driver matches {Vendor:X4}:{Product:X4} ({Path})", picked.VendorId, picked.ProductId, picked.DevicePath);
            return null;
        }

        _log.LogInformation("Opening {Model} via {Driver} on {Path}", driver.Model.Id, driver.GetType().Name, picked.DevicePath);
        return driver?.Open(picked);
    }

    public IAulaKeyboard Open(string? modelId = null) =>
        TryOpen(modelId) ?? throw new AulaDeviceNotFoundException();

    public IReadOnlyList<DeviceInfo> PresentDevices() => _scanner.ScanAll();

    private DeviceInfo? PickDevice() =>
        DevicePicker.PickBest(PresentDevices());
}
