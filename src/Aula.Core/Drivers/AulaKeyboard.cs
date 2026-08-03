using Aula.Core.Abstractions;
using Aula.Core.Devices;
using Aula.Core.Models;
using Aula.Core.Protocol;
using Aula.Core.Services;

namespace Aula.Core.Drivers;

public sealed class AulaKeyboard : IAulaKeyboard, ISinowealthDiagnostics
{
    private readonly IHidTransport _transport;
    private readonly SinowealthProtocol _protocol;

    public AulaKeyboard(IHidTransport transport, ModelConfig model, KeyboardCapabilities capabilities)
    {
        _transport = transport;
        _protocol = new SinowealthProtocol(transport, model);
        Model = model;
        Capabilities = capabilities;
        Lighting = new LightingService(_protocol);
    }

    public DeviceInfo Info => _transport.Info;

    public ModelConfig Model { get; }

    public KeyboardCapabilities Capabilities { get; }

    public IKeyboardLayout Layout => Model.Layout;

    public ILightingController Lighting { get; }

    public byte[] QueryModel() => _protocol.QueryModel();

    public byte[] ReadColorProfileRaw() => _protocol.ReadColorProfileRaw();

    public void Dispose() => _transport.Dispose();
}
