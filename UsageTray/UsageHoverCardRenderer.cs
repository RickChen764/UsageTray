using System.Drawing.Drawing2D;

namespace UsageTray;

internal static class UsageHoverCardRenderer
{
    private const int LogicalWidth = 450;
    private static readonly Color Background = Color.FromArgb(24, 27, 33);
    private static readonly Color Surface = Color.FromArgb(31, 35, 43);
    private static readonly Color Border = Color.FromArgb(54, 60, 71);
    private static readonly Color PrimaryText = Color.FromArgb(244, 246, 250);
    private static readonly Color SecondaryText = Color.FromArgb(174, 181, 193);
    private static readonly Color MutedText = Color.FromArgb(119, 128, 143);
    private static readonly Color CostAccent = Color.FromArgb(255, 200, 95);

    public static Size Measure(HoverCardContent content, int dpi)
    {
        var height = 11 + 21 + 43;
        if (!string.IsNullOrWhiteSpace(content.Message))
        {
            height += 5 + 33;
        }

        if (content.Today.Count > 0)
        {
            height += 6 + 17 + 32;
        }

        if (content.TokenBreakdown.Count > 0)
        {
            height += 4 + 26;
        }

        if (content.Performance.Count > 0)
        {
            height += 3 + 26;
        }

        if (content.AllTime.Count > 0)
        {
            height += 6 + 17 + 28;
        }

        if (content.Models.Count > 0)
        {
            height += 6 + 17 + 12 + content.Models.Count * 20;
        }

        height += 6 + 1 + 5;
        if (!string.IsNullOrWhiteSpace(content.UpdatedText))
        {
            height += 13;
        }

        height += 14 + 8;
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

        using var titleFont = CreateFont(12, FontStyle.Bold, dpi);
        using var badgeFont = CreateFont(10, FontStyle.Regular, dpi);
        using var heroFont = CreateFont(22, FontStyle.Bold, dpi);
        using var sectionFont = CreateFont(11, FontStyle.Bold, dpi);
        using var labelFont = CreateFont(10, FontStyle.Regular, dpi);
        using var valueFont = CreateFont(12, FontStyle.Bold, dpi);
        using var bodyFont = CreateFont(11, FontStyle.Regular, dpi);
        using var bodyBoldFont = CreateFont(11, FontStyle.Bold, dpi);
        using var smallFont = CreateFont(10, FontStyle.Regular, dpi);

        var padding = Scale(12, dpi);
        var contentWidth = bounds.Width - padding * 2;
        var y = Scale(11, dpi);

        DrawText(graphics, content.AppName, titleFont,
            new Rectangle(padding, y, contentWidth, Scale(18, dpi)), PrimaryText);
        if (!string.IsNullOrWhiteSpace(content.Badge))
        {
            DrawBadge(graphics, content.Badge, badgeFont,
                new Rectangle(padding, y, contentWidth, Scale(18, dpi)), dpi);
        }

        y += Scale(20, dpi);
        DrawText(graphics, content.PrimaryValue, heroFont,
            new Rectangle(padding, y, contentWidth, Scale(28, dpi)), content.AccentColor);
        y += Scale(27, dpi);
        if (!string.IsNullOrWhiteSpace(content.PrimaryCaption))
        {
            DrawText(graphics, content.PrimaryCaption, smallFont,
                new Rectangle(padding, y, contentWidth, Scale(14, dpi)), SecondaryText);
        }

        y += Scale(16, dpi);

        if (!string.IsNullOrWhiteSpace(content.Message))
        {
            y += Scale(5, dpi);
            var messageBounds = new Rectangle(padding, y, contentWidth, Scale(33, dpi));
            FillRoundedRectangle(graphics, messageBounds, Scale(5, dpi), Surface);
            DrawText(graphics, content.Message, bodyFont,
                Rectangle.Inflate(messageBounds, -Scale(8, dpi), -Scale(4, dpi)),
                SecondaryText, TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix);
            y += messageBounds.Height;
        }

        if (content.Today.Count > 0)
        {
            y += Scale(6, dpi);
            DrawSectionHeader(graphics, "今日", "服务端统计", sectionFont, smallFont,
                padding, y, contentWidth, dpi);
            y += Scale(17, dpi);
            DrawMetricRow(graphics, content.Today, padding, y, contentWidth,
                Scale(32, dpi), labelFont, valueFont, dpi, emphasizeFirst: true);
            y += Scale(32, dpi);
        }

        if (content.TokenBreakdown.Count > 0)
        {
            y += Scale(4, dpi);
            DrawCompactStrip(graphics, "Token", content.TokenBreakdown,
                padding, y, contentWidth, labelFont, bodyBoldFont, dpi);
            y += Scale(26, dpi);
        }

        if (content.Performance.Count > 0)
        {
            y += Scale(3, dpi);
            DrawCompactStrip(graphics, "性能", content.Performance,
                padding, y, contentWidth, labelFont, bodyBoldFont, dpi);
            y += Scale(26, dpi);
        }

        if (content.AllTime.Count > 0)
        {
            y += Scale(6, dpi);
            DrawSectionHeader(graphics, "累计", null, sectionFont, smallFont,
                padding, y, contentWidth, dpi);
            y += Scale(17, dpi);
            DrawMetricRow(graphics, content.AllTime, padding, y, contentWidth,
                Scale(28, dpi), labelFont, bodyBoldFont, dpi, emphasizeFirst: false);
            y += Scale(28, dpi);
        }

        if (content.Models.Count > 0)
        {
            y += Scale(6, dpi);
            DrawSectionHeader(graphics, "模型 Top 3", null, sectionFont, smallFont,
                padding, y, contentWidth, dpi);
            y += Scale(17, dpi);
            DrawModelTable(graphics, content.Models, padding, y, contentWidth,
                smallFont, bodyFont, bodyBoldFont, dpi);
            y += Scale(12 + content.Models.Count * 20, dpi);
        }

        y += Scale(6, dpi);
        using (var divider = new Pen(Border))
        {
            graphics.DrawLine(divider, padding, y, bounds.Width - padding, y);
        }

        y += Scale(5, dpi);
        if (!string.IsNullOrWhiteSpace(content.UpdatedText))
        {
            DrawText(graphics, content.UpdatedText, smallFont,
                new Rectangle(padding, y, contentWidth, Scale(13, dpi)), MutedText);
            y += Scale(13, dpi);
        }

        DrawText(graphics, content.HintText, smallFont,
            new Rectangle(padding, y, contentWidth, Scale(14, dpi)), SecondaryText);
    }

