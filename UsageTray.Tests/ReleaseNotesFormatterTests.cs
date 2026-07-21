using UsageTray.Services;
using Xunit;

namespace UsageTray.Tests;

public sealed class ReleaseNotesFormatterTests
{
    [Fact]
    public void ToPlainText_CleansGeneratedGithubChangelog()
    {
        const string markdown = "**Full Changelog**: https://github.com/example/repo/compare/v1...v2";

        Assert.Equal("该版本未提供详细更新说明。",
            ReleaseNotesFormatter.ToPlainText(markdown));
    }

    [Fact]
    public void ToPlainText_ConvertsHeadingsBulletsAndMarkdownLinks()
    {
        const string markdown = "# Changes\n\n- **Fixed** layout\n- [Details](https://example.com/release)";

        Assert.Equal(
            "Changes\n\n• Fixed layout\n• Details：https://example.com/release",
            ReleaseNotesFormatter.ToPlainText(markdown));
    }

    [Fact]
    public void ToPlainText_HandlesMissingNotes()
    {
        Assert.Equal("该版本未提供更新说明。", ReleaseNotesFormatter.ToPlainText(null));
    }

    [Fact]
    public void FromGitHubHtml_PreservesListStructureAndLinks()
    {
        const string html = "<h2>更新</h2><ul><li>修复布局</li><li><a href=\"https://example.com\">详情</a></li></ul>";

        var markdown = ReleaseNotesFormatter.FromGitHubHtml(html);

        Assert.Contains("- 修复布局", markdown);
        Assert.Contains("[详情](https://example.com)", markdown);
    }

    [Fact]
    public void Combine_ShowsAllVersionsNewestFirst()
    {
        var entries = new[]
        {
            new Models.ReleaseNoteEntry(new Version(1, 1, 10), "v1.1.10",
                "latest", "- 最新说明", new Uri("https://github.com/example/repo/releases/tag/v1.1.10")),
            new Models.ReleaseNoteEntry(new Version(1, 1, 9), "v1.1.9",
                "previous", "- 历史说明", new Uri("https://github.com/example/repo/releases/tag/v1.1.9"))
        };

        var text = ReleaseNotesFormatter.Combine(entries, string.Empty);

        Assert.True(text.IndexOf("v1.1.10", StringComparison.Ordinal) <
                    text.IndexOf("v1.1.9", StringComparison.Ordinal));
        Assert.Contains("• 最新说明", text);
        Assert.Contains("• 历史说明", text);
    }

    [Fact]
    public void CombineMarkdown_PreservesAllSectionsForReleaseModel()
    {
        var entries = new[]
        {
            new Models.ReleaseNoteEntry(new Version(2, 0), "v2.0", "v2", "- 新版", new Uri("https://github.com/example/repo/releases/tag/v2.0")),
            new Models.ReleaseNoteEntry(new Version(1, 9), "v1.9", "v1.9", "- 旧版", new Uri("https://github.com/example/repo/releases/tag/v1.9"))
        };

        var markdown = ReleaseNotesFormatter.CombineMarkdown(entries, string.Empty);

        Assert.Contains("## v2.0", markdown);
        Assert.Contains("## v1.9", markdown);
    }
}
