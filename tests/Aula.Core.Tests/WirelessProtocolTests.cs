using Aula.Core.Models;
using Aula.Core.Protocol;
using Aula.Core.Services;
using Aula.Core.Tests.TestHelpers;

namespace Aula.Core.Tests;

public class WirelessFrameTests
{
    [Fact]
    public void Build_SetsReportIdCommandSubCommandAndSequence()
    {
        var frame = WirelessFrame.Build(WirelessFrame.CmdRead, WirelessFrame.SubCmdConfirm, 0, Array.Empty<byte>());

        Assert.Equal(20, frame.Length);
        Assert.Equal(WirelessFrame.ReportId, frame[0]);
        Assert.Equal(WirelessFrame.CmdRead, frame[1]);
        Assert.Equal(WirelessFrame.SubCmdConfirm, frame[2]);
        Assert.Equal(0, frame[3]);
    }

    [Fact]
    public void Build_PadsPayloadAndComputesChecksum()
    {
        var frame = WirelessFrame.Build(WirelessFrame.CmdWrite, WirelessFrame.SubCmdConfig, 3, new byte[] { 0x0E, 0x01 });

        Assert.Equal(0x0E, frame[4]);
        Assert.Equal(0x01, frame[5]);
        Assert.Equal(0, frame[6]);
        Assert.Equal(WirelessFrame.Checksum(frame), frame[19]);
        Assert.True(WirelessProtocol.HasValidChecksum(frame));
    }

    [Fact]
    public void Build_TruncatesOverlongPayload()
    {
        var frame = WirelessFrame.Build(WirelessFrame.CmdWrite, WirelessFrame.SubCmdConfig, 0, new byte[WirelessFrame.PayloadLength + 5]);

        Assert.Equal(20, frame.Length);
        Assert.Equal(0, frame[18]);
    }
}

public class WirelessProtocolTests
{
    private static byte[] ReadFragment(byte sequence)
    {
        var payload = new byte[WirelessFrame.PayloadLength];
        payload[0] = 0x0E;
        payload[1] = sequence;
        if (sequence == 0)
        {
            payload[11] = 0x03;
        }

        return WirelessFrame.Build(WirelessFrame.CmdRead, WirelessFrame.SubCmdConfig, sequence, payload);
    }

    [Fact]
    public void ReadConfig_CollectsAllFragments()
    {
        var transport = new FakeTransport();
        for (byte i = 0; i < 10; i++)
        {
            transport.Responses.Enqueue(ReadFragment(i));
        }

        var protocol = new WirelessProtocol(transport);
        byte[]?[] config = protocol.ReadConfig();

        Assert.Equal(0x44, transport.LastSent()[1]);
        Assert.Equal(10, config.Count(f => f is not null));
        for (byte i = 0; i < 10; i++)
        {
            Assert.Equal(i, config[i]![3]);
        }
    }

    [Fact]
    public void ReadConfig_ReturnsNullForMissingFragments()
    {
        var transport = new FakeTransport();
        transport.Responses.Enqueue(ReadFragment(0));

        var protocol = new WirelessProtocol(transport);
        byte[]?[] config = protocol.ReadConfig(maxReads: 5);

        Assert.NotNull(config[0]);
        Assert.All(config.Skip(1), f => Assert.Null(f));
    }

    [Fact]
    public void WriteConfig_SendsAllFragments()
    {
        var transport = new FakeTransport();
        var protocol = new WirelessProtocol(transport);

        var fragments = Enumerable.Range(0, 10)
            .Select(i => WirelessFrame.Build(WirelessFrame.CmdWrite, WirelessFrame.SubCmdConfig, (byte)i, new byte[WirelessFrame.PayloadLength]))
            .ToArray();

        protocol.WriteConfig(fragments);

        Assert.Equal(10, transport.Sent.Count);
        Assert.All(transport.Sent, f => Assert.Equal(WirelessFrame.CmdWrite, f[1]));
    }
}

public class WirelessConfigTests
{
    private static WirelessConfig BuildConfig(int effectId)
    {
        var fragments = new byte[WirelessConfig.FragmentCount][];
        for (byte i = 0; i < WirelessConfig.FragmentCount; i++)
        {
            var payload = new byte[WirelessFrame.PayloadLength];
            payload[0] = 0x0E;
            if (i == 0)
            {
                payload[11] = (byte)effectId;
            }

            fragments[i] = WirelessFrame.Build(WirelessFrame.CmdRead, WirelessFrame.SubCmdConfig, i, payload);
        }

        return new WirelessConfig(fragments, ModelConfig.F75);
    }

    [Fact]
    public void EffectId_ReadsFragmentZeroByte15()
    {
        Assert.Equal(3, BuildConfig(3).EffectId);
        Assert.Equal(0, BuildConfig(0).EffectId);
    }

    [Fact]
    public void CustomMode_TrueForPerKeyEffect()
    {
        Assert.True(BuildConfig(21).CustomMode);
        Assert.False(BuildConfig(3).CustomMode);
    }

    [Fact]
    public void EffectTableLocation_MatchesF87Layout()
    {
        Assert.Equal((4, 7), WirelessConfig.EffectTableLocation(1));
        Assert.Equal((4, 9), WirelessConfig.EffectTableLocation(2));
        Assert.Equal((4, 17), WirelessConfig.EffectTableLocation(6));
        Assert.Equal((5, 5), WirelessConfig.EffectTableLocation(7));
        Assert.Equal((6, 13), WirelessConfig.EffectTableLocation(18));
    }

