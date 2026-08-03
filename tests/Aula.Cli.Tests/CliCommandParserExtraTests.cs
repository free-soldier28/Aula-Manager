using Aula.Core.Models;

namespace Aula.Cli.Tests;

public class CliCommandParserExtraTests
{
    [Fact]
    public void Parse_Wireless_DefaultsToRead()
    {
        var command = Assert.IsType<WirelessCommand>(CliCommandParser.Parse(new[] { "wireless" }));
        Assert.Equal("read", command.Action);
    }

    [Theory]
    [InlineData("scan")]
    [InlineData("read")]
    public void Parse_Wireless_SimpleActions(string action)
    {
        var command = Assert.IsType<WirelessCommand>(CliCommandParser.Parse(new[] { "wireless", action }));
        Assert.Equal(action, command.Action);
    }

    [Fact]
    public void Parse_WirelessEffect_ReadsOptions()
    {
        var command = Assert.IsType<WirelessCommand>(CliCommandParser.Parse(
            new[] { "wireless", "effect", "wave", "-b", "4", "-s", "2", "--color", "#00FF00", "--colorful" }));

        Assert.Equal("effect", command.Action);
        Assert.NotNull(command.Effect);
        Assert.Equal(3, command.Effect.EffectId);
        Assert.Equal(4, command.Effect.Brightness);
        Assert.Equal(2, command.Effect.Speed);
        Assert.Equal(RgbColor.FromHex("#00FF00"), command.Effect.Color);
        Assert.True(command.Effect.Colorful);
    }

    [Fact]
    public void Parse_WirelessEffect_ResolvesEffectName()
    {
        var command = Assert.IsType<WirelessCommand>(CliCommandParser.Parse(new[] { "wireless", "effect", "Marquee" }));
        Assert.Equal(10, command.Effect!.EffectId);
    }

    [Fact]
    public void Parse_WirelessEffect_MissingName_Throws()
    {
        Assert.Throws<CliParseException>(() => CliCommandParser.Parse(new[] { "wireless", "effect" }));
    }

    [Fact]
    public void Parse_Wireless_UnknownAction_Throws()
    {
        Assert.Throws<CliParseException>(() => CliCommandParser.Parse(new[] { "wireless", "bogus" }));
    }

    [Fact]
    public void Parse_WirelessEffect_UnknownOption_Throws()
    {
        Assert.Throws<CliParseException>(() => CliCommandParser.Parse(new[] { "wireless", "effect", "wave", "--nope" }));
    }

    [Fact]
    public void Parse_WirelessEffect_InvalidNumber_Throws()
    {
        Assert.Throws<CliParseException>(() => CliCommandParser.Parse(new[] { "wireless", "effect", "wave", "-b", "abc" }));
    }

    [Fact]
    public void Parse_Reset_DefaultsAndOptions()
    {
        var command = Assert.IsType<ResetCommand>(CliCommandParser.Parse(new[] { "reset" }));
        Assert.Equal("f75", command.Model);
        Assert.Null(command.VendorPath);

        var custom = Assert.IsType<ResetCommand>(CliCommandParser.Parse(
            new[] { "reset", "--model", "f87", "--vendor", "C:\\tools\\reset.exe" }));
        Assert.Equal("f87", custom.Model);
        Assert.Equal("C:\\tools\\reset.exe", custom.VendorPath);
    }

    [Fact]
    public void Parse_Reset_UnknownOption_Throws()
    {
        Assert.Throws<CliParseException>(() => CliCommandParser.Parse(new[] { "reset", "--bogus" }));
    }

    [Fact]
    public void Parse_Effect_ReadsRawFlagsAndModel()
    {
        var command = Assert.IsType<EffectCommand>(CliCommandParser.Parse(
            new[] { "effect", "wave", "--raw-flags", "0x20", "-m", "f87" }));

        Assert.Equal((byte)0x20, command.RawFlags);
        Assert.Equal("f87", command.Model);
    }

    [Fact]
    public void Parse_Effect_InvalidRawFlags_Throws()
    {
        Assert.Throws<CliParseException>(() => CliCommandParser.Parse(new[] { "effect", "wave", "--raw-flags", "GG" }));
    }

    [Fact]
    public void Parse_Effect_MissingModelValue_Throws()
    {
        Assert.Throws<CliParseException>(() => CliCommandParser.Parse(new[] { "effect", "wave", "--model" }));
    }

    [Fact]
    public void Parse_Profile_LoadAndDelete()
    {
        var load = Assert.IsType<ProfileCommand>(CliCommandParser.Parse(new[] { "profile", "load", "gaming" }));
        Assert.Equal("load", load.Action);
        Assert.Equal("gaming", load.Name);

        var delete = Assert.IsType<ProfileCommand>(CliCommandParser.Parse(new[] { "profile", "delete", "gaming" }));
        Assert.Equal("delete", delete.Action);
    }

