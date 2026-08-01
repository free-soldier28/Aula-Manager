using Aula.Core.Abstractions;
using Aula.Core.Devices;
using Aula.Core.Drivers;

namespace Aula.Core.Services;

public sealed class KeyboardDeviceFactory
{
    private readonly IHidDeviceScanner _scanner;
    private readonly DriverRegistry _registry;

    public KeyboardDeviceFactory(IHidDeviceScanner scanner, DriverRegistry registry)
    {
        _scanner = scanner;
        _registry = registry;
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
            return null;
        }

        IKeyboardDriver? driver = modelId is null
            ? _registry.Resolve(picked)
            : _registry.ResolveByModelId(modelId);

        return driver?.Open(picked);
    }

    public IAulaKeyboard Open(string? modelId = null) =>
        TryOpen(modelId) ?? throw new AulaDeviceNotFoundException();

    private DeviceInfo? PickDevice() =>
        DevicePicker.PickBest(_scanner.ScanAll());
}
