using System.Reflection;

namespace Aula.Core.Models;

public static class ProductInfo
{
    public const string RepositoryOwner = "free-soldier28";
    public const string RepositoryName = "Aula-Manager";
    public const string Repository = $"https://github.com/{RepositoryOwner}/{RepositoryName}";
    public const string ReleasesApi = $"https://api.github.com/repos/{RepositoryOwner}/{RepositoryName}/releases/latest";
    public const string AppName = "AulaManager";

    public static Version Version =>
        Assembly.GetEntryAssembly()?.GetName().Version
        ?? new Version(0, 9, 0);

    public static string VersionString => Version.ToString(3);
}
