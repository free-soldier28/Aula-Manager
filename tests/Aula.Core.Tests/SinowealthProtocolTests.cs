using Aula.Core.Models;
using Aula.Core.Protocol;
using Aula.Core.Tests.TestHelpers;

namespace Aula.Core.Tests;

public class SinowealthProtocolTests
{
    [Fact]
    public void QueryModel_ReturnsGoldenCapture()
    {
        var transport = new FakeTransport();
        transport.Responses.Enqueue(new byte[] { 0x06, 0x82, 0x01, 0x00, 0x01, 0x00, 0x06, 0x00, 0x03, 0x00, 0x00, 0x00, 0x03, 0x66 });
        var protocol = new SinowealthProtocol(transport, ModelConfig.F75);

        byte[] result = protocol.QueryModel();

        Assert.Equal(14, result.Length);
        Assert.Equal(0x03, result[8]);
        Assert.Equal(0x66, result[13]);
        Assert.Equal(0x82, transport.LastSent()[1]);
    }

    [Fact]
    public void QueryModel_Throws_OnUnexpectedResponse()
    {
        var transport = new FakeTransport();
        transport.Responses.Enqueue(new byte[] { 0x06, 0x00 });
        var protocol = new SinowealthProtocol(transport, ModelConfig.F75);

        Assert.Throws<AulaProtocolException>(() => protocol.QueryModel());
    }

    [Fact]
    public void ReadConfigRaw_ReturnsResponse()
    {
        var transport = new FakeTransport();
        transport.Responses.Enqueue(F75ReportTests.BuildConfigResponse());
        var protocol = new SinowealthProtocol(transport, ModelConfig.F75);

        byte[] config = protocol.ReadConfigRaw();

        Assert.Equal(136, config.Length);
        Assert.Equal(0x06, config[0]);
        Assert.Equal(0x84, config[1]);
        Assert.Equal(0x84, transport.LastSent()[1]);
    }

    [Fact]
    public void WriteConfigRaw_SendsWriteFrame()
    {
        var transport = new FakeTransport();
        var protocol = new SinowealthProtocol(transport, ModelConfig.F75);

        protocol.WriteConfigRaw(F75ReportTests.BuildConfigResponse());

        byte[] sent = transport.LastSent();
        Assert.Equal(0x04, sent[1]);
        Assert.Equal(F75ReportTests.BuildConfigResponse()[8..], sent[8..136]);
    }

    [Fact]
    public void WriteColorProfile_SendsProfileFrame()
    {
        var transport = new FakeTransport();
        var protocol = new SinowealthProtocol(transport, ModelConfig.F75);

        protocol.WriteColorProfile(new RgbColor(0x00, 0xFF, 0x80));

        byte[] sent = transport.LastSent();
        Assert.Equal(0x0A, sent[1]);
        Assert.Equal(new byte[] { 0x00, 0xFF, 0x80 }, sent[29..32]);
    }
}
