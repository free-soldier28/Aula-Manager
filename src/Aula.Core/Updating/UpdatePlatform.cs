using System.Runtime.InteropServices;

namespace Aula.Core.Updating;

public sealed record UpdatePlatform(
    string Os,
    string Arch,
    string Runtimes)
{
    public static UpdatePlatform Detect() => new(GetOs(), GetArch(), GetRuntimes());

    public static string OsName(bool isWindows, bool isMacOs)
    {
        if (isWindows)
        {
            return "windows";
        }

        if (isMacOs)
        {
            return "macos";
        }

        return "linux";
    }

    public static string ArchName(Architecture architecture) => architecture switch
    {
        Architecture.X64 => "x64",
        Architecture.Arm64 => "arm64",
        Architecture.X86 => "x86",
        _ => "any",
    };

    public static string RuntimesFor(string os, string arch, string archLower)
    {
        var runtimes = new List<string>();
        if (os.StartsWith("win", StringComparison.OrdinalIgnoreCase))
        {
            runtimes.Add("win");
        }
        else if (os.StartsWith("mac", StringComparison.OrdinalIgnoreCase) ||
                 os.StartsWith("osx", StringComparison.OrdinalIgnoreCase))
        {
            runtimes.Add("osx");
            runtimes.Add("macos");
        }
        else
        {
            runtimes.Add("linux");
        }

        runtimes.Add(arch);
        runtimes.Add(archLower);
        return string.Join("-", runtimes);
    }

    private static string GetOs() => OsName(OperatingSystem.IsWindows(), OperatingSystem.IsMacOS());

    private static string GetArch() => ArchName(RuntimeInformation.ProcessArchitecture);

    private static string GetRuntimes()
    {
        string os = GetOs();
        return RuntimesFor(os, GetArch(), RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant());
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
            if (Os.StartsWith("win", StringComparison.OrdinalIgnoreCase))
            {
                return new[] { "windows", "win" };
            }

            if (Os.StartsWith("mac", StringComparison.OrdinalIgnoreCase) ||
                Os.StartsWith("osx", StringComparison.OrdinalIgnoreCase))
            {
                return new[] { "macos", "osx" };
            }

            return new[] { "linux" };
        }
    }
}
