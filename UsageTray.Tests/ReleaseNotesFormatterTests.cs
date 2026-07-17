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
}
