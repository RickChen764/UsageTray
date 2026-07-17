using System.Net;
using System.Security.Cryptography;
using UsageTray.Models;
using UsageTray.Services;
using Xunit;

namespace UsageTray.Tests;

public sealed class UpdateServiceTests
{
    [Theory]
    [InlineData("v1.2.3", 1, 2, 3)]
    [InlineData("1.2.3", 1, 2, 3)]
    [InlineData("V2.0.0-beta.1", 2, 0, 0)]
    public void TryParseVersion_HandlesReleaseTags(
        string tag,
        int major,
        int minor,
        int build)
    {
        Assert.True(UpdateService.TryParseVersion(tag, out var version));
        Assert.Equal(new Version(major, minor, build), version);
    }

    [Fact]
    public void ParseChecksum_ReadsStandardSha256File()
    {
        const string hash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        Assert.Equal(hash.ToUpperInvariant(),
            UpdateService.ParseChecksum($"{hash}  UsageTray-win-x64.exe\n"));
    }

    [Theory]
    [InlineData("1.1.2", "1.1.2.0", true)]
    [InlineData("1.1.2.0", "1.1.2.0", true)]
    [InlineData("1.1.2.1", "1.1.2", false)]
    [InlineData("1.1.3", "1.1.2.0", false)]
    public void VersionsEquivalent_NormalizesMissingRevision(
        string left,
        string right,
        bool expected)
    {
        Assert.Equal(expected,
            UpdateService.VersionsEquivalent(Version.Parse(left), Version.Parse(right)));
    }

    [Theory]
    [InlineData("1.1.3.0", "1.1.3", false)]
    [InlineData("1.1.3", "1.1.3.0", false)]
    [InlineData("1.1.4", "1.1.3.0", true)]
    [InlineData("1.2.0", "1.1.99.0", true)]
    public void IsNewerVersion_UsesNormalizedSemanticOrdering(
        string candidate,
        string current,
        bool expected)
    {
        Assert.Equal(expected,
            UpdateService.IsNewerVersion(Version.Parse(candidate), Version.Parse(current)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("1234")]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    public void ParseChecksum_RejectsInvalidContent(string content)
    {
        Assert.Throws<UpdateException>(() => UpdateService.ParseChecksum(content));
    }

    [Fact]
    public void ParseRelease_RequiresExecutableAndChecksumAssets()
    {
        var release = UpdateService.ParseRelease(ValidReleaseJson);

        Assert.Equal(new Version(1, 2, 0), release.Version);
        Assert.Equal("v1.2.0", release.Tag);
        Assert.Equal("UsageTray 1.2.0", release.Name);
        Assert.Equal(71_000_000, release.ExecutableSize);
        Assert.Equal(UpdateService.ExecutableAssetName,
            Path.GetFileName(release.ExecutableUrl.AbsolutePath));
        Assert.Equal(UpdateService.ChecksumAssetName,
            Path.GetFileName(release.ChecksumUrl.AbsolutePath));
    }

    [Fact]
    public void ParseRelease_RejectsMissingChecksumAsset()
    {
        const string json = """
            {
              "tag_name": "v1.2.0",
              "name": "UsageTray 1.2.0",
              "body": "更新说明",
              "html_url": "https://github.com/RickChen764/UsageTray/releases/tag/v1.2.0",
              "assets": [
                {
                  "name": "UsageTray-win-x64.exe",
                  "browser_download_url": "https://github.com/RickChen764/UsageTray/releases/download/v1.2.0/UsageTray-win-x64.exe",
                  "size": 71000000
                }
              ]
            }
            """;

        Assert.Throws<UpdateException>(() => UpdateService.ParseRelease(json));
    }

    [Theory]
    [InlineData("https://github.com/RickChen764/UsageTray/releases/tag/v1.2.3", "v1.2.3")]
    [InlineData("https://github.com/RickChen764/UsageTray/releases/tag/v1.1.3.0", "v1.1.3.0")]
    public void ParseTagFromReleasePage_ReadsRedirectTarget(string url, string expected)
    {
        Assert.Equal(expected, UpdateService.ParseTagFromReleasePage(new Uri(url)));
    }

    [Theory]
    [InlineData("https://github.com/RickChen764/UsageTray/releases/latest")]
    [InlineData("https://example.com/RickChen764/UsageTray/releases/tag/v1.2.3")]
    public void ParseTagFromReleasePage_RejectsUnexpectedUrls(string url)
    {
        Assert.Throws<UpdateException>(() =>
            UpdateService.ParseTagFromReleasePage(new Uri(url)));
    }

    [Fact]
    public async Task DownloadAndVerifyAsync_ReportsDownloadStagesAndPercentage()
    {
        var executable = await File.ReadAllBytesAsync(
            typeof(UpdateServiceTests).Assembly.Location.Replace(
                "UsageTray.Tests.dll", "UsageTray.exe", StringComparison.Ordinal));
        var hash = Convert.ToHexString(SHA256.HashData(executable));
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith(".sha256",
                    StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent($"{hash}  {UpdateService.ExecutableAssetName}")
                };
            }

            var content = new ByteArrayContent(executable);
            content.Headers.ContentLength = executable.Length;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });
        using var service = new UpdateService(handler);
        var stages = new List<UpdateProgress>();
        var release = new UpdateRelease(
            new Version(1, 1, 7),
            "v1.1.7-test-progress",
            "UsageTray test",
            string.Empty,
            new Uri("https://github.com/RickChen764/UsageTray/releases/tag/v1.1.7"),
            new Uri("https://github.com/RickChen764/UsageTray/releases/download/v1.1.7/UsageTray-win-x64.exe"),
            new Uri("https://github.com/RickChen764/UsageTray/releases/download/v1.1.7/UsageTray-win-x64.exe.sha256"),
            executable.Length);

        var downloaded = await service.DownloadAndVerifyAsync(
            release, new InlineProgress<UpdateProgress>(stages.Add));

        Assert.Equal(UpdateProgressStage.Preparing, stages[0].Stage);
        Assert.Contains(stages, value =>
            value.Stage == UpdateProgressStage.Downloading && value.Percentage == 0);
        Assert.Contains(stages, value =>
            value.Stage == UpdateProgressStage.Downloading && value.Percentage == 100);
        Assert.Equal(UpdateProgressStage.Verifying, stages[^1].Stage);
        Assert.True(File.Exists(downloaded.ExecutablePath));
    }

    private const string ValidReleaseJson = """
        {
          "tag_name": "v1.2.0",
          "name": "UsageTray 1.2.0",
          "body": "更新说明",
          "html_url": "https://github.com/RickChen764/UsageTray/releases/tag/v1.2.0",
          "assets": [
            {
              "name": "UsageTray-win-x64.exe",
              "browser_download_url": "https://github.com/RickChen764/UsageTray/releases/download/v1.2.0/UsageTray-win-x64.exe",
              "size": 71000000
            },
            {
              "name": "UsageTray-win-x64.exe.sha256",
              "browser_download_url": "https://github.com/RickChen764/UsageTray/releases/download/v1.2.0/UsageTray-win-x64.exe.sha256",
              "size": 88
            }
          ]
        }
        """;

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = responseFactory(request);
            response.RequestMessage = request;
            return Task.FromResult(response);
        }
    }
}
