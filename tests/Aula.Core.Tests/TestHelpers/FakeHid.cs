using Aula.Core.Devices;

namespace Aula.Core.Tests.TestHelpers;

public sealed class FakeHidDevice : IHidDevice
{
    public string DevicePath { get; set; } = "path://fake";

    public int VendorID { get; set; } = AulaDeviceIds.VendorSinoWealth;

    public int ProductID { get; set; } = AulaDeviceIds.ProductF75F87Wired;

    public Func<string?>? SerialGetter { get; set; }

    public Func<string?>? NameGetter { get; set; }

    public Func<int>? FeatureGetter { get; set; }

    public Func<int>? InputGetter { get; set; }

    public Func<int>? OutputGetter { get; set; }

    public Func<IHidStream>? StreamFactory { get; set; }

    public int OpenCount { get; private set; }

    public string? GetSerialNumber() => SerialGetter?.Invoke();

    public string? GetProductName() => NameGetter?.Invoke();

    public int GetMaxFeatureReportLength() => FeatureGetter?.Invoke() ?? 520;

    public int GetMaxInputReportLength() => InputGetter?.Invoke() ?? 0;

    public int GetMaxOutputReportLength() => OutputGetter?.Invoke() ?? 0;

    public IHidStream Open()
    {
        OpenCount++;
        return StreamFactory?.Invoke() ?? new FakeHidStream();
    }
}

public sealed class FakeHidStream : IHidStream
{
    public int ReadTimeout { get; set; }

    public Action<byte[]>? OnSetFeature { get; set; }

    public Action<byte[]>? OnGetFeature { get; set; }

    public Action<byte[]>? OnWrite { get; set; }

    public Func<byte[], int>? OnRead { get; set; }

    public bool Disposed { get; set; }

    public void SetFeature(byte[] buffer) => OnSetFeature?.Invoke(buffer);

    public void GetFeature(byte[] buffer) => OnGetFeature?.Invoke(buffer);

    public void Write(byte[] buffer) => OnWrite?.Invoke(buffer);

    public int Read(byte[] buffer) => OnRead?.Invoke(buffer) ?? 0;

    public void Dispose() => Disposed = true;
}

public sealed class FakeHidDeviceList : IHidDeviceList
{
    public List<IHidDevice> Devices { get; } = new();

    public IEnumerable<IHidDevice> GetHidDevices(int vendorId, int productId) =>
        Devices.Where(d => d.VendorID == vendorId && d.ProductID == productId);

    public IEnumerable<IHidDevice> GetHidDevices() => Devices;
}
