using System.Net;
using Aula.Core.Updating;

namespace Aula.Core.Tests;

public class UpdateServiceTests
{
    [Fact]
    public async Task Check_NoNewerRelease_ReturnsNotAvailable()
    {
        var handler = new StubHandler(
            """
            {
              "tag_name": "v0.9.0",
              "name": "v0.9.0",
              "body": "test",
              "prerelease": false,
              "published_at": "2026-01-01T00:00:00Z",
              "assets": [ { "name": "aula-app-win-x64.zip", "size": 10, "browser_download_url": "https://x/a.zip" } ]
            }
            """);
        var service = new UpdateService(
            new HttpClient(handler),
            platform: new UpdatePlatform("windows", "x64", "win-x64"),
            currentVersion: "0.9.0");

        UpdateInfo info = await service.CheckAsync();

        Assert.False(info.IsAvailable);
    }

    [Fact]
    public async Task Check_NewerRelease_ReturnsAvailableWithAsset()
    {
        var handler = new StubHandler(
            """
            {
              "tag_name": "v0.10.0",
              "name": "v0.10.0",
              "body": "New hotness",
              "prerelease": false,
              "published_at": "2026-02-01T00:00:00Z",
              "assets": [
                { "name": "aula-app-win-x64.zip", "size": 10, "browser_download_url": "https://x/a-win.zip" },
                { "name": "aula-app-linux-x64.zip", "size": 10, "browser_download_url": "https://x/a-linux.zip" }
              ]
            }
            """);
        var service = new UpdateService(
            new HttpClient(handler),
            platform: new UpdatePlatform("windows", "x64", "win-x64"),
            currentVersion: "0.9.0");

        UpdateInfo info = await service.CheckAsync();

        Assert.True(info.IsAvailable);
        Assert.Equal("0.10.0", info.LatestVersion);
        Assert.Equal("aula-app-win-x64.zip", info.AssetName);
        Assert.Equal("https://x/a-win.zip", info.DownloadUrl);
        Assert.Equal("New hotness", info.ReleaseNotes);
    }

    [Fact]
    public async Task Check_Prerelease_IsIgnored()
    {
        var handler = new StubHandler(
            """
            {
              "tag_name": "v0.10.0-beta.1",
              "name": "beta",
              "body": "beta",
              "prerelease": true,
              "published_at": null,
              "assets": []
            }
            """);
        var service = new UpdateService(
            new HttpClient(handler),
            platform: new UpdatePlatform("linux", "x64", "linux-x64"),
            currentVersion: "0.9.0");

        UpdateInfo info = await service.CheckAsync();

        Assert.False(info.IsAvailable);
    }

    [Fact]
    public async Task Check_NoMatchingAsset_ReturnsNotAvailable()
    {
        var handler = new StubHandler(
            """
            {
              "tag_name": "v0.10.0",
              "name": "v0.10.0",
              "body": "",
              "prerelease": false,
              "published_at": null,
              "assets": [ { "name": "aula-app-linux-x64.zip", "size": 1, "browser_download_url": "https://x/l.zip" } ]
            }
            """);
        var service = new UpdateService(
            new HttpClient(handler),
            platform: new UpdatePlatform("windows", "x64", "win-x64"),
            currentVersion: "0.9.0");

        UpdateInfo info = await service.CheckAsync();

        Assert.False(info.IsAvailable);
    }

    [Fact]
    public async Task Check_NotFound_ReturnsNotAvailable()
    {
        var handler = new StubHandler("", HttpStatusCode.NotFound);
        var service = new UpdateService(
            new HttpClient(handler),
            currentVersion: "0.9.0");

        UpdateInfo info = await service.CheckAsync();

        Assert.False(info.IsAvailable);
    }

    [Fact]
    public async Task Download_ReturnsAssetBytes()
    {
        var handler = new StubHandler(
            "not json at all",
            HttpStatusCode.OK,
            new Dictionary<string, string> { ["Content-Type"] = "application/zip" },
            content: "payload-bytes");
        var service = new UpdateService(
            new HttpClient(handler),
            currentVersion: "0.9.0");

        var info = new UpdateInfo(true, "0.10.0", "0.9.0", "https://x/a.zip", null, null, "a.zip");
        byte[] bytes = await service.DownloadAsync(info);

        Assert.Equal("payload-bytes"u8.ToArray(), bytes);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _body;
        private readonly HttpStatusCode _status;
        private readonly byte[] _content;
        private readonly Dictionary<string, string> _headers;

        public StubHandler(
            string body,
            HttpStatusCode status = HttpStatusCode.OK,
            Dictionary<string, string>? headers = null,
            string? content = null)
        {
            _body = body;
            _status = status;
            _headers = headers ?? new Dictionary<string, string>();
            _content = content is null ? [] : System.Text.Encoding.UTF8.GetBytes(content);
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            HttpResponseMessage response = request.RequestUri!.AbsoluteUri.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                ? new HttpResponseMessage(_status) { Content = new ByteArrayContent(_content) }
                : new HttpResponseMessage(_status) { Content = new StringContent(_body) };

            foreach ((string key, string value) in _headers)
            {
                response.Content.Headers.TryAddWithoutValidation(key, value);
            }

            return Task.FromResult(response);
        }
    }
}
