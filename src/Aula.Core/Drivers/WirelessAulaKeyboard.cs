using Aula.Core.Abstractions;
using Aula.Core.Devices;
using Aula.Core.Models;
using Aula.Core.Protocol;
using Aula.Core.Services;

namespace Aula.Core.Drivers;

/// <summary>
/// Keyboard handle for the 2.4 GHz receiver. Uses the Report ID 0x13 protocol
/// instead of the frame-06 feature-report protocol used over USB.
/// </summary>
public sealed class WirelessAulaKeyboard : IAulaKeyboard
{
    private readonly IHidTransport _transport;
    private readonly WirelessProtocol _protocol;

    public WirelessAulaKeyboard(IHidTransport transport, ModelConfig model, KeyboardCapabilities capabilities)
    {
        _transport = transport;
        _protocol = new WirelessProtocol(transport);
        Model = model;
        Capabilities = capabilities;
        Lighting = new WirelessLightingService(_protocol, model);
    }

    public DeviceInfo Info => _transport.Info;

    public ModelConfig Model { get; }

    public KeyboardCapabilities Capabilities { get; }

    public IKeyboardLayout Layout => Model.Layout;

    public ILightingController Lighting { get; }

    public void Dispose() => _transport.Dispose();
}
