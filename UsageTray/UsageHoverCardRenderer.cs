using System.Drawing.Drawing2D;

namespace UsageTray;

internal static class UsageHoverCardRenderer
{
    private const int LogicalWidth = 470;
    private static readonly Color Background = Color.FromArgb(22, 25, 31);
    private static readonly Color Surface = Color.FromArgb(31, 35, 43);
    private static readonly Color SurfaceStrong = Color.FromArgb(38, 43, 52);
    private static readonly Color Border = Color.FromArgb(53, 59, 70);
    private static readonly Color PrimaryText = Color.FromArgb(244, 246, 250);
    private static readonly Color SecondaryText = Color.FromArgb(174, 181, 193);
    private static readonly Color MutedText = Color.FromArgb(119, 128, 143);
    private static readonly Color CostAccent = Color.FromArgb(255, 200, 95);

    public static Size Measure(HoverCardContent content, int dpi)
    {
        var height = 14 + 18 + PrimaryBlockHeight(content);
        if (!string.IsNullOrWhiteSpace(content.Message))
        {
            height += 8 + 42;
        }

        if (content.Today.Count > 0)
        {
            height += 9 + 15 + 4 + 46;
        }

        if (content.TokenBreakdown.Count > 0)
        {
            height += 6 + 36;
        }

        if (content.Performance.Count > 0)
        {
            height += 6 + 36;
        }

        if (content.AllTime.Count > 0)
        {
            height += 9 + 15 + 4 + 38;
        }

        if (content.Models.Count > 0)
        {
            height += 9 + 15 + 4 + 12 + content.Models.Count * 23;
        }

        height += 8 + 1 + 7;
        if (!string.IsNullOrWhiteSpace(content.UpdatedText))
        {
            height += 14 + 2;
        }

        height += 14 + 11;
        return new Size(Scale(LogicalWidth, dpi), Scale(height, dpi));
    }

    public static void Draw(
        Graphics graphics,
        Rectangle bounds,
        int dpi,
        HoverCardContent content)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        graphics.Clear(Background);

        using var borderPen = new Pen(Border, Math.Max(1F, dpi / 96F));
        graphics.DrawRectangle(borderPen, 0, 0,
            Math.Max(0, bounds.Width - 1), Math.Max(0, bounds.Height - 1));

        using var appFont = CreateFont(11, FontStyle.Bold, dpi);
        using var badgeFont = CreateFont(10, FontStyle.Regular, dpi);
        using var labelFont = CreateFont(10, FontStyle.Regular, dpi);
        using var primaryFont = CreateFont(22, FontStyle.Bold, dpi);
        using var sectionFont = CreateFont(12, FontStyle.Bold, dpi);
        using var metricLabelFont = CreateFont(10, FontStyle.Regular, dpi);
        using var metricValueFont = CreateFont(14, FontStyle.Bold, dpi);
        using var bodyFont = CreateFont(12, FontStyle.Regular, dpi);
        using var bodyBoldFont = CreateFont(12, FontStyle.Bold, dpi);
        using var smallFont = CreateFont(10, FontStyle.Regular, dpi);

        var padding = Scale(14, dpi);
        var contentWidth = bounds.Width - padding * 2;
        var y = padding;

        DrawText(graphics, content.AppName, appFont,
            new Rectangle(padding, y, contentWidth, Scale(18, dpi)), PrimaryText);
        if (!string.IsNullOrWhiteSpace(content.Badge))
        {
            DrawBadge(graphics, content.Badge, badgeFont,
                new Rectangle(padding, y, contentWidth, Scale(18, dpi)), dpi);
        }

        y += Scale(18, dpi);
        DrawText(graphics, content.PrimaryLabel, labelFont,
            new Rectangle(padding, y, contentWidth, Scale(14, dpi)), SecondaryText);
        y += Scale(13, dpi);
        DrawText(graphics, content.PrimaryValue, primaryFont,
            new Rectangle(padding, y, contentWidth, Scale(28, dpi)), content.AccentColor);
        y += Scale(27, dpi);
        if (!string.IsNullOrWhiteSpace(content.PrimaryCaption))
        {
            DrawText(graphics, content.PrimaryCaption, smallFont,
                new Rectangle(padding, y, contentWidth, Scale(14, dpi)), MutedText);
            y += Scale(14, dpi);
        }

