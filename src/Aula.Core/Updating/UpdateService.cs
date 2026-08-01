using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aula.Core.Models;

namespace Aula.Core.Updating;

public sealed record UpdateInfo(
    bool IsAvailable,
    string? LatestVersion,
    string? CurrentVersion,
    string? DownloadUrl,
    string? ReleaseNotes,
    DateTimeOffset? PublishedAt,
    string? AssetName)
{
    public static UpdateInfo None(string currentVersion) =>
        new(false, null, currentVersion, null, null, null, null);
}

public sealed class UpdateService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient _http;
    private readonly string _releasesApi;
    private readonly UpdatePlatform _platform;
    private readonly string _currentVersion;

    public UpdateService(
        HttpClient? http = null,
        string? releasesApi = null,
        UpdatePlatform? platform = null,
        string? currentVersion = null)
    {
        _http = http ?? new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("AulaManager", ProductInfo.VersionString));
        _releasesApi = releasesApi ?? ProductInfo.ReleasesApi;
        _platform = platform ?? UpdatePlatform.Detect();
        _currentVersion = currentVersion ?? ProductInfo.VersionString;
    }

    public async Task<UpdateInfo> CheckAsync(CancellationToken ct = default)
    {
        GitHubRelease? release = await GetLatestReleaseAsync(ct).ConfigureAwait(false);

        if (release is null || release.Prerelease)
        {
            return UpdateInfo.None(_currentVersion);
        }

        string? latestVersion = ParseTag(release.Tag);
        if (latestVersion is null || !IsNewer(latestVersion, _currentVersion))
        {
            return UpdateInfo.None(_currentVersion);
        }

        GitHubAsset? asset = release.Assets.FirstOrDefault(a => _platform.MatchesAsset(a.Name));
        if (asset is null)
        {
            return UpdateInfo.None(_currentVersion);
        }

        return new UpdateInfo(
            IsAvailable: true,
            LatestVersion: latestVersion,
            CurrentVersion: _currentVersion,
            DownloadUrl: asset.BrowserDownloadUrl,
            ReleaseNotes: release.Body,
            PublishedAt: release.PublishedAt,
            AssetName: asset.Name);
    }

    public async Task<byte[]> DownloadAsync(UpdateInfo update, CancellationToken ct = default)
    {
        if (update.DownloadUrl is null)
        {
            throw new AulaException("Update has no download URL.");
        }

        using var response = await _http.GetAsync(update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
    }

    public async Task<string> DownloadToFileAsync(
        UpdateInfo update,
        string destinationDirectory,
        CancellationToken ct = default)
    {
        byte[] bytes = await DownloadAsync(update, ct).ConfigureAwait(false);
        string fileName = update.AssetName ?? "update.zip";
        string path = Path.Combine(destinationDirectory, fileName);
        await File.WriteAllBytesAsync(path, bytes, ct).ConfigureAwait(false);
        return path;
    }

    private async Task<GitHubRelease?> GetLatestReleaseAsync(CancellationToken ct)
    {
        using var response = await _http.GetAsync(_releasesApi, ct).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        string json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return JsonSerializer.Deserialize<ReleaseDto>(json, JsonOptions)?.ToModel();
    }

    private static string? ParseTag(string tag) =>
        tag.TrimStart('v', 'V').Split('-')[0];

    private static bool IsNewer(string candidate, string current)
    {
        return Version.TryParse(candidate, out var c) &&
               Version.TryParse(current, out var baseVersion) &&
               c > baseVersion;
    }

    private sealed class ReleaseDto
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; set; }

        [JsonPropertyName("published_at")]
        public DateTimeOffset? PublishedAt { get; set; }

        [JsonPropertyName("assets")]
        public List<AssetDto>? Assets { get; set; }

        public GitHubRelease ToModel() => new(
            TagName ?? string.Empty,
            Name ?? string.Empty,
            Body ?? string.Empty,
            Prerelease,
            PublishedAt,
            Assets?.Select(a => a.ToModel()).ToList() ?? new List<GitHubAsset>());
    }

    private sealed class AssetDto
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("size")]
        public long Size { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }

        public GitHubAsset ToModel() => new(
            Name ?? string.Empty,
            Size,
            BrowserDownloadUrl ?? string.Empty);
    }
}
