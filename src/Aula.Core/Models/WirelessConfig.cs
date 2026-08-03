using Aula.Core.Protocol;

namespace Aula.Core.Models;

/// <summary>
/// Config as read over the 2.4 GHz protocol: 10 fragments of 20 bytes each.
/// Effect id lives in fragment 0 byte 15, the per-effect brightness/speed
/// table lives in fragments 4-6 (same layout as the AULA F87 family).
/// </summary>
public sealed class WirelessConfig : KeyboardConfig
{
    public const int FragmentCount = 10;

    public WirelessConfig(byte[]?[] fragments, ModelConfig model)
        : base(Join(fragments), model)
    {
        Fragments = (byte[]?[])fragments.Clone();
    }

    public byte[]?[] Fragments { get; }

    public bool IsComplete => Fragments.All(f => f is not null);

    public int ApplyFlag => GetByte(0, 14);

    public int ConfirmFlag => GetByte(0, 8);

    public int ColorMode => GetByte(0, 17);

    public override int EffectId => GetByte(0, 15);

    public override bool CustomMode => EffectId == WirelessPerKeyEffect;

    public override EffectParams? GetParams(int effectId)
    {
        if (effectId < 1 || effectId > 18)
        {
            return null;
        }

        (int fragment, int offset) = EffectTableLocation(effectId);
        byte[]? frag = Fragments[fragment];
        if (frag is null || offset + 1 >= frag.Length)
        {
            return null;
        }

        return new EffectParams(frag[offset], frag[offset + 1]);
    }

    public static (int Fragment, int Offset) EffectTableLocation(int effectId) =>
        effectId switch
        {
            >= 1 and <= 6 => (4, 7 + (effectId - 1) * 2),
            >= 7 and <= 13 => (5, 5 + (effectId - 7) * 2),
            >= 14 and <= 18 => (6, 5 + (effectId - 14) * 2),
            _ => (4, 7),
        };

    private byte GetByte(int fragment, int offset)
    {
        byte[]? frag = Fragments[fragment];
        return frag is null || offset >= frag.Length ? (byte)0 : frag[offset];
    }

    private static byte[] Join(byte[]?[] fragments)
    {
        var joined = new byte[FragmentCount * WirelessFrame.FragmentLength];
        for (int i = 0; i < fragments.Length && i < FragmentCount; i++)
        {
            if (fragments[i] is { } frag)
            {
                Array.Copy(frag, 0, joined, i * WirelessFrame.FragmentLength, frag.Length);
            }
        }

        return joined;
    }

    private const int WirelessPerKeyEffect = 21;
}
