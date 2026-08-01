using Aula.Core.Devices;
using Aula.Core.Protocol;

namespace Aula.Core.Tests.TestHelpers;

public sealed class FakeTransport : IHidTransport
{
    public readonly List<byte[]> Sent = new();
    public readonly Queue<byte[]> Responses = new();

    public DeviceInfo Info { get; set; } =
        new("fake://device", 0x258A, 0x010C, "SN123", "AULA F75", MaxFeatureReportLength: 520);

    public bool IsOpen { get; private set; }

    public void Open() => IsOpen = true;

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

    public void Dispose() => Close();

    public byte[] LastSent() => Sent[^1];
}
