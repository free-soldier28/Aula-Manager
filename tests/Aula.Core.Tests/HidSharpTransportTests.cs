using Aula.Core;
using Aula.Core.Devices;
using Aula.Core.Protocol;
using Aula.Core.Tests.TestHelpers;

namespace Aula.Core.Tests;

public class HidSharpTransportTests
{
    [Fact]
    public void Info_BuildsFromDevice_WithSafeFallbacks()
    {
        var flaky = new FakeHidDevice
        {
            SerialGetter = () => throw new InvalidOperationException("serial"),
            NameGetter = () => throw new InvalidOperationException("name"),
            FeatureGetter = () => throw new InvalidOperationException("feature"),
        };
        var transport = new HidSharpTransport(flaky);

        DeviceInfo info = transport.Info;

        Assert.Equal("path://fake", info.DevicePath);
        Assert.Null(info.SerialNumber);
        Assert.Null(info.ProductName);
        Assert.Equal("AULA (258A:010C)", info.DisplayName);
        Assert.Equal(0, info.MaxFeatureReportLength);
    }

    [Fact]
    public void Open_OpensStream_AndIsOpen_True()
    {
        var stream = new FakeHidStream();
        var device = new FakeHidDevice { StreamFactory = () => stream };
        var transport = new HidSharpTransport(device);

        transport.Open();

        Assert.True(transport.IsOpen);

        transport.Open();

        Assert.Equal(1, device.OpenCount);
    }

    [Fact]
    public void Open_DeviceThrows_WrapsInTransportException()
    {
        var device = new FakeHidDevice { StreamFactory = () => throw new InvalidOperationException("no device") };
        var transport = new HidSharpTransport(device);

        var ex = Assert.Throws<AulaTransportException>(() => transport.Open());

        Assert.Contains("Failed to open", ex.Message);
        Assert.False(transport.IsOpen);
    }

    [Fact]
    public void Close_DisposesStream_AndResetsOpenFlag()
    {
        var stream = new FakeHidStream();
        var transport = new HidSharpTransport(new FakeHidDevice { StreamFactory = () => stream });
        transport.Open();
        Assert.False(stream.Disposed);

        transport.Close();

        Assert.True(stream.Disposed);
        Assert.False(transport.IsOpen);
    }

    [Fact]
    public void Operations_WithoutOpen_Throw()
    {
        var transport = new HidSharpTransport(new FakeHidDevice());

        Assert.Throws<AulaTransportException>(() => transport.SetFeature(new byte[64]));
        Assert.Throws<AulaTransportException>(() => transport.GetFeature(new byte[64]));
        Assert.Throws<AulaTransportException>(() => transport.WriteOutput(new byte[64]));
        Assert.Throws<AulaTransportException>(() => transport.ReadInput(new byte[64], 100));
    }

    [Fact]
    public void SetFeature_ForwardsToStream()
    {
        var stream = new FakeHidStream();
        byte[]? received = null;
        stream.OnSetFeature = b => received = b;
        var transport = OpenTransport(stream);
        var buffer = new byte[] { 0x06, 0x84 };

        transport.SetFeature(buffer);

        Assert.Same(buffer, received);
    }

    [Fact]
    public void SetFeature_StreamThrows_WrapsInTransportException()
    {
        var stream = new FakeHidStream { OnSetFeature = _ => throw new InvalidOperationException("busy") };
        var transport = OpenTransport(stream);

        var ex = Assert.Throws<AulaTransportException>(() => transport.SetFeature(new byte[64]));

        Assert.Contains("SetFeature failed", ex.Message);
    }

    [Fact]
    public void GetFeature_ForwardsToStream()
    {
        var stream = new FakeHidStream();
        byte[]? received = null;
        stream.OnGetFeature = b => received = b;
        var transport = OpenTransport(stream);
        var buffer = new byte[64];

        transport.GetFeature(buffer);

        Assert.Same(buffer, received);
    }

    [Fact]
    public void GetFeature_StreamThrows_WrapsInTransportException()
    {
        var stream = new FakeHidStream { OnGetFeature = _ => throw new InvalidOperationException("busy") };
        var transport = OpenTransport(stream);

        var ex = Assert.Throws<AulaTransportException>(() => transport.GetFeature(new byte[64]));

        Assert.Contains("GetFeature failed", ex.Message);
    }

    [Fact]
    public void WriteOutput_ForwardsToStream()
    {
        var stream = new FakeHidStream();
        byte[]? received = null;
        stream.OnWrite = b => received = b;
        var transport = OpenTransport(stream);
        var buffer = new byte[] { 0x13, 0x00 };

        transport.WriteOutput(buffer);

        Assert.Same(buffer, received);
    }

    [Fact]
    public void WriteOutput_StreamThrows_WrapsInTransportException()
    {
        var stream = new FakeHidStream { OnWrite = _ => throw new InvalidOperationException("busy") };
        var transport = OpenTransport(stream);

        var ex = Assert.Throws<AulaTransportException>(() => transport.WriteOutput(new byte[64]));

        Assert.Contains("WriteOutput failed", ex.Message);
    }

    [Fact]
    public void ReadInput_ReturnsBytesRead()
    {
        var stream = new FakeHidStream { OnRead = _ => 5 };
        var transport = OpenTransport(stream);

        int read = transport.ReadInput(new byte[64], 100);

        Assert.Equal(5, read);
    }

    [Fact]
    public void ReadInput_Timeout_ReturnsZero()
    {
        var stream = new FakeHidStream { OnRead = _ => throw new TimeoutException() };
        var transport = OpenTransport(stream);

        Assert.Equal(0, transport.ReadInput(new byte[64], 100));
    }

    [Fact]
    public void ReadInput_OtherException_WrapsInTransportException()
    {
        var stream = new FakeHidStream { OnRead = _ => throw new IOException("gone") };
        var transport = OpenTransport(stream);

        var ex = Assert.Throws<AulaTransportException>(() => transport.ReadInput(new byte[64], 100));

        Assert.Contains("ReadInput failed", ex.Message);
    }

    [Fact]
    public void ReadInput_AppliesAndRestoresTimeout()
    {
        var stream = new FakeHidStream { ReadTimeout = 42 };
        int? timeoutDuringRead = null;
        stream.OnRead = _ =>
        {
            timeoutDuringRead = stream.ReadTimeout;
            return 3;
        };
        var transport = OpenTransport(stream);

        transport.ReadInput(new byte[64], 100);

        Assert.Equal(100, timeoutDuringRead);
        Assert.Equal(42, stream.ReadTimeout);
    }

    [Fact]
    public void Dispose_ClosesTransport()
    {
        var stream = new FakeHidStream();
        var transport = OpenTransport(stream);

        transport.Dispose();

        Assert.True(stream.Disposed);
        Assert.False(transport.IsOpen);
    }

    private static HidSharpTransport OpenTransport(FakeHidStream stream) =>
        OpenTransportWith(new FakeHidDevice { StreamFactory = () => stream });

    private static HidSharpTransport OpenTransportWith(FakeHidDevice device)
    {
        var transport = new HidSharpTransport(device);
        transport.Open();
        return transport;
    }
}
