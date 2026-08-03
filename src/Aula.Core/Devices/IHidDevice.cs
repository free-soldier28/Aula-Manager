namespace Aula.Core.Devices;

/// <summary>Abstraction over HidSharp's <c>HidDevice</c> so transports can be tested without hardware.</summary>
public interface IHidDevice
{
    string DevicePath { get; }

    int VendorID { get; }

    int ProductID { get; }

    string? GetSerialNumber();

    string? GetProductName();

    int GetMaxFeatureReportLength();

    int GetMaxInputReportLength();

    int GetMaxOutputReportLength();

    IHidStream Open();
}

/// <summary>Abstraction over HidSharp's <c>HidStream</c>.</summary>
public interface IHidStream : IDisposable
{
    int ReadTimeout { get; set; }

    void SetFeature(byte[] buffer);

    void GetFeature(byte[] buffer);

    void Write(byte[] buffer);

    int Read(byte[] buffer);
}

/// <summary>Abstraction over HidSharp's <c>DeviceList</c> so discovery can be tested with fake devices.</summary>
public interface IHidDeviceList
{
    IEnumerable<IHidDevice> GetHidDevices(int vendorId, int productId);

    IEnumerable<IHidDevice> GetHidDevices();
}
