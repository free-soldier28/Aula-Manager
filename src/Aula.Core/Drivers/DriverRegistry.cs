using Aula.Core.Abstractions;
using Aula.Core.Devices;
using Aula.Core.Models;

namespace Aula.Core.Drivers;

public sealed class DriverRegistry
{
    private readonly List<IKeyboardDriver> _drivers = new();

    public static DriverRegistry Default { get; } = CreateDefault();

    public IReadOnlyList<IKeyboardDriver> Drivers => _drivers;

    public void Register(IKeyboardDriver driver)
    {
        _drivers.RemoveAll(d =>
            string.Equals(d.Model.Id, driver.Model.Id, StringComparison.OrdinalIgnoreCase)
            && d.GetType() == driver.GetType());
        _drivers.Add(driver);
    }

    public IKeyboardDriver? Resolve(DeviceInfo device) =>
        _drivers.FirstOrDefault(d => d.Matches(device));

    public IKeyboardDriver? ResolveByModelId(string modelId) =>
        _drivers.FirstOrDefault(d => string.Equals(d.Model.Id, modelId, StringComparison.OrdinalIgnoreCase));

    private static DriverRegistry CreateDefault()
    {
        var registry = new DriverRegistry();
        var transportFactory = new HidSharpTransportFactory();

        registry.Register(new SinoWealthFeatureDriver(ModelConfig.F75, transportFactory));
        registry.Register(new SinoWealthFeatureDriver(ModelConfig.F87, transportFactory));
        registry.Register(new WirelessSinoWealthDriver(ModelConfig.F75, transportFactory));

        return registry;
    }
}
