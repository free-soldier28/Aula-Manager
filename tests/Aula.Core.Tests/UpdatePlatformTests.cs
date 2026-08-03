using System.Runtime.InteropServices;
using Aula.Core.Updating;

namespace Aula.Core.Tests;

public class UpdatePlatformTests
{
    [Theory]
    [InlineData(true, false, "windows")]
    [InlineData(false, true, "macos")]
    [InlineData(false, false, "linux")]
    public void OsName_Classifies(bool isWindows, bool isMacOs, string expected)
    {
        Assert.Equal(expected, UpdatePlatform.OsName(isWindows, isMacOs));
    }

    [Theory]
    [InlineData(Architecture.X64, "x64")]
    [InlineData(Architecture.Arm64, "arm64")]
    [InlineData(Architecture.X86, "x86")]
    [InlineData(Architecture.Wasm, "any")]
    public void ArchName_Classifies(Architecture arch, string expected)
    {
        Assert.Equal(expected, UpdatePlatform.ArchName(arch));
    }

    [Theory]
    [InlineData("windows", "x64", "win-x64-x64")]
    [InlineData("macos", "arm64", "osx-macos-arm64-arm64")]
    [InlineData("linux", "x64", "linux-x64-x64")]
    public void RuntimesFor_BuildsRuntimes(string os, string arch, string expected)
    {
        Assert.Equal(expected, UpdatePlatform.RuntimesFor(os, arch, arch));
    }

    [Fact]
    public void Detect_ReturnsPopulatedPlatform()
    {
        UpdatePlatform platform = UpdatePlatform.Detect();

        Assert.False(string.IsNullOrEmpty(platform.Os));
        Assert.False(string.IsNullOrEmpty(platform.Arch));
        Assert.Contains(platform.Arch, platform.Runtimes);
    }

    [Fact]
    public void MatchesAsset_MatchingOsAndArch_ReturnsTrue()
    {
        var windows = new UpdatePlatform("windows", "x64", "win-x64-x64");
        var mac = new UpdatePlatform("macos", "arm64", "osx-macos-arm64-arm64");
        var linux = new UpdatePlatform("linux", "x64", "linux-x64-x64");

        Assert.True(windows.MatchesAsset("aula-windows-x64.zip"));
        Assert.True(mac.MatchesAsset("aula-macos-arm64.zip"));
        Assert.True(linux.MatchesAsset("aula-linux-x64.zip"));
    }

    [Fact]
    public void MatchesAsset_WrongOsOrArch_ReturnsFalse()
    {
        var windows = new UpdatePlatform("windows", "x64", "win-x64-x64");

        Assert.False(windows.MatchesAsset("aula-linux-x64.zip"));
        Assert.False(windows.MatchesAsset("aula-windows-arm64.zip"));
    }
}
