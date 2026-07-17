namespace UsageTray.Models;

internal sealed record UsageResult(
    bool IsValid,
    decimal Remaining,
    string Unit,
    decimal? Total,
    decimal? Used,
    string? PlanName,
    string? Mode,
    UsageStatistics? Statistics);

internal sealed record UsageStatistics(
    UsagePeriod? Today,
    UsagePeriod? AllTime,
    decimal? AverageDurationMs,
    decimal? RequestsPerMinute,
    decimal? TokensPerMinute,
    IReadOnlyList<ModelUsageStat> Models);

internal sealed record UsagePeriod(
    decimal? Cost,
    decimal? ActualCost,
    long? Requests,
    long? InputTokens,
    long? OutputTokens,
    long? CacheCreationTokens,
    long? CacheReadTokens,
    long? TotalTokens);

internal sealed record ModelUsageStat(
    string Model,
    decimal? Cost,
    long? Requests,
    long? InputTokens,
    long? OutputTokens,
    long? CacheCreationTokens,
    long? CacheReadTokens,
    long? TotalTokens);
