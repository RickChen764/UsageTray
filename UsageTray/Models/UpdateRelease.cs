namespace UsageTray.Models;

internal sealed record UpdateRelease(
    Version Version,
    string Tag,
    string Name,
    string Notes,
    Uri PageUrl,
    Uri ExecutableUrl,
    Uri ChecksumUrl,
    long? ExecutableSize)
{
    public IReadOnlyList<ReleaseNoteEntry> Changelog { get; init; } = [];
}

internal sealed record ReleaseNoteEntry(
    Version Version,
    string Tag,
    string Name,
    string Notes,
    Uri PageUrl);
