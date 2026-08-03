namespace Aula.Core.Protocol;

/// <summary>
/// 2.4 GHz wireless receiver protocol (same family as the AULA F87 / F99 Pro).
/// All communication uses 20-byte HID Output Reports with Report ID 0x13:
///   [0] Report ID (0x13), [1] command, [2] sub-command, [3] sequence,
///   [4-18] 15-byte payload, [19] checksum = sum(bytes 0-18) & 0xFF.
/// Every fragment sent by the host is echoed back by the receiver.
/// </summary>
public static class WirelessFrame
{
    public const byte ReportId = 0x13;
    public const int FragmentLength = 20;
    public const int PayloadLength = 15;

    public const byte CmdRead = 0x44;
    public const byte CmdWrite = 0x04;
    public const byte CmdColor = 0x09;
    public const byte CmdPerKey = 0x02;
    public const byte CmdSave = 0x0A;

    public const byte SubCmdConfig = 0x0A;
    public const byte SubCmdPalette = 0x25;
    public const byte SubCmdPerKey = 0x1C;
    public const byte SubCmdConfirm = 0x01;

    public static byte[] Build(byte command, byte subCommand, byte sequence, ReadOnlySpan<byte> payload)
    {
        var frame = new byte[FragmentLength];
        frame[0] = ReportId;
        frame[1] = command;
        frame[2] = subCommand;
        frame[3] = sequence;

        int count = Math.Min(PayloadLength, payload.Length);
        for (int i = 0; i < count; i++)
        {
            frame[4 + i] = payload[i];
        }

        frame[19] = Checksum(frame);
        return frame;
    }

    public static byte Checksum(ReadOnlySpan<byte> frame)
    {
        byte sum = 0;
        for (int i = 0; i < 19 && i < frame.Length; i++)
        {
            sum += frame[i];
        }

        return sum;
    }
}
