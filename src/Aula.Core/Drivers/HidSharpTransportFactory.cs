using Aula.Core.Abstractions;
using Aula.Core.Devices;
using Aula.Core.Protocol;

namespace Aula.Core.Drivers;

public sealed class HidSharpTransportFactory : ITransportFactory
{
    private readonly IHidDeviceList _deviceList;

    public HidSharpTransportFactory(IHidDeviceList? deviceList = null) =>
        _deviceList = deviceList ?? HidSharpDeviceList.Local;

    public IHidTransport Create(DeviceInfo device)
    {
        IHidDevice hidDevice = _deviceList.GetHidDevices()
            .FirstOrDefault(d => d.DevicePath == device.DevicePath)
            ?? throw new AulaDeviceNotFoundException();

        return new HidSharpTransport(hidDevice);
    }
}
