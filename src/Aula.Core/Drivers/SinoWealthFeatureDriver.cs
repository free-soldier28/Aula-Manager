using Aula.Core.Abstractions;
using Aula.Core.Devices;
using Aula.Core.Models;
using Aula.Core.Protocol;

namespace Aula.Core.Drivers;

public sealed class SinoWealthFeatureDriver : IKeyboardDriver
{
    private readonly ITransportFactory _transportFactory;

    public SinoWealthFeatureDriver(ModelConfig model, ITransportFactory transportFactory)
    {
        Model = model;
        _transportFactory = transportFactory;
    }

    public ModelConfig Model { get; }

    public KeyboardCapabilities Capabilities { get; } =
        new(HasLighting: true, HasPerKeyRgb: false, HasKeyRemap: false, HasWireless: false, HasScreen: false);

    public bool Matches(DeviceInfo device) =>
        device.VendorId == Model.VendorId && device.ProductId == Model.ProductId;

    public IAulaKeyboard Open(DeviceInfo device)
    {
        IHidTransport transport = _transportFactory.Create(device);
        transport.Open();
        return new AulaKeyboard(transport, Model, Capabilities);
    }
}
