using Aula.Core.Devices;
using Aula.Core.Logging;
using Microsoft.Extensions.Logging;

namespace Aula.Core.Protocol;

/// <summary>
/// Wire-level client for the 2.4 GHz receiver protocol (Report ID 0x13).
/// Provides fragment framing, echo-wait, config read and config write.
/// </summary>
public sealed class WirelessProtocol
{
    private readonly IHidTransport _transport;
    private readonly int _readTimeoutMs;
    private readonly ILogger<WirelessProtocol> _log;

    public WirelessProtocol(IHidTransport transport, int readTimeoutMs = 300)
    {
        _transport = transport;
        _readTimeoutMs = readTimeoutMs;
        _log = AulaLogging.Logger<WirelessProtocol>();
    }

    public DeviceInfo DeviceInfo => _transport.Info;

    /// <summary>
    /// Sends a READ/CONFIRM request and collects the config fragments echoed by the receiver.
    /// Returns an array of 10 fragments (null for any sequence that was not received).
    /// </summary>
    public byte[]?[] ReadConfig(int maxReads = 15)
    {
        var request = WirelessFrame.Build(WirelessFrame.CmdRead, WirelessFrame.SubCmdConfirm, 0, ReadOnlySpan<byte>.Empty);
        _transport.WriteOutput(request);

        var config = new byte[10][];
        for (int i = 0; i < maxReads; i++)
        {
            var buffer = new byte[WirelessFrame.FragmentLength];
            int read = _transport.ReadInput(buffer, _readTimeoutMs);
            if (read <= 0)
            {
                break;
            }

            if (buffer[0] == WirelessFrame.ReportId
                && buffer[1] == WirelessFrame.CmdRead
                && buffer[2] == WirelessFrame.SubCmdConfig)
            {
                byte sequence = buffer[3];
                if (sequence < config.Length)
                {
                    config[sequence] = (byte[])buffer.Clone();
                }
            }
        }

        int received = config.Count(f => f is not null);
        if (received < config.Length)
        {
            _log.LogWarning("Read config: received {Received}/10 fragments", received);
        }
        else
        {
            _log.LogDebug("Read config: received {Received}/10 fragments", received);
        }

        return config;
    }

    /// <summary>Writes the 10 config fragments (already converted to WRITE command), waiting for echoes.</summary>
    public int WriteConfig(IEnumerable<byte[]> writeFragments)
    {
        int echoes = WriteBatch(writeFragments);
        _log.LogDebug("Wrote config with {Echoes}/{Count} echoes", echoes, writeFragments.Count());
        return echoes;
    }

    /// <summary>Writes a batch of fragments and waits for the echo of each.</summary>
    public int WriteBatch(IEnumerable<byte[]> fragments)
    {
        int echoes = 0;
        foreach (byte[] fragment in fragments)
        {
            if (WriteAndAwaitEcho(fragment))
            {
                echoes++;
            }
        }

        return echoes;
    }

    public bool WriteAndAwaitEcho(byte[] fragment)
    {
        _transport.WriteOutput(fragment);

        var buffer = new byte[WirelessFrame.FragmentLength];
        for (int i = 0; i < 5; i++)
        {
            int read = _transport.ReadInput(buffer, _readTimeoutMs);
            if (read <= 0)
            {
                _log.LogDebug("No echo for fragment command 0x{Cmd:X2}", fragment[1]);
                return false;
            }

            if (buffer[0] == WirelessFrame.ReportId && buffer[1] == fragment[1])
            {
                return true;
            }
        }

        _log.LogDebug("Echo not confirmed after retries for command 0x{Cmd:X2}", fragment[1]);
        return false;
    }

    public static bool HasValidChecksum(ReadOnlySpan<byte> frame) =>
        frame.Length >= WirelessFrame.FragmentLength
        && frame[19] == WirelessFrame.Checksum(frame);
}
