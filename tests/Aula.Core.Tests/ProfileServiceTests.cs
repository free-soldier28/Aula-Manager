using Aula.Core.Abstractions;
using Aula.Core.Models;
using Aula.Core.Services;
using Aula.Core.Tests.TestHelpers;

namespace Aula.Core.Tests;

public class ProfileServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "aula-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Save_Then_Load_RoundTripsProfile()
    {
        var service = new ProfileService(_dir);
        var profile = new KeyboardProfile(
            "gaming",
            new LightingConfig(EffectId: 3, Brightness: 5, Speed: 2, Colorful: true),
            KeyColors: new Dictionary<string, RgbColor>
            {
                ["w"] = new(0xFF, 0x00, 0x00),
                ["space"] = new(0x00, 0xFF, 0x00),
            },
            Model: "f75");

        Assert.Equal(_dir, service.DirectoryPath);
        service.Save("gaming", profile);
        KeyboardProfile? loaded = service.Load("gaming");

        Assert.NotNull(loaded);
        Assert.Equal("gaming", loaded.Name);
        Assert.Equal(3, loaded.Lighting.EffectId);
        Assert.Equal(5, loaded.Lighting.Brightness);
        Assert.Equal(2, loaded.Lighting.Speed);
        Assert.True(loaded.Lighting.Colorful);
        Assert.Equal(2, loaded.KeyColors!.Count);
        Assert.Equal(new RgbColor(0xFF, 0x00, 0x00), loaded.KeyColors["w"]);
        Assert.Equal("f75", loaded.Model);
    }

    [Fact]
    public void Load_Missing_ReturnsNull()
    {
        var service = new ProfileService(_dir);

        Assert.Null(service.Load("missing"));
    }

    [Fact]
    public void List_ReturnsSavedProfiles_Sorted()
    {
        var service = new ProfileService(_dir);
        service.Save("zeta", new KeyboardProfile("zeta", new LightingConfig(0), null, null));
        service.Save("alpha", new KeyboardProfile("alpha", new LightingConfig(0), null, null));

        IReadOnlyList<string> names = service.List();

        Assert.Equal(new[] { "alpha", "zeta" }, names);
    }

    [Fact]
    public void List_Empty_WhenNoDirectory()
    {
        var service = new ProfileService(_dir);

        Assert.Empty(service.List());
    }

    [Fact]
    public void Delete_RemovesProfile()
    {
        var service = new ProfileService(_dir);
        service.Save("temp", new KeyboardProfile("temp", new LightingConfig(0), null, null));

        Assert.True(service.Delete("temp"));
        Assert.False(service.Delete("temp"));
        Assert.Empty(service.List());
    }

    [Fact]
    public void Save_SanitizesInvalidCharacters()
    {
        var service = new ProfileService(_dir);

        service.Save("bad/name:x", new KeyboardProfile("bad/name:x", new LightingConfig(0), null, null));

        Assert.Single(service.List());
    }

    [Fact]
    public void ApplyProfile_AppliesSavedLighting_ToKeyboard()
    {
        var transport = new FakeTransport();
        transport.Responses.Enqueue(F75ReportTests.BuildConfigResponse());
        var keyboard = CreateKeyboard(transport);
        var service = new ProfileService(_dir);
        var profile = new KeyboardProfile(
            "wave",
            new LightingConfig(EffectId: 3, Brightness: 5, Speed: 2, Colorful: false),
            Model: "f75");

        service.ApplyProfile(profile, keyboard);

        Assert.Contains(transport.Sent, s => s[1] == 0x04);
        Assert.Equal(3, transport.Sent.Last()[18]);
    }

    [Fact]
    public void ApplyProfile_WithKeyColors_SendsPerKeyFrame()
    {
        var transport = new FakeTransport();
        transport.Responses.Enqueue(F75ReportTests.BuildConfigResponse());
        var keyboard = CreateKeyboard(transport);
        var service = new ProfileService(_dir);
        var profile = new KeyboardProfile(
            "custom",
            new LightingConfig(EffectId: 21),
            KeyColors: new Dictionary<string, RgbColor> { ["w"] = new(0xFF, 0x00, 0x00) },
            Model: "f75");

        service.ApplyProfile(profile, keyboard);

        Assert.Contains(transport.Sent, s => s[1] == 0x06);
    }

    [Fact]
    public void ApplyProfile_UnknownKey_Throws()
    {
        var transport = new FakeTransport();
        transport.Responses.Enqueue(F75ReportTests.BuildConfigResponse());
        var keyboard = CreateKeyboard(transport);
        var service = new ProfileService(_dir);
        var profile = new KeyboardProfile(
            "bad",
            new LightingConfig(EffectId: 21),
            KeyColors: new Dictionary<string, RgbColor> { ["zzz"] = new(1, 2, 3) },
            Model: "f75");

        Assert.Throws<AulaException>(() => service.ApplyProfile(profile, keyboard));
    }

    private static IAulaKeyboard CreateKeyboard(FakeTransport transport)
    {
        var factory = new FakeTransportFactory(_ => transport);
        var driver = new Aula.Core.Drivers.SinoWealthFeatureDriver(ModelConfig.F75, factory);
        var device = new Aula.Core.Devices.DeviceInfo(
            "path://f75", 0x258A, 0x010C, "SN", "AULA F75", 520);
        return driver.Open(device);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }
}
