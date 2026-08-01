using Aula.Core.Devices;
using Aula.Core.Protocol;

namespace Aula.Core.Abstractions;

public interface ITransportFactory
{
    IHidTransport Create(DeviceInfo device);
}
