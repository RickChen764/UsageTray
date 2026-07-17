using System.Text.RegularExpressions;

namespace UsageTray.Services;

internal static partial class ReleaseNotesFormatter
{
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

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ExcessBlankLinesRegex();

    [GeneratedRegex(
        @"^\*{0,2}Full Changelog\*{0,2}\s*[:：]\s*https?://\S+\s*$",
        RegexOptions.IgnoreCase)]
    private static partial Regex GeneratedChangelogOnlyRegex();
}
