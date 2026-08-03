using Aula.Core.Models;
using Aula.Core.Protocol;

namespace Aula.Core.Tests;

public class WirelessConfigTests
{
    private static byte[][] BuildFragments(int effectId = 3, byte applyFlag = 0, byte confirmFlag = 0, byte colorMode = 0)
    {
        var fragments = new byte[10][];
        for (int i = 0; i < fragments.Length; i++)
        {
            var payload = new byte[WirelessFrame.PayloadLength];
            payload[0] = 0x0E;
            payload[1] = (byte)i;
            fragments[i] = WirelessFrame.Build(WirelessFrame.CmdRead, WirelessFrame.SubCmdConfig, (byte)i, payload);
        }

        fragments[0][15] = (byte)effectId;
        fragments[0][14] = applyFlag;
        fragments[0][8] = confirmFlag;
        fragments[0][17] = colorMode;
        return fragments;
    }

    private static WirelessConfig Create(params byte[]?[] fragments) =>
        new(fragments, ModelConfig.F75);

    [Fact]
    public void Constructor_JoinsFragmentsIntoRawBytes()
    {
        var config = Create(BuildFragments(7, colorMode: 0x01));

        Assert.Equal(WirelessConfig.FragmentCount * WirelessFrame.FragmentLength, config.Raw.Length);
        Assert.Equal(7, config.EffectId);
    }

    [Fact]
    public void IsComplete_RequiresAllFragments()
    {
        byte[][] fragments = BuildFragments();
        Assert.True(Create(fragments).IsComplete);

        byte[]?[] partial = fragments.Cast<byte[]?>().ToArray();
        partial[3] = null;
        Assert.False(Create(partial).IsComplete);
    }

    [Fact]
    public void CustomMode_WhenPerKeyEffect()
    {
        Assert.True(Create(BuildFragments(21)).CustomMode);
        Assert.False(Create(BuildFragments(3)).CustomMode);
    }

    [Fact]
    public void FlagAccessors_ReadFragmentZero()
    {
        var config = Create(BuildFragments(3, applyFlag: 0x04, confirmFlag: 0x01, colorMode: 0x03));

        Assert.Equal(0x04, config.ApplyFlag);
        Assert.Equal(0x01, config.ConfirmFlag);
        Assert.Equal(0x03, config.ColorMode);
    }

    [Theory]
    [InlineData(1, 4, 7)]
    [InlineData(6, 4, 17)]
    [InlineData(7, 5, 5)]
    [InlineData(13, 5, 17)]
    [InlineData(14, 6, 5)]
    [InlineData(18, 6, 13)]
    public void EffectTableLocation_MapsRanges(int effectId, int expectedFragment, int expectedOffset)
    {
        (int fragment, int offset) = WirelessConfig.EffectTableLocation(effectId);

        Assert.Equal(expectedFragment, fragment);
        Assert.Equal(expectedOffset, offset);
    }

    [Fact]
    public void EffectTableLocation_Unknown_DefaultsToFourSeven()
    {
        Assert.Equal((4, 7), WirelessConfig.EffectTableLocation(0));
        Assert.Equal((4, 7), WirelessConfig.EffectTableLocation(99));
    }

    [Fact]
    public void GetParams_ReadsBrightnessAndSpeedFlags()
    {
        byte[][] fragments = BuildFragments(3);
        fragments[4][11] = 0x05;
        fragments[4][12] = 0x2A;

        var config = Create(fragments);
        EffectParams? parameters = config.GetParams(3);

        Assert.NotNull(parameters);
        Assert.Equal(0x05, parameters.Value.Brightness);
        Assert.Equal(0x2A, parameters.Value.SpeedFlags);
        Assert.Equal(2, parameters.Value.Speed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(19)]
    public void GetParams_Null_WhenOutOfRange(int effectId)
    {
        Assert.Null(Create(BuildFragments()).GetParams(effectId));
    }

    [Fact]
    public void GetParams_Null_WhenFragmentMissing()
    {
        byte[]?[] fragments = BuildFragments().Cast<byte[]?>().ToArray();
        fragments[4] = null;

        Assert.Null(Create(fragments).GetParams(1));
    }

    [Fact]
    public void GetByte_ReturnsZero_WhenFragmentNull()
    {
        var config = Create(new byte[]?[10]);

        Assert.Equal(0, config.EffectId);
        Assert.Equal(0, config.ApplyFlag);
        Assert.Equal(0, config.ConfirmFlag);
        Assert.Equal(0, config.ColorMode);
    }
}
