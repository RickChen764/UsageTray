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
                new Version(1, 1, 11),
                "v1.1.11",
                "UsageTray v1.1.11",
                "## 本次更新\n\n- 汇总展示多个版本的更新说明",
                new Uri("https://github.com/RickChen764/UsageTray/releases/tag/v1.1.11"),
                new Uri("https://github.com/RickChen764/UsageTray/releases/download/v1.1.11/UsageTray-win-x64.exe"),
                new Uri("https://github.com/RickChen764/UsageTray/releases/download/v1.1.11/UsageTray-win-x64.exe.sha256"),
                71_600_000)
            {
                Changelog =
                [
                    new Models.ReleaseNoteEntry(
                        new Version(1, 1, 11), "v1.1.11", "UsageTray v1.1.11",
                        "## 本次更新\n\n- 汇总当前版本之后的所有更新说明\n- 增加 GitHub 完整日志入口",
                        new Uri("https://github.com/RickChen764/UsageTray/releases/tag/v1.1.11")),
                    new Models.ReleaseNoteEntry(
                        new Version(1, 1, 10), "v1.1.10", "UsageTray v1.1.10",
                        "## 本次更新\n\n- 放大 Hover 字体和面板",
                        new Uri("https://github.com/RickChen764/UsageTray/releases/tag/v1.1.10"))
                ]
            };
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
