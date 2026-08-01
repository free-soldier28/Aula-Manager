namespace Aula.Core.Updating;

public sealed record GitHubRelease(
    string Tag,
    string Name,
    string Body,
    bool Prerelease,
    DateTimeOffset? PublishedAt,
    IReadOnlyList<GitHubAsset> Assets);

public sealed record GitHubAsset(string Name, long Size, string BrowserDownloadUrl);
