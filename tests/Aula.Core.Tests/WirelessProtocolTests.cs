using Aula.Core.Protocol;
using Aula.Core.Tests.TestHelpers;

namespace Aula.Core.Tests;

public class WirelessProtocolTests
{
    private static byte[] ConfigFragment(byte sequence, byte? payload = null)
    {
        byte[] fragment = WirelessFrame.Build(
            WirelessFrame.CmdRead, WirelessFrame.SubCmdConfig, sequence, new[] { payload ?? sequence });
        fragment[19] = WirelessFrame.Checksum(fragment);
        return fragment;
    }

    [Fact]
    public void ReadConfig_CollectsFragmentsBySequence()
    {
        var transport = new FakeTransport();
        for (byte i = 0; i < 10; i++)
        {
            transport.Responses.Enqueue(ConfigFragment(i));
        }

        var protocol = new WirelessProtocol(transport);
        byte[]?[] config = protocol.ReadConfig();

        Assert.Equal(10, config.Length);
        Assert.All(config, Assert.NotNull);
        Assert.Equal(0x13, config[0]![0]);
        Assert.Equal(0x44, config[0]![1]);
        Assert.Equal(WirelessFrame.CmdRead, transport.LastSent()[1]);
    }

    [Fact]
    public void ReadConfig_StopsWhenNoMoreResponses()
    {
        var transport = new FakeTransport();
        transport.Responses.Enqueue(ConfigFragment(0));

        var protocol = new WirelessProtocol(transport);
        byte[]?[] config = protocol.ReadConfig();

        Assert.NotNull(config[0]);
        Assert.Null(config[1]);
    }

    [Fact]
    public void ReadConfig_IgnoresFragmentsThatAreNotConfig()
    {
        var transport = new FakeTransport();
        transport.Responses.Enqueue(WirelessFrame.Build(0x44, 0x99, 0, new byte[1]));
        transport.Responses.Enqueue(ConfigFragment(2));

        var protocol = new WirelessProtocol(transport);
        byte[]?[] config = protocol.ReadConfig();

        Assert.Null(config[0]);
        Assert.NotNull(config[2]);
    }

    [Fact]
    public void ReadConfig_IgnoresOutOfRangeSequence()
    {
        var transport = new FakeTransport();
        transport.Responses.Enqueue(ConfigFragment(30));

        var protocol = new WirelessProtocol(transport);
        byte[]?[] config = protocol.ReadConfig();

        Assert.All(config, Assert.Null);
    }

    [Fact]
    public void WriteConfig_CountsEchoes()
    {
        var transport = new FakeTransport();
        byte[][] fragments = Enumerable.Range(0, 4)
            .Select(i => WirelessFrame.Build(WirelessFrame.CmdWrite, WirelessFrame.SubCmdConfig, (byte)i, new byte[] { 0x01 }))
            .ToArray();

        for (int i = 0; i < 4; i++)
        {
            transport.Responses.Enqueue(WirelessFrame.Build(WirelessFrame.CmdWrite, 0x0A, (byte)i, new byte[1]));
        }

        var protocol = new WirelessProtocol(transport);
        int echoes = protocol.WriteConfig(fragments);

        Assert.Equal(4, echoes);
    }

    [Fact]
    public void WriteBatch_CountsEchoes()
    {
        var transport = new FakeTransport();
        byte[][] fragments = Enumerable.Range(0, 3)
            .Select(i => WirelessFrame.Build(WirelessFrame.CmdColor, WirelessFrame.SubCmdPalette, (byte)i, new byte[] { 0x01 }))
            .ToArray();

        transport.Responses.Enqueue(WirelessFrame.Build(WirelessFrame.CmdColor, 0x25, 0, new byte[1]));

        var protocol = new WirelessProtocol(transport);
        int echoes = protocol.WriteBatch(fragments);

        Assert.Equal(1, echoes);
    }

    [Fact]
    public void WriteAndAwaitEcho_ReturnsTrue_WhenEchoMatches()
    {
        var transport = new FakeTransport();
        transport.Responses.Enqueue(WirelessFrame.Build(WirelessFrame.CmdWrite, 0x0A, 0, new byte[1]));
        byte[] fragment = WirelessFrame.Build(WirelessFrame.CmdWrite, WirelessFrame.SubCmdConfig, 0, new byte[] { 0x01 });

        var protocol = new WirelessProtocol(transport);
        bool echoed = protocol.WriteAndAwaitEcho(fragment);

        Assert.True(echoed);
    }

    [Fact]
    public void WriteAndAwaitEcho_ReturnsFalse_WhenNoResponse()
    {
        var transport = new FakeTransport();
        byte[] fragment = WirelessFrame.Build(WirelessFrame.CmdWrite, WirelessFrame.SubCmdConfig, 0, new byte[] { 0x01 });

        var protocol = new WirelessProtocol(transport);
        bool echoed = protocol.WriteAndAwaitEcho(fragment);

        Assert.False(echoed);
    }

    [Fact]
    public void WriteAndAwaitEcho_ReturnsFalse_WhenEchoWrongCommand()
    {
        var transport = new FakeTransport();
        transport.Responses.Enqueue(WirelessFrame.Build(WirelessFrame.CmdColor, 0x25, 0, new byte[1]));
        byte[] fragment = WirelessFrame.Build(WirelessFrame.CmdWrite, WirelessFrame.SubCmdConfig, 0, new byte[] { 0x01 });

        var protocol = new WirelessProtocol(transport);
        bool echoed = protocol.WriteAndAwaitEcho(fragment);

        Assert.False(echoed);
    }

    [Fact]
    public void DeviceInfo_ProxiesTransport()
    {
        var transport = new FakeTransport();
        var protocol = new WirelessProtocol(transport);

        Assert.Same(transport.Info, protocol.DeviceInfo);
    }

    [Fact]
    public void HasValidChecksum_ValidatesFinalByte()
    {
        byte[] valid = WirelessFrame.Build(0x44, 0x0A, 0, new byte[] { 0x01 });

        Assert.True(WirelessProtocol.HasValidChecksum(valid));

        valid[19] = (byte)(valid[19] ^ 0xFF);
        Assert.False(WirelessProtocol.HasValidChecksum(valid));
    }

    [Fact]
    public void HasValidChecksum_False_WhenTooShort()
    {
        Assert.False(WirelessProtocol.HasValidChecksum(new byte[10]));
    }
}
