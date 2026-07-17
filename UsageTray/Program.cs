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
