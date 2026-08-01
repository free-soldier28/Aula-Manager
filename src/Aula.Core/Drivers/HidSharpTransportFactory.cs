using Aula.Core.Abstractions;
using Aula.Core.Devices;
using Aula.Core.Protocol;
using HidSharp;

namespace Aula.Core.Drivers;

public sealed class HidSharpTransportFactory : ITransportFactory
{
    public IHidTransport Create(DeviceInfo device)
    {
        HidDevice hidDevice = DeviceList.Local.GetHidDevices()
            .FirstOrDefault(d => d.DevicePath == device.DevicePath)
            ?? throw new AulaDeviceNotFoundException();

        return new HidSharpTransport(hidDevice);
    }
}
