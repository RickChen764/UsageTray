using System.Net.Http.Headers;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using UsageTray.Models;

namespace UsageTray.Services;

internal sealed class UpdateService : IDisposable
{
    internal const string RepositoryOwner = "RickChen764";
    internal const string RepositoryName = "UsageTray";
    internal const string ExecutableAssetName = "UsageTray-win-x64.exe";
    internal const string ChecksumAssetName = "UsageTray-win-x64.exe.sha256";
    private const long MaximumExecutableBytes = 300L * 1024 * 1024;

    private readonly HttpClient _httpClient;

    public UpdateService(HttpMessageHandler? handler = null)
    {
        _httpClient = handler is null ? new HttpClient() : new HttpClient(handler);
        _httpClient.Timeout = TimeSpan.FromSeconds(45);
        _httpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("UsageTray", CurrentVersion.ToString(3)));
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        _httpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    }

    public static Version CurrentVersion
    {
        get
        {
            var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version ??
                                  new Version(0, 0, 0);
            return new Version(
                assemblyVersion.Major,
                assemblyVersion.Minor,
                Math.Max(0, assemblyVersion.Build));
        }
    }

    public static Uri ReleasesPage =>
        new($"https://github.com/{RepositoryOwner}/{RepositoryName}/releases");

    public async Task<UpdateRelease?> CheckAsync(CancellationToken cancellationToken = default)
    {
        var endpoint = new Uri(
            $"https://api.github.com/repos/{RepositoryOwner}/{RepositoryName}/releases/latest");
        using var response = await _httpClient.GetAsync(endpoint, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        if (response.StatusCode is System.Net.HttpStatusCode.Forbidden or
            System.Net.HttpStatusCode.TooManyRequests)
        {
            return await CheckFromPublicRedirectAsync(cancellationToken);
        }

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseRelease(json);
    }

    private async Task<UpdateRelease?> CheckFromPublicRedirectAsync(
        CancellationToken cancellationToken)
    {
        var latestPage = new Uri(
            $"https://github.com/{RepositoryOwner}/{RepositoryName}/releases/latest");
        using var request = new HttpRequestMessage(HttpMethod.Get, latestPage);
        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
        using var response = await _httpClient.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var finalUri = response.RequestMessage?.RequestUri ??
                       throw new UpdateException("无法确定 GitHub 最新版本地址。");
        var tag = ParseTagFromReleasePage(finalUri);
        if (!TryParseVersion(tag, out var version))
        {
            throw new UpdateException($"无法识别 Release 版本：{tag}");
        }

        var downloadRoot =
            $"https://github.com/{RepositoryOwner}/{RepositoryName}/releases/latest/download/";
        return new UpdateRelease(
            version,
            tag,
            $"UsageTray {tag}",
            "GitHub API 当前受限，请在 Release 页面查看更新说明。",
            finalUri,
            new Uri(downloadRoot + ExecutableAssetName),
            new Uri(downloadRoot + ChecksumAssetName),
            null);
    }

    public async Task<DownloadedUpdate> DownloadAndVerifyAsync(
        UpdateRelease release,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ValidateDownloadUri(release.ExecutableUrl);
        ValidateDownloadUri(release.ChecksumUrl);

        var versionDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UsageTray",
            "updates",
            release.Tag);
        Directory.CreateDirectory(versionDirectory);

        var executablePath = Path.Combine(versionDirectory, ExecutableAssetName);
        var partialPath = executablePath + ".download";
        var expectedHash = await DownloadChecksumAsync(release.ChecksumUrl, cancellationToken);

        try
        {
            using var response = await _httpClient.GetAsync(
                release.ExecutableUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            var length = response.Content.Headers.ContentLength;
            if (length is > MaximumExecutableBytes)
            {
                throw new UpdateException("更新包超过允许的最大尺寸。");
            }

            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var output = new FileStream(
                partialPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            var buffer = new byte[81920];
            long received = 0;
            int read;
            while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
            {
                received += read;
                if (received > MaximumExecutableBytes)
                {
                    throw new UpdateException("更新包超过允许的最大尺寸。");
                }

                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                if (length is > 0)
                {
                    progress?.Report((int)Math.Clamp(received * 100 / length.Value, 0, 100));
                }
            }

            await output.FlushAsync(cancellationToken);
            output.Close();

            var actualHash = await ComputeSha256Async(partialPath, cancellationToken);
            if (!string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new UpdateException("更新包 SHA-256 校验失败，已取消安装。");
            }

            var fileVersion = FileVersionInfo.GetVersionInfo(partialPath).FileVersion;
            if (!TryParseVersion(fileVersion ?? string.Empty, out var executableVersion) ||
                !VersionsEquivalent(executableVersion, release.Version))
            {
                throw new UpdateException(
                    $"更新包版本与 Release 不一致（文件 {fileVersion ?? "未知"}，Release {release.Tag}）。");
            }

            File.Move(partialPath, executablePath, overwrite: true);
            progress?.Report(100);
            return new DownloadedUpdate(executablePath, actualHash);
        }
        catch
        {
            TryDelete(partialPath);
            throw;
        }
    }

    internal static UpdateRelease ParseRelease(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var tag = root.GetProperty("tag_name").GetString() ??
                  throw new UpdateException("Release 缺少版本标签。");
        if (!TryParseVersion(tag, out var version))
        {
            throw new UpdateException($"无法识别 Release 版本：{tag}");
        }

        var assets = root.GetProperty("assets").EnumerateArray().ToArray();
        var executable = FindAsset(assets, ExecutableAssetName);
        var checksum = FindAsset(assets, ChecksumAssetName);
        var pageUrl = ReadHttpsUri(root.GetProperty("html_url").GetString(), "Release 页面");

        return new UpdateRelease(
            version,
            tag,
            root.TryGetProperty("name", out var name) ? name.GetString() ?? tag : tag,
            root.TryGetProperty("body", out var body) ? body.GetString() ?? string.Empty : string.Empty,
            pageUrl,
            ReadHttpsUri(executable.GetProperty("browser_download_url").GetString(), "更新包"),
            ReadHttpsUri(checksum.GetProperty("browser_download_url").GetString(), "校验文件"),
            executable.TryGetProperty("size", out var size) && size.TryGetInt64(out var bytes)
                ? bytes
                : null);
    }

    internal static bool TryParseVersion(string value, out Version version)
    {
        var normalized = value.Trim().TrimStart('v', 'V');
        var metadataIndex = normalized.IndexOfAny(['-', '+']);
        if (metadataIndex >= 0)
        {
            normalized = normalized[..metadataIndex];
        }

        return Version.TryParse(normalized, out version!);
    }

    internal static bool VersionsEquivalent(Version left, Version right) =>
        NormalizeVersion(left) == NormalizeVersion(right);

    internal static bool IsNewerVersion(Version candidate, Version current) =>
        NormalizeVersion(candidate) > NormalizeVersion(current);

    private static Version NormalizeVersion(Version version) => new(
        version.Major,
        version.Minor,
        Math.Max(0, version.Build),
        Math.Max(0, version.Revision));

    internal static string ParseChecksum(string content)
    {
        var candidate = content.Trim().Split((char[]?)null,
            StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (candidate is null || candidate.Length != 64 ||
            candidate.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new UpdateException("SHA-256 校验文件格式无效。");
        }

        return candidate.ToUpperInvariant();
    }

    internal static string ParseTagFromReleasePage(Uri uri)
    {
        if (uri.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            throw new UpdateException("GitHub Release 页面地址无效。");
        }

        var marker = "/releases/tag/";
        var markerIndex = uri.AbsolutePath.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            throw new UpdateException("GitHub 未返回具体的 Release 标签。");
        }

        var tag = Uri.UnescapeDataString(uri.AbsolutePath[(markerIndex + marker.Length)..]).Trim('/');
        if (string.IsNullOrWhiteSpace(tag) || tag.Contains('/'))
        {
            throw new UpdateException("GitHub Release 标签无效。");
        }

        return tag;
    }

    private async Task<string> DownloadChecksumAsync(Uri uri, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is > 16 * 1024)
        {
            throw new UpdateException("SHA-256 校验文件尺寸异常。");
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseChecksum(content);
    }

    private static JsonElement FindAsset(JsonElement[] assets, string name)
    {
        foreach (var asset in assets)
        {
            if (asset.TryGetProperty("name", out var assetName) &&
                string.Equals(assetName.GetString(), name, StringComparison.OrdinalIgnoreCase))
            {
                return asset;
            }
        }

        throw new UpdateException($"Release 缺少文件：{name}");
    }

    private static Uri ReadHttpsUri(string? value, string fieldName)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new UpdateException($"{fieldName}地址无效。");
        }

        return uri;
    }

    private static void ValidateDownloadUri(Uri uri)
    {
        if (uri.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            throw new UpdateException("更新地址不是受信任的 GitHub HTTPS 地址。");
        }
    }

    private static async Task<string> ComputeSha256Async(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // 下次下载会覆盖临时文件。
        }
    }

    public void Dispose() => _httpClient.Dispose();
}

internal sealed record DownloadedUpdate(string ExecutablePath, string Sha256);

internal sealed class UpdateException : Exception
{
    public UpdateException(string message) : base(message)
    {
    }
}
