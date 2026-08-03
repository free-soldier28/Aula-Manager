using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using Aula.Core;
using Aula.Core.Devices;
using Aula.Core.Drivers;
using Aula.Core.Models;
using Aula.Core.Protocol;
using Aula.Core.Services;
using Aula.Core.Updating;

namespace Aula.Cli.Tests;

public sealed class CliRunnerTests : IDisposable
{
    private readonly string _root;
    private readonly FakeScanner _scanner = new();
    private readonly FakeTransport _transport = new();
    private readonly StringWriter _out = new();
    private readonly StringWriter _err = new();
    private readonly FakeHttpHandler _http = new();
    private readonly string _profileDir;
    private readonly string _staging;
    private readonly string _currentExe;
    private ProcessStartInfo? _launched;
    private CliRunner? _runner;

    public CliRunnerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "aula-tests", "cli-" + Guid.NewGuid().ToString("N"));
        _profileDir = Path.Combine(_root, "profiles");
        _staging = Path.Combine(_root, "staging");
        _currentExe = Path.Combine(_root, "app", "AulaManager.exe");
    }

    private CliRunner Runner
    {
        get
        {
            if (_runner is null)
            {
                _runner = CreateRunner();
            }

            return _runner;
        }
    }

    private CliRunner CreateRunner()
    {
        var registry = new DriverRegistry();
        registry.Register(new SinoWealthFeatureDriver(ModelConfig.F75, new FakeTransportFactory(_ => _transport)));
        registry.Register(new WirelessSinoWealthDriver(ModelConfig.F75, new FakeTransportFactory(_ => _transport)));
        var factory = new KeyboardDeviceFactory(_scanner, registry);

        return new CliRunner(
            _scanner,
            factory,
            new ProfileService(_profileDir),
            new UpdateService(http: new HttpClient(_http), currentVersion: "0.9.0"),
            new UpdateInstaller(
                currentExecutable: _currentExe,
                stagingDirectory: _staging,
                startProcess: si => _launched = si),
            new FakeTransportFactory(_ => _transport),
            _out,
            _err);
    }

    private void AddF75Device() => _scanner.Devices.Add(CliFakes.F75Device());

    private void AddWirelessDevice() => _scanner.Devices.Add(CliFakes.DongleDevice());

    private void EnqueueConfigResponse() => _transport.Responses.Enqueue(CliFakes.ConfigResponse());

    private static StringContent Json(object payload) =>
        new(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

    private static object Release(string tag = "v1.0.0", bool prerelease = false)
    {
        return new
        {
            tag_name = tag,
            name = tag,
            body = "release notes",
            prerelease,
            published_at = "2025-01-15T10:00:00Z",
            assets = new[]
            {
                new { name = "aula-win-x64.zip", size = 123, browser_download_url = "https://example.com/aula.zip" },
            },
        };
    }

    private void EnqueueRelease(string tag = "v1.0.0", bool prerelease = false) =>
        _http.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = Json(Release(tag, prerelease)) });

    [Fact]
    public void Help_PrintsUsage_ReturnsZero()
    {
        int result = Runner.Run(new HelpCommand());

        Assert.Equal(0, result);
        Assert.Contains("Usage: aula <command>", _out.ToString());
    }

    [Fact]
    public void List_NoDevices_PrintsMessage_ReturnsOne()
    {
        int result = Runner.Run(new ListCommand());

        Assert.Equal(1, result);
        Assert.Contains("No AULA devices found.", _out.ToString());
    }

    [Fact]
    public void List_WithDevices_PrintsDetails()
    {
        AddF75Device();

        int result = Runner.RunList();

        Assert.Equal(0, result);
        Assert.Contains("258A:010C", _out.ToString());
        Assert.Contains("AULA F75", _out.ToString());
    }

    [Fact]
    public void Effects_PrintsEffectTable()
    {
        int result = Runner.Run(new EffectsCommand("f75"));

        Assert.Equal(0, result);
        Assert.Contains("wave", _out.ToString());
        Assert.Contains("marquee", _out.ToString());
    }

    [Fact]
    public void Info_PrintsDeviceAndModel()
    {
        AddF75Device();
        _transport.Responses.Enqueue(CliFakes.ModelResponse());

        int result = Runner.Run(new InfoCommand("f75"));

        Assert.Equal(0, result);
        Assert.Contains("AULA F75", _out.ToString());
        Assert.Contains("SN123", _out.ToString());
        Assert.Contains("Model       : 0x03", _out.ToString());
    }

    [Fact]
    public void Effect_AppliesAndPrints()
    {
        AddF75Device();
        EnqueueConfigResponse();

        int result = Runner.Run(new EffectCommand("f75", 3, Brightness: 4, Speed: 2, Color: null, Colorful: false));

        Assert.Equal(0, result);
        Assert.Contains("Applied effect '3'", _out.ToString());
    }

    [Fact]
    public void Effect_WithRawFlags_PrintsFlags()
    {
        AddF75Device();
        EnqueueConfigResponse();

        int result = Runner.Run(new EffectCommand("f75", 3, null, null, null, false, RawFlags: 0x20));

        Assert.Equal(0, result);
        Assert.Contains("flags=0x20", _out.ToString());
    }

    [Fact]
    public void Off_AppliesEffectZero()
    {
        AddF75Device();
        EnqueueConfigResponse();

        int result = Runner.Run(new OffCommand("f75"));

        Assert.Equal(0, result);
        byte[] last = _transport.Sent[^1];
        Assert.Equal(0x00, last[18]);
    }

    [Fact]
    public void Reset_ResetsLighting()
    {
        AddF75Device();
        EnqueueConfigResponse();

        int result = Runner.Run(new ResetCommand("f75"));

        Assert.Equal(0, result);
        Assert.Contains("factory defaults", _out.ToString());
    }

    [Fact]
    public void Reset_WithVendorPath_MissingFile_ReturnsOne()
    {
        string path = Path.Combine(_root, "nope", "tool.exe");

        int result = Runner.Run(new ResetCommand("f75", path));

        Assert.Equal(1, result);
        Assert.Contains("reset tool not found", _err.ToString());
    }

    [Fact]
    public void Reset_WithVendorDirectory_NoExe_ReturnsOne()
    {
        string dir = Path.Combine(_root, "tools-empty");
        Directory.CreateDirectory(dir);

        int result = Runner.Run(new ResetCommand("f75", dir));

        Assert.Equal(1, result);
        Assert.Contains("no reset tool", _err.ToString());
    }

    [Fact]
    public void Reset_WithVendorExe_LaunchFailure_ReturnsOne()
    {
        string dir = Path.Combine(_root, "tools");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "reset.exe"), "not an exe");

        int result = Runner.Run(new ResetCommand("f75", dir));

        Assert.Equal(1, result);
        Assert.Contains("cannot launch reset tool", _err.ToString());
    }

    [Fact]
    public void PerKey_WithKeyColors_Applies()
    {
        AddF75Device();
        EnqueueConfigResponse();

        int result = Runner.Run(new PerKeyCommand(
            "f75", new RgbColor(0xFF, 0xFF, 0xFF), FillAll: true,
            KeyColors: new Dictionary<string, RgbColor> { ["w"] = new(0xFF, 0x00, 0x00) }));

        Assert.Equal(0, result);
        Assert.Contains("Applied per-key custom mode", _out.ToString());
        Assert.Contains("w=#FF0000", _out.ToString());
    }

    [Fact]
    public void PerKey_FillAll_Applies()
    {
        AddF75Device();
        EnqueueConfigResponse();

        int result = Runner.Run(new PerKeyCommand("f75", new RgbColor(0x00, 0xFF, 0x00), FillAll: true));

        Assert.Equal(0, result);
        Assert.Contains("126 LEDs = #00FF00", _out.ToString());
    }

    [Fact]
    public void PerKey_SingleLed_Applies()
    {
        AddF75Device();
        EnqueueConfigResponse();

        int result = Runner.Run(new PerKeyCommand("f75", new RgbColor(1, 2, 3), LedIndex: 14));

        Assert.Equal(0, result);
        Assert.Contains("LED 14 = #010203", _out.ToString());
    }

    [Fact]
    public void PerKey_UnknownKey_Throws()
    {
        AddF75Device();
        EnqueueConfigResponse();

        Assert.Throws<AulaException>(() => Runner.Run(new PerKeyCommand(
            "f75", new RgbColor(255, 255, 255),
            KeyColors: new Dictionary<string, RgbColor> { ["zzz"] = new(1, 2, 3) })));
    }

    [Fact]
    public void PerKey_LedIndex_OutOfRange_Throws()
    {
        AddF75Device();
        EnqueueConfigResponse();

        Assert.Throws<AulaException>(() => Runner.Run(new PerKeyCommand("f75", new RgbColor(1, 2, 3), LedIndex: 999)));
    }

    [Fact]
    public void Profile_List_Empty_ReturnsZero()
    {
        int result = Runner.Run(new ProfileCommand("list"));

        Assert.Equal(0, result);
        Assert.Equal(string.Empty, _out.ToString());
    }

    [Fact]
    public void Profile_List_WithSavedProfile()
    {
        AddF75Device();
        EnqueueConfigResponse();
        Runner.Run(new ProfileCommand("save", "gaming", Color: new RgbColor(1, 2, 3)));

        int result = Runner.Run(new ProfileCommand("list"));

        Assert.Equal(0, result);
        Assert.Contains("gaming", _out.ToString());
    }

    [Fact]
    public void Profile_Save_ReadsKeyboardAndSaves()
    {
        AddF75Device();
        EnqueueConfigResponse();

        int result = Runner.Run(new ProfileCommand("save", "gaming"));

        Assert.Equal(0, result);
        Assert.Contains("Saved profile 'gaming'", _out.ToString());
        Assert.True(File.Exists(Path.Combine(_profileDir, "gaming.json")));
    }

    [Fact]
    public void Profile_Delete_RemovesProfile()
    {
        AddF75Device();
        EnqueueConfigResponse();
        Runner.Run(new ProfileCommand("save", "temp", Color: new RgbColor(1, 2, 3)));

        int result = Runner.Run(new ProfileCommand("delete", "temp"));

        Assert.Equal(0, result);
        Assert.Contains("Deleted profile 'temp'", _out.ToString());
    }

    [Fact]
    public void Profile_Delete_Missing_ReturnsOne()
    {
        int result = Runner.Run(new ProfileCommand("delete", "missing"));

        Assert.Equal(1, result);
        Assert.Contains("not found", _err.ToString());
    }

    [Fact]
    public void Profile_Apply_AppliesSavedProfile()
    {
        AddF75Device();
        EnqueueConfigResponse();
        Runner.Run(new ProfileCommand("save", "wave", Color: new RgbColor(1, 2, 3)));

        EnqueueConfigResponse();
        int result = Runner.Run(new ProfileCommand("apply", "wave"));

        Assert.Equal(0, result);
        Assert.Contains("Applied profile 'wave'", _out.ToString());
    }

    [Fact]
    public void Profile_UnknownAction_Throws()
    {
        Assert.Throws<AulaException>(() => Runner.Run(new ProfileCommand("bogus", "x")));
    }

    [Fact]
    public void Update_Check_NoUpdate_PrintsUpToDate()
    {
        int result = Runner.Run(new UpdateCommand("check"));

        Assert.Equal(0, result);
        Assert.Contains("is up to date", _out.ToString());
    }

    [Fact]
    public void Update_Check_Available_PrintsVersion()
    {
        EnqueueRelease("v1.2.0");

        int result = Runner.Run(new UpdateCommand("check"));

        Assert.Equal(0, result);
        Assert.Contains("1.2.0", _out.ToString());
        Assert.Contains("release notes", _out.ToString());
    }

    [Fact]
    public void Update_Install_NoForce_PrintsPrompt()
    {
        EnqueueRelease("v1.2.0");

        int result = Runner.Run(new UpdateCommand("install"));

        Assert.Equal(0, result);
        Assert.Contains("--force", _out.ToString());
    }

    [Fact]
    public void Update_Install_UpToDate()
    {
        int result = Runner.Run(new UpdateCommand("install", Force: true));

        Assert.Equal(0, result);
        Assert.Contains("You are up to date.", _out.ToString());
    }

    [Fact]
    public void Update_Install_Force_DownloadsAndStages()
    {
        string zipSource = Path.Combine(_root, "zip");
        Directory.CreateDirectory(zipSource);
        File.WriteAllText(Path.Combine(zipSource, "AulaManager.dll"), "payload");
        string zipPath = Path.Combine(_root, "update.zip");
        ZipFile.CreateFromDirectory(zipSource, zipPath);

        Directory.CreateDirectory(_staging);
        EnqueueRelease("v1.2.0");
        _http.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(File.ReadAllBytes(zipPath)),
        });

        int result = Runner.Run(new UpdateCommand("install", Force: true));

        Assert.Equal(0, result);
        Assert.NotNull(_launched);
        Assert.True(File.Exists(Path.Combine(_staging, "update.cmd")));
        Assert.Contains("Update staged", _out.ToString());
    }

    [Fact]
    public void Update_UnknownAction_ReturnsOne()
    {
        int result = Runner.Run(new UpdateCommand("bogus"));

        Assert.Equal(1, result);
        Assert.Contains("Unsupported update action", _err.ToString());
    }

    [Fact]
    public void Update_HttpError_ReturnsOne()
    {
        _http.Throw(new HttpRequestException("network down"));

        int result = Runner.Run(new UpdateCommand("check"));

        Assert.Equal(1, result);
        Assert.Contains("cannot reach GitHub", _err.ToString());
    }

    [Fact]
    public void Update_Timeout_ReturnsOne()
    {
        _http.Throw(new OperationCanceledException());

        int result = Runner.Run(new UpdateCommand("check"));

        Assert.Equal(1, result);
        Assert.Contains("timed out", _err.ToString());
    }

    [Fact]
    public void Dump_PrintsConfigAndColorProfile()
    {
        AddF75Device();
        EnqueueConfigResponse();
        _transport.Responses.Enqueue(CliFakes.ColorProfileResponse());

        int result = Runner.Run(new DumpCommand("f75"));

        Assert.Equal(0, result);
        Assert.Contains("Effect      : 10", _out.ToString());
        Assert.Contains("Color profile (520 bytes)", _out.ToString());
    }

    [Fact]
    public void WirelessScan_NoReceiver_ReturnsOne()
    {
        int result = Runner.Run(new WirelessCommand("scan"));

        Assert.Equal(1, result);
        Assert.Contains("No 2.4G receiver found.", _out.ToString());
    }

    [Fact]
    public void WirelessScan_WithReceiver_PrintsCollections()
    {
        AddWirelessDevice();

        int result = Runner.Run(new WirelessCommand("scan"));

        Assert.Equal(0, result);
        Assert.Contains("2.4G receiver (1 collection(s))", _out.ToString());
    }

    [Fact]
    public void WirelessRead_NoCollection_ReturnsOne()
    {
        AddF75Device();

        int result = Runner.Run(new WirelessCommand("read"));

        Assert.Equal(1, result);
        Assert.Contains("No 2.4G receiver collection", _out.ToString());
    }

    [Fact]
    public void Wireless_UnknownAction_ReturnsOne()
    {
        int result = Runner.Run(new WirelessCommand("bogus"));

        Assert.Equal(1, result);
        Assert.Contains("Unknown wireless action", _err.ToString());
    }

    [Fact]
    public void WirelessEffect_NoReceiver_Throws()
    {
        Assert.Throws<AulaException>(() => Runner.Run(
            new WirelessCommand("effect", new WirelessEffectCommand(3))));
    }

    [Fact]
    public void WirelessEffect_AppliesOverReceiver()
    {
        AddWirelessDevice();
        for (byte i = 0; i < 10; i++)
        {
            _transport.Responses.Enqueue(CliFakes.WirelessReadFragment(i));
        }

        for (int i = 0; i < 10; i++)
        {
            _transport.Responses.Enqueue(WirelessFrame.Build(WirelessFrame.CmdWrite, 0x00, 0, new byte[WirelessFrame.PayloadLength]));
        }

        for (int i = 0; i < 37; i++)
        {
            _transport.Responses.Enqueue(WirelessFrame.Build(WirelessFrame.CmdColor, 0x00, 0, new byte[WirelessFrame.PayloadLength]));
        }

        _transport.Responses.Enqueue(WirelessFrame.Build(WirelessFrame.CmdSave, 0x00, 0, new byte[WirelessFrame.PayloadLength]));

        int result = Runner.Run(new WirelessCommand("effect", new WirelessEffectCommand(3, Brightness: 5)));

        Assert.Equal(0, result);
        Assert.Contains("Wireless: applied effect '3'", _out.ToString());
    }

    [Fact]
    public void PrintUpdateCheck_Available_PrintsFields()
    {
        var info = new UpdateInfo(
            true, "1.2.0", "0.9.0", "https://example.com/aula.zip", "notes", DateTimeOffset.Parse("2025-01-15T10:00:00Z"), "aula-win-x64.zip");

        int result = Runner.PrintUpdateCheck(info);

        Assert.Equal(0, result);
        Assert.Contains("Latest        : 1.2.0", _out.ToString());
        Assert.Contains("notes", _out.ToString());
    }

    [Fact]
    public void Print_OutputsToStreams()
    {
        Assert.Equal(0, Runner.Print("hello"));
        Assert.Equal(1, Runner.Print("oops", error: true));

        Assert.Contains("hello", _out.ToString());
        Assert.Contains("oops", _err.ToString());
    }

    [Fact]
    public void Run_UnknownCommand_ReturnsZero()
    {
        int result = Runner.Run(new WirelessEffectCommand(3));

        Assert.Equal(0, result);
    }

    [Fact]
    public void WirelessRead_WithCollection_ProbesAndReadsFragments()
    {
        AddWirelessDevice();
        _transport.Responses.Enqueue(CliFakes.WirelessReadFragment(0));
        _transport.Responses.Enqueue(CliFakes.WirelessReadFragment(1));
        _transport.Responses.Enqueue(CliFakes.WirelessReadFragment(2));

        int result = Runner.Run(new WirelessCommand("read"));

        Assert.Equal(0, result);
        Assert.Contains("READ config: 3/10 fragments", _out.ToString());
        Assert.Contains("[00] OK", _out.ToString());
        Assert.Contains("[02] OK", _out.ToString());
    }

    [Fact]
    public void WirelessRead_OpenFailure_ContinuesToNextCollection()
    {
        AddWirelessDevice();
        _transport.OpenException = new IOException("access denied");

        int result = Runner.Run(new WirelessCommand("read"));

        Assert.Equal(1, result);
        Assert.Contains("open failed: access denied", _out.ToString());
    }

    [Fact]
    public void WirelessRead_NoResponses_PrintsNoResponse()
    {
        AddWirelessDevice();

        int result = Runner.Run(new WirelessCommand("read"));

        Assert.Equal(1, result);
        Assert.Contains("no response on this collection", _out.ToString());
    }

    [Fact]
    public void Reset_VendorTool_LaunchesAndReturnsExitCode()
    {
        string tool = Path.Combine(Environment.SystemDirectory, "where.exe");
        if (!OperatingSystem.IsWindows() || !File.Exists(tool))
        {
            return;
        }

        int result = Runner.Run(new ResetCommand("f75", tool));

        Assert.Contains("Launching official reset tool", _out.ToString());
        Assert.Contains("Reset tool exited with code 2", _out.ToString());
        Assert.Equal(2, result);
    }

    public void Dispose()
    {
        _out.Dispose();
        _err.Dispose();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
