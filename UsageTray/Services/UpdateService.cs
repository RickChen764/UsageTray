using System.Net.Http.Headers;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
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
        // GitHub Release 的自包含单文件约 70 MB；慢速或跨境网络需要更宽裕的总下载时间。
        _httpClient.Timeout = TimeSpan.FromMinutes(10);
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
            $"https://api.github.com/repos/{RepositoryOwner}/{RepositoryName}/releases?per_page=100");
        using var response = await _httpClient.GetAsync(endpoint, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        if (response.StatusCode is System.Net.HttpStatusCode.Forbidden or
            System.Net.HttpStatusCode.TooManyRequests)
        {
            return await CheckFromPublicFeedAsync(cancellationToken);
        }

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseReleaseList(json, CurrentVersion);
    }

    private async Task<UpdateRelease?> CheckFromPublicFeedAsync(
        CancellationToken cancellationToken)
    {
        var feedUrl = new Uri(
            $"https://github.com/{RepositoryOwner}/{RepositoryName}/releases.atom");
        using var request = new HttpRequestMessage(HttpMethod.Get, feedUrl);
        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/atom+xml"));
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is > 2 * 1024 * 1024)
        {
            throw new UpdateException("GitHub Release Feed 尺寸异常。");
        }

        var feed = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseReleaseFeed(feed, CurrentVersion);
    }

    public async Task<DownloadedUpdate> DownloadAndVerifyAsync(
        UpdateRelease release,
        IProgress<UpdateProgress>? progress = null,
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
        progress?.Report(new UpdateProgress(UpdateProgressStage.Preparing));
        var expectedHash = await DownloadChecksumAsync(release.ChecksumUrl, cancellationToken);

        try
        {
            using var response = await _httpClient.GetAsync(
                release.ExecutableUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            var length = response.Content.Headers.ContentLength ?? release.ExecutableSize;
            if (length is > MaximumExecutableBytes)
            {
                throw new UpdateException("更新包超过允许的最大尺寸。");
            }

            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var output = new FileStream(
                partialPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            var buffer = new byte[81920];
            long received = 0;
            var lastPercentage = -1;
            progress?.Report(new UpdateProgress(UpdateProgressStage.Downloading,
                length is > 0 ? 0 : null));
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
                    var percentage = (int)Math.Clamp(received * 100 / length.Value, 0, 100);
                    if (percentage != lastPercentage)
                    {
                        lastPercentage = percentage;
                        progress?.Report(new UpdateProgress(
                            UpdateProgressStage.Downloading, percentage));
                    }
                }
            }

            await output.FlushAsync(cancellationToken);
            output.Close();

            progress?.Report(new UpdateProgress(UpdateProgressStage.Verifying));
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
        var release = ParseInstallableRelease(document.RootElement);
        return release with
        {
            Changelog =
            [
                new ReleaseNoteEntry(
                    release.Version,
                    release.Tag,
                    release.Name,
                    release.Notes,
                    release.PageUrl)
            ]
        };
    }

    internal static UpdateRelease? ParseReleaseList(string json, Version currentVersion)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new UpdateException("GitHub Release 列表格式无效。");
        }

        var candidates = new List<(Version Version, JsonElement Element)>();
        foreach (var element in document.RootElement.EnumerateArray())
        {
            if (ReadBoolean(element, "draft") || ReadBoolean(element, "prerelease") ||
                !element.TryGetProperty("tag_name", out var tagElement) ||
                !TryParseVersion(tagElement.GetString() ?? string.Empty, out var version) ||
                !IsNewerVersion(version, currentVersion))
            {
                continue;
            }

            candidates.Add((version, element.Clone()));
        }

        var ordered = candidates
            .GroupBy(candidate => NormalizeVersion(candidate.Version))
            .Select(group => group.First())
            .OrderByDescending(candidate => NormalizeVersion(candidate.Version))
            .ToArray();
        if (ordered.Length == 0)
        {
            return null;
        }

        var latest = ParseInstallableRelease(ordered[0].Element);
        var changelog = ordered
            .Select(candidate => ParseReleaseNote(candidate.Element, candidate.Version))
            .ToArray();
        return latest with
        {
            Notes = ReleaseNotesFormatter.CombineMarkdown(changelog, latest.Notes),
            Changelog = changelog
        };
    }

    internal static UpdateRelease? ParseReleaseFeed(string xml, Version currentVersion)
    {
        XDocument document;
        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };
            using var stringReader = new StringReader(xml);
            using var reader = XmlReader.Create(stringReader, settings);
            document = XDocument.Load(reader);
        }
        catch (Exception ex) when (ex is XmlException or InvalidOperationException)
        {
            throw new UpdateException("GitHub Release Feed 格式无效。", ex);
        }

        XNamespace atom = "http://www.w3.org/2005/Atom";
        var notes = new List<ReleaseNoteEntry>();
        foreach (var entry in document.Root?.Elements(atom + "entry") ?? [])
        {
            var pageValue = entry.Elements(atom + "link")
                .FirstOrDefault(link =>
                    string.Equals((string?)link.Attribute("rel"), "alternate",
                        StringComparison.OrdinalIgnoreCase))?
                .Attribute("href")?.Value;
            if (!Uri.TryCreate(pageValue, UriKind.Absolute, out var pageUrl))
            {
                continue;
            }

            string tag;
            try
            {
                tag = ParseTagFromReleasePage(pageUrl);
            }
            catch (UpdateException)
            {
                continue;
            }

            if (!TryParseVersion(tag, out var version) ||
                IsPrereleaseTag(tag) ||
                !IsNewerVersion(version, currentVersion))
            {
                continue;
            }

            var title = entry.Element(atom + "title")?.Value?.Trim();
            var html = entry.Element(atom + "content")?.Value;
            notes.Add(new ReleaseNoteEntry(
                version,
                tag,
                string.IsNullOrWhiteSpace(title) ? $"UsageTray {tag}" : title,
                ReleaseNotesFormatter.FromGitHubHtml(html),
                pageUrl));
        }

        var ordered = notes
            .GroupBy(note => NormalizeVersion(note.Version))
            .Select(group => group.First())
            .OrderByDescending(note => NormalizeVersion(note.Version))
            .ToArray();
        if (ordered.Length == 0)
        {
            return null;
        }

        var latest = ordered[0];
        var downloadRoot =
            $"https://github.com/{RepositoryOwner}/{RepositoryName}/releases/latest/download/";
        var release = new UpdateRelease(
            latest.Version,
            latest.Tag,
            latest.Name,
            latest.Notes,
            latest.PageUrl,
            new Uri(downloadRoot + ExecutableAssetName),
            new Uri(downloadRoot + ChecksumAssetName),
            null)
        {
            Changelog = ordered
        };
        return release with
        {
            Notes = ReleaseNotesFormatter.CombineMarkdown(ordered, latest.Notes)
        };
    }

    private static UpdateRelease ParseInstallableRelease(JsonElement root)
    {
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

    private static ReleaseNoteEntry ParseReleaseNote(JsonElement root, Version version)
    {
        var tag = root.GetProperty("tag_name").GetString() ?? version.ToString(3);
        var name = root.TryGetProperty("name", out var nameElement)
            ? nameElement.GetString() ?? tag
            : tag;
        var notes = root.TryGetProperty("body", out var bodyElement)
            ? bodyElement.GetString() ?? string.Empty
            : string.Empty;
        var pageUrl = ReadHttpsUri(
            root.GetProperty("html_url").GetString(), "Release 页面");
        return new ReleaseNoteEntry(version, tag, name, notes, pageUrl);
    }

    private static bool ReadBoolean(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) &&
        value.ValueKind == JsonValueKind.True;

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

    private static bool IsPrereleaseTag(string tag)
    {
        var normalized = tag.Trim().TrimStart('v', 'V');
        return normalized.Contains('-', StringComparison.Ordinal);
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

    public UpdateException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
