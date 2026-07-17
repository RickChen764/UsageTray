using System.Text;

namespace UsageTray;

internal sealed record HoverCardMetric(
    string Label,
    string Value,
    bool Emphasized = false);

internal sealed record HoverCardModelRow(
    string Model,
    string Tokens,
    string Cost);

internal sealed record HoverCardContent(
    string AppName,
    string PrimaryLabel,
    string PrimaryValue,
    string? PrimaryCaption,
    string? Badge,
    Color AccentColor,
    IReadOnlyList<HoverCardMetric> Today,
    IReadOnlyList<HoverCardMetric> TokenBreakdown,
    IReadOnlyList<HoverCardMetric> Performance,
    IReadOnlyList<HoverCardMetric> AllTime,
    IReadOnlyList<HoverCardModelRow> Models,
    string UpdatedText,
    string HintText,
    string? Message = null)
{
    public static HoverCardContent CreateStatus(
        string value,
        string message,
        Color accentColor,
        string updatedText = "",
        string hintText = "左键刷新 · 双击设置 · 右键菜单") => new(
            "UsageTray",
            "状态",
            value,
            null,
            null,
            accentColor,
            [],
            [],
            [],
            [],
            [],
            updatedText,
            hintText,
            message);

    public string ToPlainText()
    {
        var builder = new StringBuilder();
        builder.AppendLine(AppName);
        builder.AppendLine($"{PrimaryLabel}：{PrimaryValue}");
        if (!string.IsNullOrWhiteSpace(PrimaryCaption))
        {
            builder.AppendLine(PrimaryCaption);
        }

        if (!string.IsNullOrWhiteSpace(Badge))
        {
            builder.AppendLine(Badge);
        }

        if (!string.IsNullOrWhiteSpace(Message))
        {
            builder.AppendLine(Message);
        }

        AppendMetrics(builder, "今日", Today);
        AppendMetrics(builder, "Token", TokenBreakdown);
        AppendMetrics(builder, "性能", Performance);
        AppendMetrics(builder, "累计", AllTime);
        foreach (var model in Models)
        {
            builder.AppendLine($"{model.Model}：{model.Tokens} · {model.Cost}");
        }

        if (!string.IsNullOrWhiteSpace(UpdatedText))
        {
            builder.AppendLine(UpdatedText);
        }

        builder.Append(HintText);
        return builder.ToString();
    }

    private static void AppendMetrics(
        StringBuilder builder,
        string title,
        IReadOnlyList<HoverCardMetric> metrics)
    {
        if (metrics.Count == 0)
        {
            return;
        }

        builder.Append(title);
        builder.Append("：");
        builder.AppendLine(string.Join(" · ", metrics.Select(metric =>
            $"{metric.Label} {metric.Value}")));
    }
}
