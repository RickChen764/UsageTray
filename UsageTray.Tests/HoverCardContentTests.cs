using System.Drawing;
using UsageTray;
using Xunit;

namespace UsageTray.Tests;

public sealed class HoverCardContentTests
{
    [Fact]
    public void ToPlainText_PreservesImportantMetricsForAccessibility()
    {
        var content = new HoverCardContent(
            "UsageTray",
            "钱包余额",
            "9526.79 USD",
            "剩余比例 95.3%",
            "计费 · unrestricted",
            Color.Green,
            [new HoverCardMetric("费用", "$175.37", true)],
            [new HoverCardMetric("输入", "8.39M")],
            [new HoverCardMetric("RPM", "13")],
            [new HoverCardMetric("Token", "537.09M")],
            [new HoverCardModelRow("gpt-5.6-sol", "536.33M", "$472.45")],
            "更新于 21:00:51",
            "左键刷新");

        var text = content.ToPlainText();

        Assert.Contains("钱包余额：9526.79 USD", text);
        Assert.Contains("今日：费用 $175.37", text);
        Assert.Contains("gpt-5.6-sol：536.33M · $472.45", text);
        Assert.Contains("左键刷新", text);
    }

    [Fact]
    public void Measure_GrowsProportionallyWithDpi()
    {
        var content = HoverCardContent.CreateStatus(
            "刷新中…", "正在读取最新用量。", Color.Blue);

        var normal = UsageHoverCardRenderer.Measure(content, 96);
        var highDpi = UsageHoverCardRenderer.Measure(content, 144);

        Assert.Equal(420, normal.Width);
        Assert.Equal(630, highDpi.Width);
        Assert.InRange(highDpi.Height, normal.Height * 1.5 - 1,
            normal.Height * 1.5 + 1);
    }
}
