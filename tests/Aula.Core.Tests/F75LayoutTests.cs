using Aula.Core.Abstractions;

namespace Aula.Core.Tests;

public class F75LayoutTests
{
    [Theory]
    [InlineData("esc", 0)]
    [InlineData("f1", 12)]
    [InlineData("f4", 30)]
    [InlineData("w", 14)]
    [InlineData("space", 35)]
    [InlineData("enter", 81)]
    [InlineData("W", 14)]
    [InlineData(" left ", 89)]
    public void GetLedIndex_ResolvesKeyNames(string key, int expected)
    {
        Assert.Equal(expected, F75Layout.Instance.GetLedIndex(key));
    }

    [Fact]
    public void GetLedIndex_UnknownKey_ReturnsMinusOne()
    {
        Assert.Equal(-1, F75Layout.Instance.GetLedIndex("nonexistent"));
    }

    [Fact]
    public void TryGetLedIndex_HandlesUnknownKey()
    {
        Assert.False(F75Layout.Instance.TryGetLedIndex("zzz", out int index));
        Assert.Equal(-1, index);

        Assert.True(F75Layout.Instance.TryGetLedIndex("a", out int a));
        Assert.Equal(9, a);
    }

    [Fact]
    public void GetKeyName_RoundTripsLedIndex()
    {
        Assert.Equal("w", F75Layout.GetKeyName(14));
        Assert.Equal("led999", F75Layout.GetKeyName(999));
    }

    [Fact]
    public void Keys_ContainsCoreKeys()
    {
        var keys = F75Layout.Instance.Keys;
        Assert.Contains("w", keys);
        Assert.Contains("space", keys);
        Assert.Contains("enter", keys);
        Assert.Contains("f12", keys);
    }
}
