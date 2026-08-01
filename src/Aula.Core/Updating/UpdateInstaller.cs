using System.Diagnostics;

namespace Aula.Core.Updating;

public sealed class UpdateInstaller
{
    private readonly string _currentExecutable;
    private readonly string _currentDirectory;
    private readonly string _stagingDirectory;

    public UpdateInstaller(string? currentExecutable = null, string? stagingDirectory = null)
    {
        _currentExecutable = currentExecutable ?? Environment.ProcessPath
            ?? throw new AulaException("Cannot determine the current executable path.");
        _currentDirectory = Path.GetDirectoryName(_currentExecutable)
            ?? throw new AulaException("Cannot determine the application directory.");
        _stagingDirectory = stagingDirectory ?? Path.Combine(
            Path.GetTempPath(), "aula-update", Guid.NewGuid().ToString("N"));
    }

    public string StagingDirectory => _stagingDirectory;

    public async Task InstallAsync(
        string zipPath,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(_stagingDirectory);
        System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, _stagingDirectory, overwriteFiles: true);

        string script = CreateUpdaterScript();
        var startInfo = new ProcessStartInfo
        {
            FileName = script,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.Environment["AULA_PID"] = Environment.ProcessId.ToString();
        startInfo.Environment["AULA_CURRENT_EXE"] = _currentExecutable;
        startInfo.Environment["AULA_STAGING"] = _stagingDirectory;

        Process.Start(startInfo);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    private string CreateUpdaterScript()
    {
        if (OperatingSystem.IsWindows())
        {
            string path = Path.Combine(_stagingDirectory, "update.cmd");
            string body = """
                @echo off
                setlocal
                for %%F in ("%AULA_CURRENT_EXE%") do set "target=%%~dpF"
                set /a tries=0
                :wait
                tasklist /FI "PID eq %AULA_PID%" 2>nul | findstr /I "%AULA_PID%" >nul
                if not errorlevel 1 (
                    set /a tries+=1
                    if %tries% gtr 60 exit /b 1
                    timeout /t 1 /nobreak >nul
                    goto wait
                )
                copy /Y "%AULA_STAGING%\*" "%target%" >nul
                if errorlevel 1 exit /b 1
                del /Q "%AULA_STAGING%" >nul 2>nul
                start "" "%AULA_CURRENT_EXE%"
                """;
            File.WriteAllText(path, body);
            return path;
        }

        string sh = Path.Combine(_stagingDirectory, "update.sh");
        string shBody = """
            #!/bin/sh
            target="$(dirname "$AULA_CURRENT_EXE")"
            tries=0
            while kill -0 "$AULA_PID" 2>/dev/null; do
                tries=$((tries+1))
                [ "$tries" -gt 60 ] && exit 1
                sleep 1
            done
            cp -f "$AULA_STAGING"/. "$target"/ 2>/dev/null || cp -f "$AULA_STAGING"/* "$target"/
            rm -rf "$AULA_STAGING"
            "$AULA_CURRENT_EXE" &
            """;
        File.WriteAllText(sh, shBody);
        Process.Start("chmod", $"+x \"{sh}\"").WaitForExit();
        return sh;
    }
}