    private static void DrawBadge(
        Graphics graphics,
        string text,
        Font font,
        Rectangle rowBounds,
        int dpi)
    {
        var horizontalPadding = Scale(7, dpi);
        var measured = TextRenderer.MeasureText(graphics, text, font,
            new Size(Scale(185, dpi), rowBounds.Height),
            TextFormatFlags.SingleLine | TextFormatFlags.NoPadding |
            TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        var width = Math.Min(Scale(185, dpi), measured.Width + horizontalPadding * 2);
        var bounds = new Rectangle(rowBounds.Right - width, rowBounds.Top,
            width, rowBounds.Height);
        FillRoundedRectangle(graphics, bounds, bounds.Height / 2, Surface);
        DrawText(graphics, text, font, Rectangle.Inflate(bounds, -horizontalPadding, 0),
            SecondaryText, CenteredTextFlags);
    }

    private static void DrawSectionHeader(
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
                new Rectangle(x, y, width, Scale(15, dpi)), MutedText, RightTextFlags);
        }
    }

    private static void DrawMetricRow(
        Graphics graphics,
        IReadOnlyList<HoverCardMetric> metrics,
        int x,
        int y,
        int width,
        int height,
        Font labelFont,
        Font valueFont,
        int dpi,
        bool emphasizeFirst)
    {
        var bounds = new Rectangle(x, y, width, height);
        FillRoundedRectangle(graphics, bounds, Scale(5, dpi), Surface);
        var itemWidth = width / metrics.Count;
        for (var index = 0; index < metrics.Count; index++)
        {
            var left = x + index * itemWidth;
            var actualWidth = index == metrics.Count - 1 ? x + width - left : itemWidth;
            if (index > 0)
            {
                using var divider = new Pen(Border);
                graphics.DrawLine(divider, left, y + Scale(5, dpi),
                    left, y + height - Scale(5, dpi));
            }

            var labelWidth = Math.Min(Scale(43, dpi), actualWidth / 2);
            DrawText(graphics, metrics[index].Label, labelFont,
                new Rectangle(left + Scale(7, dpi), y, labelWidth, height), MutedText);
            DrawText(graphics, metrics[index].Value, valueFont,
                new Rectangle(left + labelWidth, y, actualWidth - labelWidth - Scale(7, dpi), height),
                emphasizeFirst && index == 0 ? CostAccent : PrimaryText,
                RightTextFlags);
        }
    }

    private static void DrawCompactStrip(
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
        var height = Scale(26, dpi);
        var titleWidth = Scale(46, dpi);
        DrawText(graphics, title, labelFont,
            new Rectangle(x, y, titleWidth, height), SecondaryText);
        var valueWidth = width - titleWidth;
        var itemWidth = valueWidth / metrics.Count;
        for (var index = 0; index < metrics.Count; index++)
        {
            var left = x + titleWidth + index * itemWidth;
            var actualWidth = index == metrics.Count - 1
                ? x + width - left
                : itemWidth;
            DrawText(graphics, metrics[index].Label, labelFont,
                new Rectangle(left, y, actualWidth, Scale(12, dpi)), MutedText,
                CenteredTextFlags);
            DrawText(graphics, metrics[index].Value, valueFont,
                new Rectangle(left, y + Scale(12, dpi), actualWidth, Scale(14, dpi)),
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
        var tokenWidth = Scale(100, dpi);
        var costWidth = Scale(68, dpi);
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
            var rowHeight = Scale(20, dpi);
            if (index % 2 == 0)
            {
                FillRoundedRectangle(graphics,
                    new Rectangle(x, y, width, rowHeight), Scale(3, dpi), Surface);
            }

            var inset = Scale(5, dpi);
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
                                TextFormatFlags.NoPadding |
                                TextFormatFlags.NoPrefix) =>
        TextRenderer.DrawText(graphics, text, font, bounds, color, flags);

    private static readonly TextFormatFlags CenteredTextFlags =
        TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter |
        TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis |
        TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;

    private static readonly TextFormatFlags RightTextFlags =
        TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter |
        TextFormatFlags.Right | TextFormatFlags.EndEllipsis |
        TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;

    private static Font CreateFont(float logicalPixels, FontStyle style, int dpi) =>
        new("Microsoft YaHei UI", logicalPixels * dpi / 96F, style, GraphicsUnit.Pixel);

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