    [Fact]
    public void Parse_Profile_Save_WithColorAndKeyColors()
    {
        var command = Assert.IsType<ProfileCommand>(CliCommandParser.Parse(
            new[] { "profile", "save", "gaming", "w=ff0000", "--color", "#00FF00", "--colorful" }));

        Assert.Equal("save", command.Action);
        Assert.Equal("gaming", command.Name);
        Assert.Equal(RgbColor.FromHex("#00FF00"), command.Color);
        Assert.True(command.Colorful);
        Assert.Equal(RgbColor.FromHex("#ff0000"), command.KeyColors!["w"]);
    }

    [Fact]
    public void Parse_Profile_List_WithModel()
    {
        var command = Assert.IsType<ProfileCommand>(CliCommandParser.Parse(new[] { "profile", "list", "-m", "f87" }));
        Assert.Equal("f87", command.Model);
    }

    [Fact]
    public void Parse_Profile_UnknownOption_Throws()
    {
        Assert.Throws<CliParseException>(() => CliCommandParser.Parse(new[] { "profile", "save", "x", "--bogus" }));
    }

    [Theory]
    [InlineData("save")]
    [InlineData("load")]
    [InlineData("apply")]
    [InlineData("delete")]
    public void Parse_Profile_RequiresName_ForActions(string action)
    {
        Assert.Throws<CliParseException>(() => CliCommandParser.Parse(new[] { "profile", action }));
    }

    [Fact]
    public void Parse_PerKey_ReadsLedIndex()
    {
        var command = Assert.IsType<PerKeyCommand>(CliCommandParser.Parse(
            new[] { "perkey", "--led", "5", "--color", "#FFFFFF" }));

        Assert.Equal(5, command.LedIndex);
        Assert.False(command.FillAll);
    }

    [Fact]
    public void Parse_PerKey_ReadsRgbTriple()
    {
        var command = Assert.IsType<PerKeyCommand>(CliCommandParser.Parse(
            new[] { "perkey", "--color", "0", "255", "128" }));

        Assert.Equal(new RgbColor(0, 255, 128), command.Color);
    }

    [Fact]
    public void Parse_PerKey_InvalidKeyColor_Throws()
    {
        Assert.Throws<CliParseException>(() => CliCommandParser.Parse(new[] { "perkey", "w=zzzzzz" }));
    }

    [Fact]
    public void Parse_PerKey_UnknownOption_Throws()
    {
        Assert.Throws<CliParseException>(() => CliCommandParser.Parse(new[] { "perkey", "--bogus" }));
    }

    [Fact]
    public void Parse_Help_Aliases()
    {
        Assert.IsType<HelpCommand>(CliCommandParser.Parse(new[] { "-h" }));
    }

    [Fact]
    public void Parse_Effect_UnknownOption_Throws()
    {
        Assert.Throws<CliParseException>(() => CliCommandParser.Parse(new[] { "effect", "wave", "--bogus" }));
    }

    [Fact]
    public void Parse_Profile_NoArgs_Throws()
    {
        Assert.Throws<CliParseException>(() => CliCommandParser.Parse(new[] { "profile" }));
    }

    [Fact]
    public void Parse_PerKey_NoArgs_Throws()
    {
        Assert.Throws<CliParseException>(() => CliCommandParser.Parse(new[] { "perkey" }));
    }

    [Fact]
    public void Parse_Effect_InvalidColorWord_Throws()
    {
        Assert.Throws<CliParseException>(() => CliCommandParser.Parse(new[] { "effect", "wave", "--color", "red" }));
    }

    [Fact]
    public void Parse_Effect_NoArgs_Throws()
    {
        Assert.Throws<CliParseException>(() => CliCommandParser.Parse(new[] { "effect" }));
    }

    [Fact]
    public void Parse_Effect_ColorWithoutHash_AcceptsRRGGBB()
    {
        var command = Assert.IsType<EffectCommand>(CliCommandParser.Parse(
            new[] { "effect", "wave", "--color", "FF0000" }));

        Assert.Equal(RgbColor.FromHex("#FF0000"), command.Color);
    }

    [Fact]
    public void RecordCommands_SupportValueEquality()
    {
        var help = new HelpCommand();
        var helpCopy = help with { };
        Assert.Equal(help, helpCopy);
        Assert.Equal(help.GetHashCode(), helpCopy.GetHashCode());
        Assert.NotEmpty(help.ToString());

        var list = new ListCommand();
        var listCopy = list with { };
        Assert.Equal(list, listCopy);
        Assert.NotEqual<CliCommand>(list, help);
        Assert.NotEmpty(list.ToString());
    }
}
