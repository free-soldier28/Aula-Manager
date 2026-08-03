using System.Net;
using System.Text;
using System.Text.Json;
using Aula.Core.Models;
using Aula.Core.Updating;

namespace Aula.Core.Tests;

public class UpdateServiceTests
{
    private sealed class FakeHttpHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new();
        public List<HttpRequestMessage> Requests { get; } = new();

        public void Enqueue(HttpResponseMessage response) => _responses.Enqueue(response);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(_responses.Count > 0 ? _responses.Dequeue() : new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    private static UpdateService Create(
        FakeHttpHandler handler, string currentVersion = "0.9.0",
        string platformOs = "windows", string platformArch = "x64")
    {
        var platform = new UpdatePlatform(platformOs, platformArch, "win-x64");
        return new UpdateService(new HttpClient(handler), platform: platform, currentVersion: currentVersion);
    }

    private static StringContent Json(object payload) =>
        new(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

    private static object Release(
        string tag = "v1.0.0",
        bool prerelease = false,
        string body = "release notes",
        string assetName = "aula-win-x64.zip")
    {
        return new
        {
            tag_name = tag,
            name = tag,
            body,
            prerelease,
            published_at = "2025-01-15T10:00:00Z",
            assets = new[]
            {
                new { name = assetName, size = 1234, browser_download_url = "https://example.com/aula.zip" },
            },
        };
    }

    [Fact]
    public async Task CheckAsync_ReturnsAvailable_WhenNewerVersionExists()
    {
        var handler = new FakeHttpHandler();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = Json(Release("v1.2.0")) });
        var service = Create(handler, currentVersion: "0.9.0");

        UpdateInfo info = await service.CheckAsync();

        Assert.True(info.IsAvailable);
        Assert.Equal("1.2.0", info.LatestVersion);
        Assert.Equal("0.9.0", info.CurrentVersion);
        Assert.Equal("https://example.com/aula.zip", info.DownloadUrl);
        Assert.Equal("release notes", info.ReleaseNotes);
        Assert.Equal("aula-win-x64.zip", info.AssetName);
        Assert.NotNull(info.PublishedAt);
    }

    [Fact]
    public async Task CheckAsync_NoUpdate_WhenSameVersion()
    {
        var handler = new FakeHttpHandler();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = Json(Release("v0.9.0")) });
        var service = Create(handler, currentVersion: "0.9.0");

        UpdateInfo info = await service.CheckAsync();

        Assert.False(info.IsAvailable);
        Assert.Equal("0.9.0", info.CurrentVersion);
    }

    [Fact]
    public async Task CheckAsync_NoUpdate_WhenReleaseIsOlder()
    {
        var handler = new FakeHttpHandler();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = Json(Release("v0.8.0")) });
        var service = Create(handler, currentVersion: "0.9.0");

        Assert.False((await service.CheckAsync()).IsAvailable);
    }

    [Fact]
    public async Task CheckAsync_NoUpdate_WhenPrerelease()
    {
        var handler = new FakeHttpHandler();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = Json(Release("v1.0.0", prerelease: true)) });
        var service = Create(handler, currentVersion: "0.9.0");

        Assert.False((await service.CheckAsync()).IsAvailable);
    }

    [Fact]
    public async Task CheckAsync_NoUpdate_WhenTagNotVersion()
    {
        var handler = new FakeHttpHandler();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = Json(Release("nightly")) });
        var service = Create(handler, currentVersion: "0.9.0");

        Assert.False((await service.CheckAsync()).IsAvailable);
    }

    [Fact]
    public async Task CheckAsync_NoUpdate_WhenNoMatchingAsset()
    {
        var handler = new FakeHttpHandler();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = Json(Release("v1.2.0", assetName: "aula-linux-x64.zip")) });
        var service = Create(handler, currentVersion: "0.9.0");

        Assert.False((await service.CheckAsync()).IsAvailable);
    }

    [Fact]
    public async Task CheckAsync_NoUpdate_WhenEndpointReturnsNotFound()
    {
        var handler = new FakeHttpHandler();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.NotFound));
        var service = Create(handler, currentVersion: "0.9.0");

        Assert.False((await service.CheckAsync()).IsAvailable);
    }

    [Fact]
    public async Task DownloadAsync_ReturnsBytes()
    {
        var handler = new FakeHttpHandler();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[] { 1, 2, 3, 4 }),
        });
        var service = Create(handler, currentVersion: "0.9.0");
        var update = new UpdateInfo(true, "1.0.0", "0.9.0", "https://example.com/aula.zip", null, null, "aula-win-x64.zip");

        byte[] bytes = await service.DownloadAsync(update);

        Assert.Equal(new byte[] { 1, 2, 3, 4 }, bytes);
    }

    [Fact]
    public async Task DownloadAsync_Throws_WhenNoUrl()
    {
        var service = Create(new FakeHttpHandler(), currentVersion: "0.9.0");
        var update = UpdateInfo.None("0.9.0");

        await Assert.ThrowsAsync<AulaException>(() => service.DownloadAsync(update));
    }

    [Fact]
    public async Task DownloadToFileAsync_WritesFile()
    {
        var handler = new FakeHttpHandler();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[] { 0xAA, 0xBB }),
        });
        var service = Create(handler, currentVersion: "0.9.0");
        var update = new UpdateInfo(true, "1.0.0", "0.9.0", "https://example.com/aula.zip", null, null, "aula-win-x64.zip");
        string directory = Path.Combine(Path.GetTempPath(), "aula-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            string path = await service.DownloadToFileAsync(update, directory);

            Assert.Equal(Path.Combine(directory, "aula-win-x64.zip"), path);
            Assert.Equal(new byte[] { 0xAA, 0xBB }, await File.ReadAllBytesAsync(path));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task DownloadToFileAsync_FallsBackToUpdateZip_WhenNoAssetName()
    {
        var handler = new FakeHttpHandler();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[0]) });
        var service = Create(handler, currentVersion: "0.9.0");
        var update = new UpdateInfo(true, "1.0.0", "0.9.0", "https://example.com/aula.zip", null, null, null);
        string directory = Path.Combine(Path.GetTempPath(), "aula-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            string path = await service.DownloadToFileAsync(update, directory);
            Assert.Equal("update.zip", Path.GetFileName(path));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void UpdateInfo_None_SetsNoUpdateFields()
    {
        UpdateInfo info = UpdateInfo.None("0.9.0");

        Assert.False(info.IsAvailable);
        Assert.Null(info.LatestVersion);
        Assert.Equal("0.9.0", info.CurrentVersion);
        Assert.Null(info.DownloadUrl);
        Assert.Null(info.ReleaseNotes);
        Assert.Null(info.PublishedAt);
        Assert.Null(info.AssetName);
    }

    [Fact]
    public void GitHubRelease_And_Asset_AreRecords()
    {
        var asset = new GitHubAsset("a.zip", 42, "https://example.com/a.zip");
        var release = new GitHubRelease("v1.0.0", "v1.0.0", "body", false, null, new[] { asset });

        Assert.Equal("a.zip", release.Assets[0].Name);
        Assert.Equal(42L, release.Assets[0].Size);
        Assert.Equal("https://example.com/a.zip", release.Assets[0].BrowserDownloadUrl);
        Assert.Equal("v1.0.0", release.Name);
        Assert.False(release.Prerelease);
        Assert.Equal("body", release.Body);
    }
}
