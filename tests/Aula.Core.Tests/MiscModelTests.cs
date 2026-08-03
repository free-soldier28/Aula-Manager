using Aula.Core.Abstractions;
using Aula.Core.Devices;
using Aula.Core.Models;
using Aula.Core.Services;
using Aula.Core.Tests.TestHelpers;

namespace Aula.Core.Tests;

public class MiscModelTests
{
    [Fact]
    public void UnknownKeyboardLayout_IsEmpty()
    {
        UnknownKeyboardLayout layout = UnknownKeyboardLayout.Instance;

        Assert.Equal(0, layout.LedCount);
        Assert.Empty(layout.Keys);
        Assert.Empty(layout.Rows);
        Assert.Equal(-1, layout.GetLedIndex("w"));
        Assert.False(layout.TryGetLedIndex("w", out int index));
        Assert.Equal(-1, index);
    }

    [Fact]
    public void DeviceInfo_DisplayName_FallsBackToVidPid()
    {
        var named = new DeviceInfo("p", 0x258A, 0x010C, null, "AULA F75");
        var unnamed = new DeviceInfo("p", 0x258A, 0x010C, null, "  ");

        Assert.Equal("AULA F75", named.DisplayName);
        Assert.Equal("AULA (258A:010C)", unnamed.DisplayName);
    }

    [Fact]
    public void AulaDeviceNotFoundException_HasDefaultMessage()
    {
        var ex = new AulaDeviceNotFoundException();

        Assert.Contains("No AULA device found", ex.Message);
    }

    [Fact]
    public void Exceptions_CanCarryInnerException()
    {
        var inner = new InvalidOperationException("boom");
        var transport = new AulaTransportException("outer", inner);

        Assert.Same(inner, transport.InnerException);
        Assert.IsAssignableFrom<AulaException>(transport);

        var simple = new AulaTransportException("simple");
        Assert.Equal("simple", simple.Message);
    }

    [Fact]
    public void LightingConfig_Off_IsEffectZero()
    {
        Assert.Equal(0, LightingConfig.Off.EffectId);
    }

    [Fact]
    public void EffectParams_DecodesSpeedFlags()
    {
        var parameters = new EffectParams(0x07, 0x47);

        Assert.Equal(0x07, parameters.Brightness);
        Assert.Equal(0x04, parameters.Speed);
        Assert.True(parameters.Colorful);
        Assert.Equal(0x07, parameters.ColorMode);
    }

    [Fact]
    public void EffectParams_Colorful_WhenModeSeven()
    {
        Assert.True(new EffectParams(0, 0x07).Colorful);
        Assert.False(new EffectParams(0, 0x03).Colorful);
    }

    [Fact]
    public void EffectLibrary_LookupByIdAndName()
    {
        Assert.Equal("wave", EffectLibrary.FindById(3)?.Name);
        Assert.Null(EffectLibrary.FindById(100));
        Assert.Equal(3, EffectLibrary.FindByName("WAVE")?.Id);
        Assert.Null(EffectLibrary.FindByName("bogus"));
    }

    [Fact]
    public void KeyboardCapabilities_RecordHasDefaults()
    {
        var caps = new KeyboardCapabilities();

        Assert.True(caps.HasLighting);
        Assert.False(caps.HasKeyRemap);
        Assert.False(caps.HasWireless);
        Assert.False(caps.HasScreen);
    }

    [Fact]
    public void KeyboardProfile_FromCurrent_ReadsKeyboardState()
    {
        var transport = new FakeTransport();
        transport.Responses.Enqueue(F75ReportTests.BuildConfigResponse());
        var factory = new FakeTransportFactory(_ => transport);
        var driver = new Aula.Core.Drivers.SinoWealthFeatureDriver(ModelConfig.F75, factory);
        var device = new DeviceInfo("path://f75", 0x258A, 0x010C, "SN", "AULA F75", 520);
        using var keyboard = driver.Open(device);

        KeyboardProfile profile = KeyboardProfile.FromCurrent("wave", keyboard);

        Assert.Equal("wave", profile.Name);
        Assert.Equal(10, profile.Lighting.EffectId);
        Assert.Equal(3, profile.Lighting.Brightness);
        Assert.Equal(4, profile.Lighting.Speed);
        Assert.True(profile.Lighting.Colorful);
        Assert.Equal("f75", profile.Model);
    }

    [Fact]
    public void ModelConfig_Resolve_FallsBackToF75()
    {
        Assert.Same(ModelConfig.F75, ModelConfig.Resolve(null));
        Assert.Same(ModelConfig.F75, ModelConfig.Resolve("  "));
        Assert.Same(ModelConfig.F75, ModelConfig.Resolve("bogus"));
        Assert.Same(ModelConfig.F87, ModelConfig.Resolve("f87"));
    }
}
