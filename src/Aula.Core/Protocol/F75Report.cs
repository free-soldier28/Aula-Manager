using Aula.Core.Models;

namespace Aula.Core.Protocol;

public static class F75Report
{
    public const byte CmdModelQuery = 0x82;
    public const byte CmdReadConfig = 0x84;
    public const byte CmdWriteConfig = 0x04;
    public const byte CmdReadColorProfile = 0x8A;
    public const byte CmdColorProfile = 0x0A;
    public const byte CmdPerKey = 0x06;

    public static byte[] Create(ModelConfig model, byte command, byte a0, byte a1, byte a2, byte a3, ushort length)
    {
        var buffer = new byte[model.ReportLength];
        buffer[0] = model.ReportId;
        buffer[1] = command;
        buffer[2] = a0;
        buffer[3] = a1;
        buffer[4] = a2;
        buffer[5] = a3;
        buffer[6] = (byte)(length & 0xFF);
        buffer[7] = (byte)(length >> 8);
        return buffer;
    }

    public static byte[] CreateConfigRead(ModelConfig model) =>
        Create(model, CmdReadConfig, 0x00, 0x00, 0x01, 0x00, 0x0080);

    public static byte[] CreateConfigWrite(ModelConfig model, byte[] configResponse)
    {
        if (configResponse.Length < model.ConfigResponseLength)
        {
            throw new AulaProtocolException(
                $"Config response is {configResponse.Length} bytes, expected at least {model.ConfigResponseLength}.");
        }

        var buffer = Create(model, CmdWriteConfig, 0x00, 0x00, 0x01, 0x00, 0x0080);
        int payload = model.ConfigResponseLength - model.ConfigHeaderLength;
        Array.Copy(configResponse, model.ConfigHeaderLength, buffer, model.ConfigHeaderLength, payload);
        return buffer;
    }

    public static byte[] CreateColorProfile(ModelConfig model, RgbColor color)
    {
        var buffer = new byte[model.ReportLength];
        buffer[0] = model.ReportId;
        buffer[1] = CmdColorProfile;
        buffer[7] = 0x02;
        buffer[29] = color.R;
        buffer[30] = color.G;
        buffer[31] = color.B;
        buffer[0x202] = 0x5A;
        buffer[0x203] = 0xA5;
        return buffer;
    }

    public static byte[] CreateColorProfileRead(ModelConfig model)
    {
        var buffer = new byte[model.ReportLength];
        buffer[0] = model.ReportId;
        buffer[1] = CmdReadColorProfile;
        buffer[7] = 0x02;
        return buffer;
    }

    public static byte[] CreatePerKeyColors(ModelConfig model, IReadOnlyList<RgbColor> colors)
    {
        int ledCount = colors.Count;

        var buffer = new byte[model.ReportLength];
        buffer[0] = model.ReportId;
        buffer[1] = CmdPerKey;
        buffer[4] = 0x01;
        buffer[6] = (byte)ledCount;
        buffer[7] = 0x01;

        for (int i = 0; i < ledCount; i++)
        {
            buffer[0x08 + i] = colors[i].R;
            buffer[0x86 + i] = colors[i].G;
            buffer[0x104 + i] = colors[i].B;
        }

        return buffer;
    }

    public static byte[] CreateModelQuery(ModelConfig model)
    {
        var buffer = new byte[model.ReportLength];
        buffer[0] = model.ReportId;
        buffer[1] = CmdModelQuery;
        buffer[2] = 0x01;
        buffer[3] = 0x00;
        buffer[4] = 0x01;
        buffer[5] = 0x00;
        buffer[6] = 0x06;
        return buffer;
    }
}