        if (!string.IsNullOrWhiteSpace(content.Message))
        {
            y += Scale(8, dpi);
            var messageBounds = new Rectangle(
                padding, y, contentWidth, Scale(42, dpi));
            FillRoundedRectangle(graphics, messageBounds, Scale(7, dpi), Surface);
            DrawText(graphics, content.Message, bodyFont,
                Rectangle.Inflate(messageBounds, -Scale(11, dpi), -Scale(7, dpi)),
                SecondaryText, TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
            y += messageBounds.Height;
        }

        if (content.Today.Count > 0)
        {
            y += Scale(9, dpi);
            DrawSectionTitle(graphics, "今日用量", "服务端统计", sectionFont,
                smallFont, padding, y, contentWidth, dpi);
            y += Scale(19, dpi);
            DrawMetricTiles(graphics, content.Today, padding, y,
                contentWidth, Scale(46, dpi), metricLabelFont, metricValueFont, dpi);
            y += Scale(46, dpi);
        }

        if (content.TokenBreakdown.Count > 0)
        {
            y += Scale(6, dpi);
            DrawMetricStrip(graphics, "Token 构成", content.TokenBreakdown,
                padding, y, contentWidth, metricLabelFont, bodyBoldFont, dpi);
            y += Scale(36, dpi);
        }

        if (content.Performance.Count > 0)
        {
            y += Scale(6, dpi);
            DrawMetricStrip(graphics, "实时性能", content.Performance,
                padding, y, contentWidth, metricLabelFont, bodyBoldFont, dpi);
            y += Scale(36, dpi);
        }

        if (content.AllTime.Count > 0)
        {
            y += Scale(9, dpi);
            DrawSectionTitle(graphics, "累计用量", null, sectionFont,
                smallFont, padding, y, contentWidth, dpi);
            y += Scale(19, dpi);
            DrawSummaryStrip(graphics, content.AllTime, padding, y,
                contentWidth, metricLabelFont, bodyBoldFont, dpi);
            y += Scale(38, dpi);
        }

        if (content.Models.Count > 0)
        {
            y += Scale(9, dpi);
            DrawSectionTitle(graphics, "模型消耗", "Top 3", sectionFont,
                smallFont, padding, y, contentWidth, dpi);
            y += Scale(19, dpi);
            DrawModelTable(graphics, content.Models, padding, y,
                contentWidth, smallFont, bodyFont, bodyBoldFont, dpi);
            y += Scale(12 + content.Models.Count * 23, dpi);
        }

        y += Scale(8, dpi);
        using (var divider = new Pen(Border))
        {
            graphics.DrawLine(divider, padding, y, bounds.Width - padding, y);
        }

        y += Scale(7, dpi);
        if (!string.IsNullOrWhiteSpace(content.UpdatedText))
        {
            DrawText(graphics, content.UpdatedText, smallFont,
                new Rectangle(padding, y, contentWidth, Scale(14, dpi)), MutedText);
            y += Scale(16, dpi);
        }

        DrawText(graphics, content.HintText, smallFont,
            new Rectangle(padding, y, contentWidth, Scale(14, dpi)), SecondaryText);
    }

    private static int PrimaryBlockHeight(HoverCardContent content) =>
        string.IsNullOrWhiteSpace(content.PrimaryCaption) ? 40 : 54;

