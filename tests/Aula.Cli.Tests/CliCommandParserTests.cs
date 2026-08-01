using Aula.Core.Models;

namespace Aula.Cli.Tests;

public class CliCommandParserTests
{
    [Fact]
    public void Parse_EmptyArgs_ReturnsHelp()
    {
        Assert.IsType<HelpCommand>(CliCommandParser.Parse(Array.Empty<string>()));
    }

    [Fact]
    public void Parse_Help_ReturnsHelp()
    {
        Assert.IsType<HelpCommand>(CliCommandParser.Parse(new[] { "help" }));
        Assert.IsType<HelpCommand>(CliCommandParser.Parse(new[] { "--help" }));
    }

    [Fact]
    public void Parse_List_ReturnsList()
    {
        Assert.IsType<ListCommand>(CliCommandParser.Parse(new[] { "list" }));
        Assert.IsType<ListCommand>(CliCommandParser.Parse(new[] { "ls" }));
    }

    [Fact]
    public void Parse_Info_ReadsModel()
    {
        var command = Assert.IsType<InfoCommand>(CliCommandParser.Parse(new[] { "info", "--model", "f87" }));
        Assert.Equal("f87", command.Model);

        Assert.IsType<InfoCommand>(CliCommandParser.Parse(new[] { "info" }));
    }

    [Fact]
    public void Parse_Effects_ReadsModel()
    {
        var command = Assert.IsType<EffectsCommand>(CliCommandParser.Parse(new[] { "effects" }));
        Assert.Equal("f75", command.Model);
    }

    [Theory]
    [InlineData("wave", 3)]
    [InlineData("3", 3)]
    [InlineData("Marquee", 10)]
    public void Parse_Effect_ResolvesNameOrId(string reference, int expectedId)
    {
        var command = Assert.IsType<EffectCommand>(CliCommandParser.Parse(new[] { "effect", reference }));
        Assert.Equal(expectedId, command.EffectId);
    }

    [Fact]
    public void Parse_Effect_ReadsOptions()
    {
        var command = Assert.IsType<EffectCommand>(CliCommandParser.Parse(
            new[] { "effect", "wave", "-b", "4", "-s", "2", "--color", "#FF0000", "--model", "f87" }));

        Assert.Equal(3, command.EffectId);
        Assert.Equal(4, command.Brightness);
        Assert.Equal(2, command.Speed);
        Assert.Equal(RgbColor.FromHex("#FF0000"), command.Color);
        Assert.Equal("f87", command.Model);
    }

    [Fact]
    public void Parse_Effect_ReadsRgbTriple()
    {
        var command = Assert.IsType<EffectCommand>(CliCommandParser.Parse(
            new[] { "effect", "wave", "--color", "0", "255", "128" }));

        Assert.Equal(new RgbColor(0, 255, 128), command.Color);
    }

    [Fact]
    public void Parse_Effect_Colorful()
    {
        var command = Assert.IsType<EffectCommand>(CliCommandParser.Parse(
            new[] { "effect", "wave", "--colorful" }));

        Assert.True(command.Colorful);
    }

    [Fact]
    public void Parse_Off_ReturnsOff()
    {
        Assert.IsType<OffCommand>(CliCommandParser.Parse(new[] { "off" }));
    }

    [Fact]
    public void Parse_Dump_ReturnsDump()
    {
        Assert.IsType<DumpCommand>(CliCommandParser.Parse(new[] { "dump" }));
    }

    public static TheoryData<string[]> InvalidInputs => new()
    {
        new[] { "effect", "wave", "--speed", "abc" },
        new[] { "effect", "wave", "--color", "#GGGGGG" },
        new[] { "effect", "wave", "--color", "0", "1" },
        new[] { "effect", "nonexistent" },
        new[] { "effect", "99" },
        new[] { "unknown" },
        new[] { "effect", "wave", "--speed" },
    };

    [Theory]
    [MemberData(nameof(InvalidInputs))]
    public void Parse_Throws_OnInvalidInput(string[] args)
    {
        Assert.Throws<CliParseException>(() => CliCommandParser.Parse(args));
    }
}
