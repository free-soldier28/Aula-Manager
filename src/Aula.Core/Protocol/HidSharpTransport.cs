using Aula.Core.Devices;
using Aula.Core.Logging;
using Microsoft.Extensions.Logging;

namespace Aula.Core.Protocol;

public sealed class HidSharpTransport : IHidTransport
{
    private readonly IHidDevice _device;
    private readonly ILogger<HidSharpTransport> _log;
    private IHidStream? _stream;
    private bool _open;

    public HidSharpTransport(IHidDevice device)
    {
        _device = device;
        _log = AulaLogging.Logger<HidSharpTransport>();
    }

    public DeviceInfo Info
    {
        get
        {
            string? serial = SafeString(_device.GetSerialNumber, "serial");
            string? name = SafeString(_device.GetProductName, "name");
            int featureLength = SafeInt(_device.GetMaxFeatureReportLength, "feature");
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

        _log.LogInformation("Opening device {Path}", _device.DevicePath);
        try
        {
            _stream = _device.Open();
            _open = true;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to open device {Path}", _device.DevicePath);
            throw new AulaTransportException($"Failed to open device '{_device.DevicePath}': {ex.Message}", ex);
        }
    }

    public void Close()
    {
        if (_open)
        {
            _log.LogDebug("Closing device {Path}", _device.DevicePath);
        }

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
            _log.LogError(ex, "SetFeature failed on {Path}", _device.DevicePath);
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
            _log.LogError(ex, "GetFeature failed on {Path}", _device.DevicePath);
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
            _log.LogError(ex, "WriteOutput failed on {Path}", _device.DevicePath);
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
            _log.LogDebug("ReadInput timed out on {Path} after {Timeout}ms", _device.DevicePath, timeoutMs);
            return 0;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "ReadInput failed on {Path}", _device.DevicePath);
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

    private string? SafeString(Func<string?> getter, string field)
    {
        try
        {
            return getter();
        }
        catch (Exception ex)
        {
            _log.LogDebug("Failed to read {Field} for {Path}: {Message}", field, _device.DevicePath, ex.Message);
            return null;
        }
    }

    private int SafeInt(Func<int> getter, string field)
    {
        try
        {
            return getter();
        }
        catch (Exception ex)
        {
            _log.LogDebug("Failed to read {Field} for {Path}: {Message}", field, _device.DevicePath, ex.Message);
            return 0;
        }
    }
}
