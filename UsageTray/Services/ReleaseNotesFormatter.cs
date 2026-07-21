using System.Net;
using System.Text.RegularExpressions;

namespace UsageTray.Services;

internal static partial class ReleaseNotesFormatter
{
    public static string FromGitHubHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var text = html.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        text = HtmlListItemRegex().Replace(text, "\n- ");
        text = HtmlBlockEndRegex().Replace(text, "\n\n");
        text = HtmlBreakRegex().Replace(text, "\n");
        text = HtmlAnchorRegex().Replace(text, match =>
            $"[{StripHtml(match.Groups[2].Value)}]({WebUtility.HtmlDecode(match.Groups[1].Value)})");
        text = HtmlTagRegex().Replace(text, string.Empty);
        text = WebUtility.HtmlDecode(text);
        return ExcessBlankLinesRegex().Replace(text.Trim(), "\n\n");
    }

    public static string Combine(
        IReadOnlyList<Models.ReleaseNoteEntry> entries,
        string fallbackNotes)
    {
        if (entries.Count == 0)
        {
            return ToPlainText(fallbackNotes);
        }

        var sections = entries.Select(entry =>
        {
            var body = ToPlainText(entry.Notes);
            return $"{entry.Tag}\n{body}";
        });
        return string.Join("\n\n────────────────────────\n\n", sections);
    }

    public static string CombineMarkdown(
        IReadOnlyList<Models.ReleaseNoteEntry> entries,
        string fallbackNotes)
    {
        if (entries.Count == 0)
        {
            return fallbackNotes;
        }

        return string.Join("\n\n---\n\n", entries.Select(entry =>
            $"## {entry.Tag}\n\n{entry.Notes}"));
    }

    public static string ToPlainText(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return "该版本未提供更新说明。";
        }

        if (GeneratedChangelogOnlyRegex().IsMatch(markdown.Trim()))
        {
            return "该版本未提供详细更新说明。";
        }

        var text = markdown.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        text = MarkdownLinkRegex().Replace(text, match =>
            $"{match.Groups[1].Value}：{match.Groups[2].Value}");
        text = AutoLinkRegex().Replace(text, "$1");
        text = HeadingRegex().Replace(text, string.Empty);
        text = BulletRegex().Replace(text, "• ");
        text = BoldRegex().Replace(text, "$1");
        text = UnderlineBoldRegex().Replace(text, "$1");
        text = StrikeRegex().Replace(text, "$1");
        text = InlineCodeRegex().Replace(text, "$1");
        text = BlockQuoteRegex().Replace(text, string.Empty);
        text = HtmlTagRegex().Replace(text, string.Empty);
        text = Regex.Replace(text, "(?i)Full Changelog(?=\\s*[:：])", "完整变更记录");
        text = ExcessBlankLinesRegex().Replace(text.Trim(), "\n\n");
        return text;
    }

    [GeneratedRegex(@"\[([^\]\r\n]+)\]\((https?://[^)\s]+)\)", RegexOptions.IgnoreCase)]
    private static partial Regex MarkdownLinkRegex();

    [GeneratedRegex(@"<(https?://[^>\s]+)>", RegexOptions.IgnoreCase)]
    private static partial Regex AutoLinkRegex();

    [GeneratedRegex(@"(?m)^\s{0,3}#{1,6}\s+")]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"(?m)^[ \t]*[-*+][ \t]+")]
    private static partial Regex BulletRegex();

    [GeneratedRegex(@"\*\*([^*\r\n]+)\*\*")]
    private static partial Regex BoldRegex();

    [GeneratedRegex(@"__([^_\r\n]+)__")]
    private static partial Regex UnderlineBoldRegex();

    [GeneratedRegex(@"~~([^~\r\n]+)~~")]
    private static partial Regex StrikeRegex();

    [GeneratedRegex(@"`([^`\r\n]+)`")]
    private static partial Regex InlineCodeRegex();

    [GeneratedRegex(@"(?m)^[ \t]*>[ \t]?")]
    private static partial Regex BlockQuoteRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"(?is)<li\b[^>]*>")]
    private static partial Regex HtmlListItemRegex();

    [GeneratedRegex(@"(?is)</(?:li|p|div|ul|ol|h[1-6])\s*>")]
    private static partial Regex HtmlBlockEndRegex();

    [GeneratedRegex(@"(?is)<br\s*/?>")]
    private static partial Regex HtmlBreakRegex();

    [GeneratedRegex(@"(?is)<a\b[^>]*href\s*=\s*[""'](https?://[^""']+)[""'][^>]*>(.*?)</a>")]
    private static partial Regex HtmlAnchorRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ExcessBlankLinesRegex();

    [GeneratedRegex(
        @"^\*{0,2}Full Changelog\*{0,2}\s*[:：]\s*https?://\S+\s*$",
        RegexOptions.IgnoreCase)]
    private static partial Regex GeneratedChangelogOnlyRegex();

    private static string StripHtml(string value) =>
        WebUtility.HtmlDecode(HtmlTagRegex().Replace(value, string.Empty)).Trim();
}
