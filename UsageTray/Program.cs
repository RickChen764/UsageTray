using System.Threading;
using UsageTray.Services;

namespace UsageTray;

internal static class Program
{
    private const string MutexName = @"Local\UsageTray.SingleInstance";

    [STAThread]
    private static void Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        if (UpdateInstaller.TryRunApplyMode(args))
        {
            return;
        }

#if DEBUG
        if (args.Length == 1 && string.Equals(args[0], "--preview-update-dialog", StringComparison.Ordinal))
        {
            var preview = new Models.UpdateRelease(
                new Version(1, 1, 6),
                "v1.1.6",
                "UsageTray v1.1.6",
                "## 本次更新\n\n- 修复更新说明中的 Markdown 标记和长链接排版\n- 更新窗口直接展示版本变更详情\n- 移除跳转 GitHub 的额外入口\n- 保留滚动阅读和 DPI 自适应布局",
                new Uri("https://github.com/RickChen764/UsageTray/releases/tag/v1.1.6"),
                new Uri("https://github.com/RickChen764/UsageTray/releases/download/v1.1.6/UsageTray-win-x64.exe"),
                new Uri("https://github.com/RickChen764/UsageTray/releases/download/v1.1.6/UsageTray-win-x64.exe.sha256"),
                71_600_000);
            Application.Run(new UpdatePromptForm(preview));
            return;
        }
#endif

        using var mutex = new Mutex(initiallyOwned: true, MutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            MessageBox.Show("UsageTray 已在运行，请查看任务栏通知区域。", "UsageTray",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        UpdateInstaller.ScheduleCleanupIfNeeded(args);
        Application.Run(new TrayApplicationContext());
    }
}
