using System.Net;
using Aula.Core.Abstractions;
using Aula.Core.Devices;
using Aula.Core.Protocol;

namespace Aula.Cli.Tests;

public sealed class FakeScanner : IHidDeviceScanner
{
    public List<DeviceInfo> Devices { get; } = new();

    public IReadOnlyList<DeviceInfo> Scan(int vendorId, int productId) =>
        Devices.Where(d => d.VendorId == vendorId && d.ProductId == productId).ToList();

    public IReadOnlyList<DeviceInfo> ScanAll() => Devices.ToList();
}

public sealed class ThrowingScanner : IHidDeviceScanner
{
    public Exception Exception { get; } = new IOException("scan boom");

    public IReadOnlyList<DeviceInfo> Scan(int vendorId, int productId) => throw Exception;

    public IReadOnlyList<DeviceInfo> ScanAll() => throw Exception;
}

public sealed class FakeTransport : IHidTransport
{
    public readonly List<byte[]> Sent = new();
    public readonly Queue<byte[]> Responses = new();

    public DeviceInfo Info { get; set; } =
        new("path://f75", 0x258A, 0x010C, "SN123", "AULA F75", 520, 0, 0);

    public bool IsOpen { get; private set; }

    public Exception? OpenException { get; set; }

    public void Open()
    {
        if (OpenException is not null)
        {
            throw OpenException;
        }

        IsOpen = true;
    }

    public void Close() => IsOpen = false;

    public void SetFeature(byte[] buffer) => Sent.Add((byte[])buffer.Clone());

    public void GetFeature(byte[] buffer)
    {
        if (Responses.Count == 0)
        {
            throw new InvalidOperationException("No response queued in FakeTransport.");
        }

        byte[] response = Responses.Dequeue();
        Array.Clear(buffer, 0, buffer.Length);
        Array.Copy(response, 0, buffer, 0, Math.Min(response.Length, buffer.Length));
    }

    public void WriteOutput(byte[] buffer) => Sent.Add((byte[])buffer.Clone());

    public int ReadInput(byte[] buffer, int timeoutMs)
    {
        if (Responses.Count == 0)
        {
            return 0;
        }

        byte[] response = Responses.Dequeue();
        Array.Clear(buffer, 0, buffer.Length);
        Array.Copy(response, 0, buffer, 0, Math.Min(response.Length, buffer.Length));
        return response.Length;
    }

    public void Dispose() => Close();
}

public sealed class FakeTransportFactory : ITransportFactory
{
    private readonly Func<DeviceInfo, FakeTransport> _create;

    public FakeTransportFactory(Func<DeviceInfo, FakeTransport>? create = null) =>
        _create = create ?? (_ => new FakeTransport());

    public IHidTransport Create(DeviceInfo device) => _create(device);
}

public static class CliFakes
{    public static DeviceInfo F75Device() =>
        new("path://f75", AulaDeviceIds.VendorSinoWealth, AulaDeviceIds.ProductF75F87Wired, "SN", "AULA F75", 520, 0, 0);

    public static DeviceInfo DongleDevice() =>
        new("path://dongle", AulaDeviceIds.VendorWireless, AulaDeviceIds.ProductWireless, "SN", "2.4G Wireless Receiver", 0, 20, 20);

    public static byte[] ConfigResponse()
    {
        var response = new byte[136];
        response[0] = 0x06;
        response[1] = 0x84;
        response[4] = 0x01;
        response[6] = 0x80;
        response[8] = 0x00;
        response[9] = 0x03;
        response[10] = 0x03;
        response[11] = 0x01;
        response[17] = 0x00;
        response[18] = 0x0A;
        response[26] = 0x01;
        response[64] = 0xFF;
        response[65] = 0xFF;
        response[66] = 0x04;
        response[67] = 0x07;
        response[134] = 0x5A;
        response[135] = 0xA5;
        return response;
    }

    public static byte[] ModelResponse()
    {
        var response = new byte[520];
        response[0] = 0x06;
        response[1] = 0x82;
        response[8] = 0x03;
        response[12] = 0x03;
        response[13] = 0x66;
        return response;
    }

    public static byte[] ColorProfileResponse()
    {
        var response = new byte[520];
        response[0] = 0x06;
        response[1] = 0x8A;
        response[29] = 0xFF;
        response[30] = 0x00;
        response[31] = 0x00;
        return response;
    }

    public static byte[] WirelessReadFragment(byte sequence)
    {
        var payload = new byte[WirelessFrame.PayloadLength];
        payload[0] = 0x0E;
        if (sequence == 0)
        {
            payload[11] = 0x03;
        }

        return WirelessFrame.Build(WirelessFrame.CmdRead, WirelessFrame.SubCmdConfig, sequence, payload);
    }
}

public sealed class FakeHttpHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses = new();
    private readonly Queue<Exception> _exceptions = new();

    public void Enqueue(HttpResponseMessage response) => _responses.Enqueue(response);

    public void Throw(Exception exception) => _exceptions.Enqueue(exception);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_exceptions.Count > 0)
        {
            throw _exceptions.Dequeue();
        }

        return Task.FromResult(
            _responses.Count > 0 ? _responses.Dequeue() : new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}