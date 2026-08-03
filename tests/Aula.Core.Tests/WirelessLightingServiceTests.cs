using Aula.Core.Models;
using Aula.Core.Protocol;
using Aula.Core.Services;
using Aula.Core.Tests.TestHelpers;

namespace Aula.Core.Tests;

public class WirelessLightingServiceTests
{
    private static byte[] ConfigFragment(byte sequence, byte effectId = 0)
    {
        byte[] frame = WirelessFrame.Build(
            WirelessFrame.CmdRead, WirelessFrame.SubCmdConfig, sequence,
            new byte[WirelessFrame.PayloadLength]);
        frame[4] = 0x0E;
        frame[15] = effectId;
        frame[19] = WirelessFrame.Checksum(frame);
        return frame;
    }

    private static byte[] Echo(byte command) =>
        WirelessFrame.Build(command, 0x00, 0, new byte[WirelessFrame.PayloadLength]);

    private static void EnqueueFullConversation(FakeTransport transport, bool withConfig = true)
    {
        if (withConfig)
        {
            for (byte i = 0; i < 10; i++)
            {
                transport.Responses.Enqueue(ConfigFragment(i, (byte)(i == 0 ? 10 : 0)));
            }
        }

        for (int i = 0; i < 10; i++)
        {
            transport.Responses.Enqueue(Echo(WirelessFrame.CmdWrite));
        }

        for (int i = 0; i < 37; i++)
        {
            transport.Responses.Enqueue(Echo(WirelessFrame.CmdColor));
        }

        transport.Responses.Enqueue(Echo(WirelessFrame.CmdSave));
    }

    private static WirelessLightingService Create(FakeTransport transport) =>
        new(new WirelessProtocol(transport), ModelConfig.F75);

    [Fact]
    public void ReadConfig_ReturnsWirelessConfig()
    {
        var transport = new FakeTransport();
        for (byte i = 0; i < 10; i++)
        {
            transport.Responses.Enqueue(ConfigFragment(i, (byte)(i == 0 ? 10 : 0)));
        }

        KeyboardConfig config = Create(transport).ReadConfig();

        Assert.IsType<WirelessConfig>(config);
        Assert.Equal(10, config.EffectId);
    }

    [Fact]
    public void Apply_ModifiesReadConfigFragments()
    {
        var transport = new FakeTransport();
        EnqueueFullConversation(transport, withConfig: true);

        Create(transport).Apply(new LightingConfig(EffectId: 3, Brightness: 5, Speed: 2));

        byte[] firstWrite = transport.Sent[1];
        Assert.Equal(WirelessFrame.CmdWrite, firstWrite[1]);
        Assert.Equal(0x03, firstWrite[15]);
        Assert.Equal(0x03, firstWrite[17]);

        byte[] paramsFragment = transport.Sent[1 + 4];
        Assert.Equal(0x05, paramsFragment[11]);
        Assert.Equal(0x20, paramsFragment[12]);
    }

    [Fact]
    public void Apply_BuildsFromTemplate_WhenConfigUnavailable()
    {
        var transport = new FakeTransport();
        EnqueueFullConversation(transport, withConfig: false);

        Create(transport).Apply(new LightingConfig(EffectId: 3, Brightness: 5, Speed: 2));

        byte[] firstWrite = transport.Sent[1];
        Assert.Equal(WirelessFrame.CmdWrite, firstWrite[1]);
        Assert.Equal(0x03, firstWrite[15]);
        Assert.Equal(0x03, firstWrite[17]);

        byte[] paramsFragment = transport.Sent[5];
        Assert.Equal(0x05, paramsFragment[11]);
        Assert.Equal(0x27, paramsFragment[12]);
    }

