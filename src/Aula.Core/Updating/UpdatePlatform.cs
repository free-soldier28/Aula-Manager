using System.Runtime.InteropServices;

namespace Aula.Core.Updating;

public sealed record UpdatePlatform(
    string Os,
    string Arch,
    string Runtimes)
{
    public static UpdatePlatform Detect() => new(GetOs(), GetArch(), GetRuntimes());

    private static string GetOs()
    {
        if (OperatingSystem.IsWindows())
        {
            return "windows";
        }

        if (OperatingSystem.IsMacOS())
        {
            return "macos";
        }

        return "linux";
    }

    private static string GetArch()
    {
        return RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            Architecture.X86 => "x86",
            _ => "any",
        };
    }

    private static string GetRuntimes()
    {
        var runtimes = new List<string>();
        if (OperatingSystem.IsWindows())
        {
            runtimes.Add("win");
        }
        else if (OperatingSystem.IsMacOS())
        {
            runtimes.Add("osx");
            runtimes.Add("macos");
        }
        else
        {
            runtimes.Add("linux");
        }

        runtimes.Add(GetArch());
        runtimes.Add(RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant());
        return string.Join("-", runtimes);
    }

    public bool MatchesAsset(string assetName)
    {
        return OsAliases.Any(a => assetName.Contains(a, StringComparison.OrdinalIgnoreCase)) &&
               assetName.Contains(Arch, StringComparison.OrdinalIgnoreCase);
    }

    private IReadOnlyList<string> OsAliases
    {
        get
        {
            if (OperatingSystem.IsWindows())
            {
                return new[] { "windows", "win" };
            }

            if (OperatingSystem.IsMacOS())
            {
                return new[] { "macos", "osx" };
            }

            return new[] { "linux" };
        }
    }
}
