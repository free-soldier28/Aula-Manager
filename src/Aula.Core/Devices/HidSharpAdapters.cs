using HidSharp;

namespace Aula.Core.Devices;

/// <summary>Default <see cref="IHidDeviceList"/> backed by HidSharp's <c>DeviceList.Local</c>.</summary>
public sealed class HidSharpDeviceList : IHidDeviceList
{
    public static readonly IHidDeviceList Local = new HidSharpDeviceList();

    private HidSharpDeviceList()
    {
    }

    public IEnumerable<IHidDevice> GetHidDevices(int vendorId, int productId) =>
        DeviceList.Local.GetHidDevices(vendorId, productId).Select(d => (IHidDevice)new HidSharpDeviceAdapter(d));

    public IEnumerable<IHidDevice> GetHidDevices() =>
        DeviceList.Local.GetHidDevices().Select(d => (IHidDevice)new HidSharpDeviceAdapter(d));
}

internal sealed class HidSharpDeviceAdapter : IHidDevice
{
    private readonly HidDevice _device;

    public HidSharpDeviceAdapter(HidDevice device) => _device = device;

    public string DevicePath => _device.DevicePath;

    public int VendorID => _device.VendorID;

    public int ProductID => _device.ProductID;

    public string? GetSerialNumber() => _device.GetSerialNumber();

    public string? GetProductName() => _device.GetProductName();

    public int GetMaxFeatureReportLength() => _device.GetMaxFeatureReportLength();

    public int GetMaxInputReportLength() => _device.GetMaxInputReportLength();

    public int GetMaxOutputReportLength() => _device.GetMaxOutputReportLength();

    public IHidStream Open() => new HidSharpStreamAdapter(_device.Open());
}

internal sealed class HidSharpStreamAdapter : IHidStream
{
    private readonly HidStream _stream;

    public HidSharpStreamAdapter(HidStream stream) => _stream = stream;

    public int ReadTimeout
    {
        get => _stream.ReadTimeout;
        set => _stream.ReadTimeout = value;
    }

    public void SetFeature(byte[] buffer) => _stream.SetFeature(buffer);

    public void GetFeature(byte[] buffer) => _stream.GetFeature(buffer);

    public void Write(byte[] buffer) => _stream.Write(buffer);

    public int Read(byte[] buffer) => _stream.Read(buffer);

    public void Dispose() => _stream.Dispose();
}
