using Aula.Core.Devices;
using HidSharp;

namespace Aula.Core.Protocol;

public sealed class HidSharpTransport : IHidTransport
{
    private readonly HidDevice _device;
    private HidStream? _stream;
    private bool _open;

    public HidSharpTransport(HidDevice device) => _device = device;

    public DeviceInfo Info
    {
        get
        {
            string? serial = SafeString(_device.GetSerialNumber);
            string? name = SafeString(_device.GetProductName);
            int featureLength = SafeInt(_device.GetMaxFeatureReportLength);
            return new DeviceInfo(_device.DevicePath, _device.VendorID, _device.ProductID, serial, name, featureLength);
        }
    }

    public bool IsOpen => _open;

    public void Open()
    {
        if (_open)
        {
            return;
        }

        try
        {
            _stream = _device.Open();
            _open = true;
        }
        catch (Exception ex)
        {
            throw new AulaTransportException($"Failed to open device '{_device.DevicePath}': {ex.Message}", ex);
        }
    }

    public void Close()
    {
        _stream?.Dispose();
        _stream = null;
        _open = false;
    }

    public void SetFeature(byte[] buffer)
    {
        EnsureOpen();

        try
        {
            _stream!.SetFeature(buffer);
        }
        catch (Exception ex)
        {
            throw new AulaTransportException($"SetFeature failed: {ex.Message}", ex);
        }
    }

    public void GetFeature(byte[] buffer)
    {
        EnsureOpen();

        try
        {
            _stream!.GetFeature(buffer);
        }
        catch (Exception ex)
        {
            throw new AulaTransportException($"GetFeature failed: {ex.Message}", ex);
        }
    }

    public void WriteOutput(byte[] buffer)
    {
        EnsureOpen();

        try
        {
            _stream!.Write(buffer);
        }
        catch (Exception ex)
        {
            throw new AulaTransportException($"WriteOutput failed: {ex.Message}", ex);
        }
    }

    public int ReadInput(byte[] buffer, int timeoutMs)
    {
        EnsureOpen();

        int previous = _stream!.ReadTimeout;
        try
        {
            _stream.ReadTimeout = timeoutMs;
            return _stream.Read(buffer);
        }
        catch (TimeoutException)
        {
            return 0;
        }
        catch (Exception ex)
        {
            throw new AulaTransportException($"ReadInput failed: {ex.Message}", ex);
        }
        finally
        {
            _stream.ReadTimeout = previous;
        }
    }

    public void Dispose() => Close();

    private void EnsureOpen()
    {
        if (!_open)
        {
            throw new AulaTransportException("Device is not open.");
        }
    }

    private static string? SafeString(Func<string?> getter)
    {
        try
        {
            return getter();
        }
        catch
        {
            return null;
        }
    }

    private static int SafeInt(Func<int> getter)
    {
        try
        {
            return getter();
        }
        catch
        {
            return 0;
        }
    }
}
