using Aula.Core.Abstractions;
using Aula.Core.Devices;
using Aula.Core.Logging;
using Aula.Core.Protocol;
using Microsoft.Extensions.Logging;

namespace Aula.Core.Drivers;

public sealed class HidSharpTransportFactory : ITransportFactory
{
    private readonly IHidDeviceList _deviceList;
    private readonly ILogger<HidSharpTransportFactory> _log;

    public HidSharpTransportFactory(IHidDeviceList? deviceList = null)
    {
        _deviceList = deviceList ?? HidSharpDeviceList.Local;
        _log = AulaLogging.Logger<HidSharpTransportFactory>();
    }

    public IHidTransport Create(DeviceInfo device)
    {
        IHidDevice hidDevice = _deviceList.GetHidDevices()
            .FirstOrDefault(d => d.DevicePath == device.DevicePath)
            ?? throw new AulaDeviceNotFoundException();

        _log.LogDebug("Creating transport for {Path}", device.DevicePath);
        return new HidSharpTransport(hidDevice);
    }
}
