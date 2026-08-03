using Aula.Core.Abstractions;
using Aula.Core.Devices;
using Aula.Core.Models;
using Aula.Core.Protocol;

namespace Aula.Core.Drivers;

/// <summary>
/// Driver for the 2.4 GHz receiver dongle (AULA F75), which is controlled
/// through the Report ID 0x13 wireless protocol rather than frame-06.
/// </summary>
public sealed class WirelessSinoWealthDriver : IKeyboardDriver
{
    private readonly ITransportFactory _transportFactory;

    public WirelessSinoWealthDriver(ModelConfig model, ITransportFactory transportFactory)
    {
        Model = model;
        _transportFactory = transportFactory;
    }

    public ModelConfig Model { get; }

    public KeyboardCapabilities Capabilities { get; } =
        new(HasLighting: true, HasPerKeyRgb: true, HasKeyRemap: false, HasWireless: true, HasScreen: false);

    public bool Matches(DeviceInfo device) =>
        device.VendorId == AulaDeviceIds.VendorWireless
        && device.ProductId == AulaDeviceIds.ProductWireless;

    public IAulaKeyboard Open(DeviceInfo device)
    {
        IHidTransport transport = _transportFactory.Create(device);
        transport.Open();
        return new WirelessAulaKeyboard(transport, Model, Capabilities);
    }
}