    [Fact]
    public void Apply_WithColor_WritesColorPalette()
    {
        var transport = new FakeTransport();
        EnqueueFullConversation(transport);

        Create(transport).Apply(new LightingConfig(EffectId: 1, Color: new RgbColor(0xFF, 0x00, 0x80)));

        byte[] paletteFragment = transport.Sent[1 + 10 + 1];
        Assert.Equal(WirelessFrame.CmdColor, paletteFragment[1]);
        Assert.Equal(0xFF, paletteFragment[12]);
        Assert.Equal(0x00, paletteFragment[13]);
        Assert.Equal(0x80, paletteFragment[14]);
    }

    [Fact]
    public void Apply_WithPerKeyColors_WritesPerKeyMap()
    {
        var transport = new FakeTransport();
        EnqueueFullConversation(transport);

        var colors = new RgbColor[30];
        colors[0] = new RgbColor(0xFF, 0x00, 0x00);
        colors[1] = new RgbColor(0x00, 0xFF, 0x00);
        colors[2] = new RgbColor(0x00, 0x00, 0xFF);
        for (int i = 3; i < colors.Length; i++)
        {
            colors[i] = new RgbColor(0x10, 0x20, 0x30);
        }

        Create(transport).Apply(new LightingConfig(EffectId: 21, PerKeyColors: colors));

        byte[] redPlane = transport.Sent[1 + 10];
        Assert.Equal(WirelessFrame.CmdPerKey, redPlane[1]);
        Assert.Equal(0x0E, redPlane[4]);
        Assert.Equal(0xFF, redPlane[5]);
        Assert.Equal(0x00, redPlane[6]);
        Assert.Equal(0x00, redPlane[7]);

        byte[] trailer = transport.Sent[1 + 10 + 27];
        Assert.Equal(WirelessFrame.CmdPerKey, trailer[1]);
        Assert.Equal(0x06, trailer[4]);
        Assert.Equal(0x5A, trailer[7]);
        Assert.Equal(0xA5, trailer[8]);
    }

    [Fact]
    public void Apply_UnknownEffect_Throws()
    {
        var transport = new FakeTransport();
        Assert.Throws<AulaProtocolException>(() => Create(transport).Apply(new LightingConfig(EffectId: 99)));
    }

    [Fact]
    public void Apply_WithRawFlags_WritesFlagsDirectly()
    {
        var transport = new FakeTransport();
        EnqueueFullConversation(transport);

        Create(transport).Apply(new LightingConfig(EffectId: 3, RawFlags: 0x37));

        byte[] paramsFragment = transport.Sent[1 + 4];
        Assert.Equal(0x37, paramsFragment[12]);
    }

    [Fact]
    public void TurnOff_AppliesEffectZero()
    {
        var transport = new FakeTransport();
        EnqueueFullConversation(transport);

        Create(transport).TurnOff();

        byte[] firstWrite = transport.Sent[1];
        Assert.Equal(0x00, firstWrite[15]);
    }

    [Fact]
    public void Reset_WritesTemplateConfig_AndPalette_AndSave()
    {
        var transport = new FakeTransport();
        EnqueueFullConversation(transport, withConfig: false);

        Create(transport).Reset();

        Assert.Equal(10 + 37 + 1, transport.Sent.Count);
        Assert.Equal(WirelessFrame.CmdWrite, transport.Sent[0][1]);
        Assert.Equal(WirelessFrame.CmdColor, transport.Sent[10][1]);
        Assert.Equal(WirelessFrame.CmdSave, transport.Sent[^1][1]);
    }

    [Fact]
    public void FindEffect_ById_AndByName()
    {
        var service = Create(new FakeTransport());

        Assert.Equal(3, service.FindEffect(3)?.Id);
        Assert.Null(service.FindEffect(99));
        Assert.Equal(3, service.FindEffect("WAVE")?.Id);
        Assert.Null(service.FindEffect("nonexistent"));
    }

    [Fact]
    public void GetParams_ReturnsNull_ForOutOfRangeEffect()
    {
        var transport = new FakeTransport();
        KeyboardConfig config = Create(transport).ReadConfig();

        Assert.Null(config.GetParams(0));
    }
}
