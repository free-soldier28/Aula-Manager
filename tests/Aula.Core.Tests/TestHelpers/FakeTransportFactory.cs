using Aula.Core.Abstractions;
using Aula.Core.Devices;
using Aula.Core.Protocol;

namespace Aula.Core.Tests.TestHelpers;

public sealed class FakeTransportFactory : ITransportFactory
{
    private readonly Func<DeviceInfo, FakeTransport> _create;

    public FakeTransportFactory(Func<DeviceInfo, FakeTransport>? create = null) =>
        _create = create ?? (_ => new FakeTransport());

    public IHidTransport Create(DeviceInfo device) => _create(device);
}
