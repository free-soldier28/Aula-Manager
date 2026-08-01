using Aula.Core.Models;
using Aula.Core.Protocol;
using Aula.Core.Services;
using Aula.Core.Tests.TestHelpers;

namespace Aula.Core.Tests;

public class LightingServiceTests
{
    private static (LightingService Service, FakeTransport Transport) Create()
    {
        var transport = new FakeTransport();
        transport.Responses.Enqueue(F75ReportTests.BuildConfigResponse());
        var service = new LightingService(new SinowealthProtocol(transport, ModelConfig.F75));
        return (service, transport);
    }

    [Fact]
    public void Apply_SetsEffectIdAndParams()
    {
        var (service, transport) = Create();

        service.Apply(new LightingConfig(EffectId: 3, Brightness: 2, Speed: 1, Colorful: false));

        byte[] sent = transport.LastSent();
        Assert.Equal(0x04, sent[1]);
        Assert.Equal(0x03, sent[18]);
        Assert.Equal(0x00, sent[17]);
        Assert.Equal(0x02, sent[64 + 2 * 3]);
        Assert.Equal(0x10, sent[64 + 2 * 3 + 1]);
    }

    [Fact]
    public void Apply_WithColorful_SetsFlag()
    {
        var (service, transport) = Create();

        service.Apply(new LightingConfig(EffectId: 5, Brightness: 4, Speed: 4, Colorful: true));

        byte[] sent = transport.LastSent();
        Assert.Equal(0x47, sent[64 + 2 * 5 + 1]);
    }

    [Fact]
    public void Apply_StaticColor_WritesColorProfileAfterConfig()
    {
        var (service, transport) = Create();

        service.Apply(new LightingConfig(EffectId: 1, Brightness: 4, Color: new RgbColor(0xFF, 0x00, 0x00)));

        Assert.Equal(3, transport.Sent.Count);
        Assert.Equal(0x84, transport.Sent[0][1]);
        Assert.Equal(0x04, transport.Sent[1][1]);
        Assert.Equal(0x0A, transport.Sent[2][1]);
        Assert.Equal(new byte[] { 0xFF, 0x00, 0x00 }, transport.Sent[2][29..32]);
    }

    [Fact]
    public void Apply_ClampsBrightnessAndSpeed()
    {
        var (service, transport) = Create();

        service.Apply(new LightingConfig(EffectId: 3, Brightness: 99, Speed: -3));

        byte[] sent = transport.LastSent();
        Assert.Equal(0x04, sent[64 + 2 * 3]);
        Assert.Equal(0x00, sent[64 + 2 * 3 + 1] >> 4);
    }

    [Fact]
    public void Apply_StaticEffect_IgnoresSpeed()
    {
        var (service, transport) = Create();

        service.Apply(new LightingConfig(EffectId: 1, Brightness: 4, Speed: 2, Color: null));

        byte[] sent = transport.LastSent();
        Assert.Equal(0x04, sent[1]);
        Assert.Equal(0x07, sent[64 + 2 * 1 + 1]);
        Assert.Equal(2, transport.Sent.Count);
    }

    [Fact]
    public void Apply_UnknownEffect_Throws()
    {
        var (service, transport) = Create();

        Assert.Throws<AulaProtocolException>(() => service.Apply(new LightingConfig(EffectId: 99)));
    }

    [Fact]
    public void TurnOff_SetsEffectZero()
    {
        var (service, transport) = Create();

        service.TurnOff();

        byte[] sent = transport.LastSent();
        Assert.Equal(0x04, sent[1]);
        Assert.Equal(0x00, sent[18]);
    }

    [Fact]
    public void ReadConfig_ReturnsParsedConfig()
    {
        var (service, transport) = Create();

        KeyboardConfig config = service.ReadConfig();

        Assert.Equal(10, config.EffectId);
        Assert.Equal(136, config.Raw.Length);
    }

    [Fact]
    public void FindEffect_ByName_IsCaseInsensitive()
    {
        var (service, _) = Create();

        Assert.Equal(3, service.FindEffect("WAVE")?.Id);
        Assert.Null(service.FindEffect("nonexistent"));
    }
}
