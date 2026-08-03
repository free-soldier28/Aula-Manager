using System.Diagnostics;
using System.IO.Compression;
using Aula.Core.Updating;

namespace Aula.Core.Tests;

public class UpdateInstallerTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "aula-tests", "installer-" + Guid.NewGuid().ToString("N"));

    public UpdateInstallerTests()
    {
        Directory.CreateDirectory(_root);
    }

    private string CreateZip()
    {
        string source = Path.Combine(_root, "source");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "AulaManager.dll"), "payload");
        File.WriteAllText(Path.Combine(source, "AulaManager.exe"), "exe");
        string zip = Path.Combine(_root, "update.zip");
        ZipFile.CreateFromDirectory(source, zip);
        return zip;
    }

    [Fact]
    public async Task InstallAsync_StagesZip_AndLaunchesUpdaterScript()
    {
        ProcessStartInfo? captured = null;
        var installer = new UpdateInstaller(
            currentExecutable: Path.Combine(_root, "app", "AulaManager.exe"),
            stagingDirectory: Path.Combine(_root, "staging"),
            startProcess: si => captured = si);

        string zip = CreateZip();
        await installer.InstallAsync(zip);

        Assert.NotNull(captured);
        Assert.StartsWith(installer.StagingDirectory, captured.FileName, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(Path.Combine(installer.StagingDirectory, "AulaManager.dll")));
        Assert.Equal(Environment.ProcessId.ToString(), captured.Environment["AULA_PID"]);
        Assert.Equal(Path.Combine(_root, "app", "AulaManager.exe"), captured.Environment["AULA_CURRENT_EXE"]);
        Assert.Equal(installer.StagingDirectory, captured.Environment["AULA_STAGING"]);
    }

    [Fact]
    public async Task InstallAsync_WritesWindowsUpdaterScript()
    {
        var installer = new UpdateInstaller(
            currentExecutable: Path.Combine(_root, "app", "AulaManager.exe"),
            stagingDirectory: Path.Combine(_root, "staging2"));

        await installer.InstallAsync(CreateZip());

        string scriptPath = Path.Combine(installer.StagingDirectory, "update.cmd");
        Assert.True(File.Exists(scriptPath));
        string script = File.ReadAllText(scriptPath);
        Assert.Contains("tasklist", script);
        Assert.Contains("AULA_PID", script);
    }

    [Fact]
    public async Task InstallAsync_NonWindows_WritesUnixUpdaterScript_AndChmods()
    {
        string? chmodFile = null;
        string? chmodArgs = null;
        var installer = new UpdateInstaller(
            currentExecutable: Path.Combine(_root, "app", "AulaManager.exe"),
            stagingDirectory: Path.Combine(_root, "staging-unix"),
            startProcess: _ => { },
            isWindows: () => false,
            chmod: (file, args) => { chmodFile = file; chmodArgs = args; });

        await installer.InstallAsync(CreateZip());

        string scriptPath = Path.Combine(installer.StagingDirectory, "update.sh");
        Assert.True(File.Exists(scriptPath));
        string script = File.ReadAllText(scriptPath);
        Assert.StartsWith("#!/bin/sh", script, StringComparison.Ordinal);
        Assert.Contains("AULA_PID", script);
        Assert.Contains("AULA_STAGING", script);
        Assert.Equal(scriptPath, chmodFile);
        Assert.Contains("+x", chmodArgs);
    }

    [Fact]
    public void StagingDirectory_ReturnsConfiguredPath()
    {
        var installer = new UpdateInstaller(
            currentExecutable: "aula.exe",
            stagingDirectory: Path.Combine(_root, "staging3"));

        Assert.Equal(Path.Combine(_root, "staging3"), installer.StagingDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