    private static void DrawBadge(
        Graphics graphics,
        string text,
        Font font,
        Rectangle rowBounds,
        int dpi)
    {
        var horizontalPadding = Scale(8, dpi);
        var measured = TextRenderer.MeasureText(graphics, text, font,
            new Size(Scale(230, dpi), rowBounds.Height),
            TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        var width = Math.Min(Scale(230, dpi), measured.Width + horizontalPadding * 2);
        var bounds = new Rectangle(rowBounds.Right - width, rowBounds.Top,
            width, rowBounds.Height);
        FillRoundedRectangle(graphics, bounds, bounds.Height / 2, SurfaceStrong);
        DrawText(graphics, text, font,
            Rectangle.Inflate(bounds, -horizontalPadding, 0), SecondaryText,
            TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis | TextFormatFlags.HorizontalCenter);
    }

    private static void DrawSectionTitle(
        Graphics graphics,
        string title,
        string? caption,
        Font titleFont,
        Font captionFont,
        int x,
        int y,
        int width,
        int dpi)
    {
        DrawText(graphics, title, titleFont,
            new Rectangle(x, y, width, Scale(15, dpi)), PrimaryText);
        if (!string.IsNullOrWhiteSpace(caption))
        {
            DrawText(graphics, caption, captionFont,
                new Rectangle(x, y, width, Scale(15, dpi)), MutedText,
                TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter |
                TextFormatFlags.Right | TextFormatFlags.NoPadding);
        }
    }

    private static void DrawMetricTiles(
        Graphics graphics,
        IReadOnlyList<HoverCardMetric> metrics,
        int x,
        int y,
        int width,
        int height,
        Font labelFont,
        Font valueFont,
        int dpi)
    {
        var gap = Scale(7, dpi);
        var tileWidth = (width - gap * (metrics.Count - 1)) / metrics.Count;
        for (var index = 0; index < metrics.Count; index++)
        {
            var metric = metrics[index];
            var left = x + index * (tileWidth + gap);
            var actualWidth = index == metrics.Count - 1 ? x + width - left : tileWidth;
            var bounds = new Rectangle(left, y, actualWidth, height);
            FillRoundedRectangle(graphics, bounds, Scale(7, dpi),
                metric.Emphasized ? SurfaceStrong : Surface);
            using var outline = new Pen(metric.Emphasized
                ? Color.FromArgb(92, 79, 53)
                : Border);
            DrawRoundedRectangle(graphics, bounds, Scale(7, dpi), outline);

            var inner = Rectangle.Inflate(bounds, -Scale(9, dpi), -Scale(5, dpi));
            DrawText(graphics, metric.Label, labelFont,
                new Rectangle(inner.Left, inner.Top, inner.Width, Scale(13, dpi)), MutedText);
            DrawText(graphics, metric.Value, valueFont,
                new Rectangle(inner.Left, inner.Top + Scale(14, dpi),
                    inner.Width, Scale(20, dpi)),
                metric.Emphasized ? CostAccent : PrimaryText);
        }
    }

    private static void DrawMetricStrip(
        Graphics graphics,
        string title,
        IReadOnlyList<HoverCardMetric> metrics,
        int x,
        int y,
        int width,
        Font labelFont,
        Font valueFont,
        int dpi)
    {
        var height = Scale(36, dpi);
        var bounds = new Rectangle(x, y, width, height);
        FillRoundedRectangle(graphics, bounds, Scale(6, dpi), Surface);
        var titleWidth = Scale(73, dpi);
        DrawText(graphics, title, labelFont,
            new Rectangle(x + Scale(9, dpi), y, titleWidth - Scale(9, dpi), height),
            SecondaryText);

        var dividerX = x + titleWidth;
        using (var divider = new Pen(Border))
        {
            graphics.DrawLine(divider, dividerX, y + Scale(7, dpi),
                dividerX, y + height - Scale(7, dpi));
        }

        var metricWidth = (width - titleWidth) / metrics.Count;
        for (var index = 0; index < metrics.Count; index++)
        {
            var left = dividerX + index * metricWidth;
            var actualWidth = index == metrics.Count - 1
                ? x + width - left
                : metricWidth;
            DrawText(graphics, metrics[index].Label, labelFont,
                new Rectangle(left, y + Scale(3, dpi), actualWidth, Scale(13, dpi)),
                MutedText, CenteredTextFlags);
            DrawText(graphics, metrics[index].Value, valueFont,
                new Rectangle(left, y + Scale(16, dpi), actualWidth, Scale(17, dpi)),
                PrimaryText, CenteredTextFlags);
        }
    }

    private static void DrawSummaryStrip(
        Graphics graphics,
        IReadOnlyList<HoverCardMetric> metrics,
        int x,
        int y,
        int width,
        Font labelFont,
        Font valueFont,
        int dpi)
    {
        var height = Scale(38, dpi);
        var bounds = new Rectangle(x, y, width, height);
        FillRoundedRectangle(graphics, bounds, Scale(7, dpi), Surface);
        var metricWidth = width / metrics.Count;
        for (var index = 0; index < metrics.Count; index++)
        {
            var left = x + index * metricWidth;
            var actualWidth = index == metrics.Count - 1 ? x + width - left : metricWidth;
            if (index > 0)
            {
                using var divider = new Pen(Border);
                graphics.DrawLine(divider, left, y + Scale(7, dpi),
                    left, y + height - Scale(7, dpi));
            }

            DrawText(graphics, metrics[index].Label, labelFont,
                new Rectangle(left, y + Scale(3, dpi), actualWidth, Scale(13, dpi)),
                MutedText, CenteredTextFlags);
            DrawText(graphics, metrics[index].Value, valueFont,
                new Rectangle(left, y + Scale(16, dpi), actualWidth, Scale(17, dpi)),
                PrimaryText, CenteredTextFlags);
        }
    }

    private static void DrawModelTable(
        Graphics graphics,
        IReadOnlyList<HoverCardModelRow> models,
        int x,
        int y,
        int width,
        Font headerFont,
        Font bodyFont,
        Font bodyBoldFont,
        int dpi)
    {
        var tokenWidth = Scale(112, dpi);
        var costWidth = Scale(76, dpi);
        var modelWidth = width - tokenWidth - costWidth;
        DrawText(graphics, "模型", headerFont,
            new Rectangle(x, y, modelWidth, Scale(12, dpi)), MutedText);
        DrawText(graphics, "Token", headerFont,
            new Rectangle(x + modelWidth, y, tokenWidth, Scale(12, dpi)), MutedText,
            RightTextFlags);
        DrawText(graphics, "费用", headerFont,
            new Rectangle(x + modelWidth + tokenWidth, y, costWidth, Scale(12, dpi)),
            MutedText, RightTextFlags);
        y += Scale(12, dpi);

        for (var index = 0; index < models.Count; index++)
        {
            var row = models[index];
            var rowHeight = Scale(23, dpi);
            if (index % 2 == 0)
            {
                FillRoundedRectangle(graphics,
                    new Rectangle(x, y, width, rowHeight), Scale(4, dpi), Surface);
            }

            var inset = Scale(7, dpi);
            DrawText(graphics, row.Model, index == 0 ? bodyBoldFont : bodyFont,
                new Rectangle(x + inset, y, modelWidth - inset, rowHeight), PrimaryText);
            DrawText(graphics, row.Tokens, bodyFont,
                new Rectangle(x + modelWidth, y, tokenWidth - inset, rowHeight),
                SecondaryText, RightTextFlags);
            DrawText(graphics, row.Cost, bodyFont,
                new Rectangle(x + modelWidth + tokenWidth, y, costWidth - inset, rowHeight),
                index == 0 ? CostAccent : SecondaryText, RightTextFlags);
            y += rowHeight;
        }
    }

    private static void DrawText(
        Graphics graphics,
        string text,
        Font font,
        Rectangle bounds,
        Color color,
        TextFormatFlags flags = TextFormatFlags.SingleLine |
                                TextFormatFlags.VerticalCenter |
                                TextFormatFlags.EndEllipsis |
                                TextFormatFlags.NoPadding) =>
        TextRenderer.DrawText(graphics, text, font, bounds, color, flags);

    private static readonly TextFormatFlags CenteredTextFlags =
        TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter |
        TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis |
        TextFormatFlags.NoPadding;

    private static readonly TextFormatFlags RightTextFlags =
        TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter |
        TextFormatFlags.Right | TextFormatFlags.EndEllipsis |
        TextFormatFlags.NoPadding;

    private static Font CreateFont(float logicalPixels, FontStyle style, int dpi) =>
        new("Microsoft YaHei UI", logicalPixels * dpi / 96F,
            style, GraphicsUnit.Pixel);

    private static int Scale(int value, int dpi) =>
        (int)Math.Round(value * dpi / 96F, MidpointRounding.AwayFromZero);

    private static void FillRoundedRectangle(
        Graphics graphics,
        Rectangle rectangle,
        int radius,
        Color color)
    {
        using var path = RoundedRectangle(rectangle, radius);
        using var brush = new SolidBrush(color);
        graphics.FillPath(brush, path);
    }

    private static void DrawRoundedRectangle(
        Graphics graphics,
        Rectangle rectangle,
        int radius,
        Pen pen)
    {
        var adjusted = new Rectangle(rectangle.X, rectangle.Y,
            Math.Max(1, rectangle.Width - 1), Math.Max(1, rectangle.Height - 1));
        using var path = RoundedRectangle(adjusted, radius);
        graphics.DrawPath(pen, path);
    }

    private static GraphicsPath RoundedRectangle(Rectangle rectangle, int radius)
    {
        var safeRadius = Math.Max(1, Math.Min(radius,
            Math.Min(rectangle.Width, rectangle.Height) / 2));
        var diameter = safeRadius * 2;
        var path = new GraphicsPath();
        path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
