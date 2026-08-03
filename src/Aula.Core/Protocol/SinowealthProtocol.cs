using Aula.Core.Devices;
using Aula.Core.Logging;
using Aula.Core.Models;
using Microsoft.Extensions.Logging;

namespace Aula.Core.Protocol;

public sealed class SinowealthProtocol
{
    private const int ModelQueryResponseLength = 14;

    private readonly IHidTransport _transport;
    private readonly ILogger<SinowealthProtocol> _log;

    public SinowealthProtocol(IHidTransport transport, ModelConfig model)
    {
        _transport = transport;
        _log = AulaLogging.Logger<SinowealthProtocol>();
        Model = model;
    }

    public ModelConfig Model { get; }

    public DeviceInfo DeviceInfo => _transport.Info;

    public byte[] QueryModel()
    {
        _transport.SetFeature(F75Report.CreateModelQuery(Model));

        var response = ReadResponse(F75Report.CmdModelQuery);
        return response[..ModelQueryResponseLength];
    }

    public byte[] ReadConfigRaw()
    {
        _transport.SetFeature(F75Report.CreateConfigRead(Model));

        var response = ReadResponse(F75Report.CmdReadConfig);
        return response[..Model.ConfigResponseLength];
    }

    public void WriteConfigRaw(byte[] configResponse)
    {
        _log.LogDebug("Writing config ({Length} bytes)", configResponse.Length);
        _transport.SetFeature(F75Report.CreateConfigWrite(Model, configResponse));
    }

    public void WriteColorProfile(RgbColor color)
    {
        _log.LogDebug("Writing color profile {Color}", color.ToHex());
        _transport.SetFeature(F75Report.CreateColorProfile(Model, color));
    }

    public void WritePerKeyColors(IReadOnlyList<RgbColor> colors)
    {
        _log.LogDebug("Writing per-key colors ({Count} LEDs)", colors.Count);
        _transport.SetFeature(F75Report.CreatePerKeyColors(Model, colors));
    }

    public byte[] ReadColorProfileRaw()
    {
        _transport.SetFeature(F75Report.CreateColorProfileRead(Model));

        var response = ReadResponse(F75Report.CmdReadColorProfile);
        return response;
    }

    private byte[] ReadResponse(byte expectedCommand)
    {
        var response = new byte[Model.ReportLength];
        response[0] = Model.ReportId;
        _transport.GetFeature(response);

        if (response[0] != Model.ReportId || response[1] != expectedCommand)
        {
            _log.LogWarning(
                "Unexpected response header {Header0:X2} {Header1:X2}, expected {ReportId:X2} {Command:X2}",
                response[0], response[1], Model.ReportId, expectedCommand);
            throw new AulaProtocolException(
                $"Unexpected response header: {response[0]:X2} {response[1]:X2}, expected {Model.ReportId:X2} {expectedCommand:X2}.");
        }

        return response;
    }
}