    [Fact]
    public void GetParams_ReadsBrightnessAndSpeedFromTableFragment()
    {
        var fragments = new byte[WirelessConfig.FragmentCount][];
        for (byte i = 0; i < WirelessConfig.FragmentCount; i++)
        {
            var payload = new byte[WirelessFrame.PayloadLength];
            payload[0] = 0x0E;
            fragments[i] = WirelessFrame.Build(WirelessFrame.CmdRead, WirelessFrame.SubCmdConfig, i, payload);
        }

        // Effect 2 (Respire): fragment 4, offset 9 = brightness, offset 10 = speed flags.
        fragments[4][9] = 0x05;
        fragments[4][10] = (byte)((2 << 4) | 0x07);
        var config = new WirelessConfig(fragments, ModelConfig.F75);

        EffectParams? p = config.GetParams(2);

        Assert.NotNull(p);
        Assert.Equal(5, p.Value.Brightness);
        Assert.Equal(2, p.Value.Speed);
        Assert.True(p.Value.Colorful);
    }
}

public class WirelessLightingServiceTests
{
    private static WirelessLightingService CreateService(FakeTransport transport) =>
        new(new WirelessProtocol(transport), ModelConfig.F75);

    private static void EnqueueReadResponses(FakeTransport transport)
    {
        for (byte i = 0; i < 10; i++)
        {
            var payload = new byte[WirelessFrame.PayloadLength];
            payload[0] = 0x0E;
            transport.Responses.Enqueue(WirelessFrame.Build(WirelessFrame.CmdRead, WirelessFrame.SubCmdConfig, i, payload));
        }
    }

    [Fact]
    public void Apply_SendsReadWritePaletteAndSave()
    {
        var transport = new FakeTransport();
        EnqueueReadResponses(transport);
        var service = CreateService(transport);

        service.Apply(new LightingConfig(EffectId: 2, Brightness: 3, Speed: 2, Color: new RgbColor(0xFF, 0x00, 0x00)));

        Assert.Equal(1 + 10 + 37 + 1, transport.Sent.Count);
        Assert.Equal(WirelessFrame.CmdRead, transport.Sent[0][1]);
        Assert.All(transport.Sent.Skip(1).Take(10), f => Assert.Equal(WirelessFrame.CmdWrite, f[1]));
        Assert.All(transport.Sent.Skip(11).Take(37), f => Assert.Equal(WirelessFrame.CmdColor, f[1]));
        Assert.Equal(WirelessFrame.CmdSave, transport.Sent[^1][1]);
    }

    [Fact]
    public void Apply_SetsEffectIdAndColorModeOnFragmentZero()
    {
        var transport = new FakeTransport();
        EnqueueReadResponses(transport);
        var service = CreateService(transport);

        service.Apply(new LightingConfig(EffectId: 2, Color: new RgbColor(0x00, 0xFF, 0x00)));

        byte[] fragment0 = transport.Sent[1];
        Assert.Equal(WirelessFrame.CmdWrite, fragment0[1]);
        Assert.Equal(0x01, fragment0[8]);
        Assert.Equal(2, fragment0[15]);
        Assert.Equal(0x01, fragment0[17]);
        Assert.True(WirelessProtocol.HasValidChecksum(fragment0));
    }

    [Fact]
    public void Apply_SetsCustomColorInPaletteFragmentOne()
    {
        var transport = new FakeTransport();
        EnqueueReadResponses(transport);
        var service = CreateService(transport);

        service.Apply(new LightingConfig(EffectId: 1, Color: new RgbColor(0x11, 0x22, 0x33)));

        byte[] palette1 = transport.Sent[12];
        Assert.Equal(WirelessFrame.CmdColor, palette1[1]);
        Assert.Equal(0x11, palette1[12]);
        Assert.Equal(0x22, palette1[13]);
        Assert.Equal(0x33, palette1[14]);
        Assert.Equal(0xFF, palette1[16]);
    }

    [Fact]
    public void Apply_WithNoColor_UsesSingleColorMode()
    {
        var transport = new FakeTransport();
        EnqueueReadResponses(transport);
        var service = CreateService(transport);

        service.Apply(new LightingConfig(EffectId: 3));

        byte[] fragment0 = transport.Sent[1];
        Assert.Equal(0x03, fragment0[17]);
    }

    [Fact]
    public void Apply_PerKey_WritesConfigPerKeyMapAndSave()
    {
        var transport = new FakeTransport();
        EnqueueReadResponses(transport);
        var service = CreateService(transport);
        var colors = Enumerable.Repeat(new RgbColor(0xAA, 0xBB, 0xCC), 126).ToList();

        service.Apply(new LightingConfig(EffectId: 21, PerKeyColors: colors));

        Assert.Equal(1 + 10 + 28 + 1, transport.Sent.Count);
        Assert.Equal(21, transport.Sent[1][15]);
        Assert.All(transport.Sent.Skip(11).Take(28), f => Assert.Equal(WirelessFrame.CmdPerKey, f[1]));
        Assert.Equal(WirelessFrame.CmdSave, transport.Sent[^1][1]);

        byte[] firstPlane = transport.Sent[11];
        Assert.Equal(WirelessFrame.SubCmdPerKey, firstPlane[2]);
        Assert.Equal(0xAA, firstPlane[5]);
        Assert.Equal(0x0E, firstPlane[4]);
    }

    [Fact]
    public void Reset_WritesFactoryTemplateAndSave()
    {
        var transport = new FakeTransport();
        var service = CreateService(transport);

        service.Reset();

        Assert.Equal(10 + 37 + 1, transport.Sent.Count);
        Assert.All(transport.Sent.Take(10), f => Assert.Equal(WirelessFrame.CmdWrite, f[1]));
        Assert.All(transport.Sent.Skip(10).Take(37), f => Assert.Equal(WirelessFrame.CmdColor, f[1]));
        Assert.Equal(WirelessFrame.CmdSave, transport.Sent[^1][1]);
    }
}
