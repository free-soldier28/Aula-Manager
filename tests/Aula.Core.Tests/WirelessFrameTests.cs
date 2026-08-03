using Aula.Core.Protocol;

namespace Aula.Core.Tests;

public class WirelessFrameTests
{
    [Fact]
    public void Build_ProducesTwentyByteFrame_WithFieldsAndChecksum()
    {
        byte[] frame = WirelessFrame.Build(0x44, 0x0A, 3, new byte[] { 0x01, 0x02 });

        Assert.Equal(WirelessFrame.FragmentLength, frame.Length);
        Assert.Equal(0x13, frame[0]);
        Assert.Equal(0x44, frame[1]);
        Assert.Equal(0x0A, frame[2]);
        Assert.Equal(3, frame[3]);
        Assert.Equal(0x01, frame[4]);
        Assert.Equal(0x02, frame[5]);
        Assert.Equal(0x00, frame[6]);
        Assert.Equal(WirelessFrame.Checksum(frame), frame[19]);
    }

    [Fact]
    public void Build_TruncatesPayload_WhenLongerThanFifteenBytes()
    {
        byte[] frame = WirelessFrame.Build(0x04, 0x0A, 0, Enumerable.Repeat((byte)0xAA, 30).ToArray());

        for (int i = 4; i < 4 + WirelessFrame.PayloadLength; i++)
        {
            Assert.Equal(0xAA, frame[i]);
        }

        Assert.Equal(WirelessFrame.Checksum(frame), frame[19]);
    }

    [Fact]
    public void Build_PadsShortPayload_WithZeros()
    {
        byte[] frame = WirelessFrame.Build(0x04, 0x0A, 0, ReadOnlySpan<byte>.Empty);

        Assert.All(frame[4..19], b => Assert.Equal(0x00, b));
    }

    [Fact]
    public void Checksum_SumsFirstNineteenBytes()
    {
        byte[] frame = WirelessFrame.Build(0x04, 0x0A, 7, new byte[] { 0xFF, 0x00, 0x80 });

        int expected = 0;
        for (int i = 0; i < 19; i++)
        {
            expected = (expected + frame[i]) & 0xFF;
        }

        Assert.Equal((byte)expected, WirelessFrame.Checksum(frame));
    }
}
