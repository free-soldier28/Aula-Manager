using Aula.Core.Devices;

namespace Aula.Core.Protocol;

public interface IHidTransport : IDisposable
{
    DeviceInfo Info { get; }

    bool IsOpen { get; }

    void Open();

    void Close();

    void SetFeature(byte[] buffer);

    void GetFeature(byte[] buffer);

    void WriteOutput(byte[] buffer);

    int ReadInput(byte[] buffer, int timeoutMs);
}
