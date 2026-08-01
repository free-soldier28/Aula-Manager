using Aula.Core.Models;
using Aula.Core.Protocol;

namespace Aula.Core.Tests;

public class F75ReportTests
{
    private static readonly ModelConfig Model = ModelConfig.F75;

    [Fact]
    public void Create_BuildsConfigReadFrame()
    {
        var frame = F75Report.CreateConfigRead(Model);

        Assert.Equal(520, frame.Length);
        Assert.Equal(0x06, frame[0]);
        Assert.Equal(0x84, frame[1]);
        Assert.Equal(new byte[] { 0x00, 0x00, 0x01, 0x00 }, frame[2..6]);
        Assert.Equal(0x80, frame[6]);
        Assert.Equal(0x00, frame[7]);
        Assert.True(frame[8..].All(b => b == 0));
    }

    [Fact]
    public void CreateConfigWrite_CopiesPayload_AndSetsWriteCommand()
    {
        var response = BuildConfigResponse();
        var frame = F75Report.CreateConfigWrite(Model, response);

        Assert.Equal(520, frame.Length);
        Assert.Equal(0x06, frame[0]);
        Assert.Equal(0x04, frame[1]);
        Assert.Equal(response[8..], frame[8..136]);
        Assert.True(frame[136..].All(b => b == 0));
    }

    [Fact]
    public void CreateConfigWrite_Throws_WhenResponseTooShort()
    {
        Assert.Throws<AulaProtocolException>(() => F75Report.CreateConfigWrite(Model, new byte[10]));
    }

    [Fact]
    public void CreateColorProfile_SetsLed0Color_AndTerminator()
    {
        var frame = F75Report.CreateColorProfile(Model, new RgbColor(0xFF, 0x00, 0x00));

        Assert.Equal(520, frame.Length);
        Assert.Equal(0x0A, frame[1]);
        Assert.Equal(0xFF, frame[29]);
        Assert.Equal(0x00, frame[30]);
        Assert.Equal(0x00, frame[31]);
        Assert.Equal(0x5A, frame[0x202]);
        Assert.Equal(0xA5, frame[0x203]);
    }

    [Fact]
    public void CreateModelQuery_MatchesGoldenCapture()
    {
        var frame = F75Report.CreateModelQuery(Model);

        Assert.Equal(new byte[] { 0x06, 0x82, 0x01, 0x00, 0x01, 0x00, 0x06 }, frame[..7]);
        Assert.True(frame[7..].All(b => b == 0));
    }

    public static byte[] BuildConfigResponse()
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
        response[12] = 0x00;
        response[13] = 0x00;
        response[14] = 0x04;
        response[15] = 0x04;
        response[16] = 0x07;
        response[17] = 0x00;
        response[18] = 0x0A;
        response[19] = 0x20;
        response[26] = 0x01;
        response[36] = 0x00;
        response[64] = 0xFF;
        response[65] = 0xFF;

        response[66] = 0x04;
        response[67] = 0x07;
        response[84] = 0x03;
        response[85] = 0x47;

        response[134] = 0x5A;
        response[135] = 0xA5;
        return response;
    }
}
