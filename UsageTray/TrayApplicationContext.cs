using System.Globalization;
using System.Text;
using UsageTray.Models;
using UsageTray.Services;

namespace UsageTray;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly SettingsStore _settingsStore = new();
    private readonly UsageApiClient _apiClient = new();
    private readonly UpdateService _updateService = new();
    private readonly NotifyIcon _notifyIcon;
    private readonly TaskbarToolbarForm _toolbar;
    private readonly System.Windows.Forms.Timer _refreshTimer = new();
    private readonly System.Windows.Forms.Timer _updateTimer = new();
    private readonly ToolStripMenuItem _statusItem = new("余额：尚未读取") { Enabled = false };
    private readonly ToolStripMenuItem _todayCostItem = new("今日费用：--") { Enabled = false };
    private readonly ToolStripMenuItem _todayTokensItem = new("今日 Token：--") { Enabled = false };
    private readonly ToolStripMenuItem _updatedItem = new("最后更新：--") { Enabled = false };
    private readonly ToolStripMenuItem _refreshItem = new("立即刷新");
    private readonly ToolStripMenuItem _versionItem = new() { Enabled = false };
    private readonly ToolStripMenuItem _updateItem = new("检查更新…");
    private Icon? _currentIcon;
    private AppSettings _settings;
    private bool _refreshing;
    private bool _checkingUpdate;
    private bool _showingUpdatePrompt;
    private bool _installingUpdate;
    private string? _lastError;
    private UpdateRelease? _availableUpdate;

    public TrayApplicationContext()
    {
        _settings = _settingsStore.Load(out var warning);
        var menu = new ContextMenuStrip();
        menu.Items.Add(_statusItem);
        menu.Items.Add(_todayCostItem);
        menu.Items.Add(_todayTokensItem);
        menu.Items.Add(_updatedItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_refreshItem);
        menu.Items.Add("设置…", null, (_, _) => ShowSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_versionItem);
        menu.Items.Add(_updateItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => ExitThread());

        _refreshItem.Click += async (_, _) => await RefreshUsageAsync(showSuccessNotification: true);
        _refreshTimer.Tick += async (_, _) => await RefreshUsageAsync(showSuccessNotification: false);
        _updateItem.Click += async (_, _) => await UpdateMenuItem_ClickAsync();
        _updateTimer.Interval = checked(6 * 60 * 60 * 1000);
        _updateTimer.Tick += async (_, _) => await CheckForUpdatesAsync(notifyWhenCurrent: false);
        _versionItem.Text = $"当前版本：v{UpdateService.CurrentVersion.ToString(3)}";

        _notifyIcon = new NotifyIcon
        {
            Visible = false,
            ContextMenuStrip = menu,
            Text = "UsageTray - 等待配置"
        };
        _notifyIcon.DoubleClick += (_, _) => ShowSettings();
        UpdateIcon(null, TrayIconState.Loading);

        _toolbar = new TaskbarToolbarForm(menu);
        _toolbar.RefreshRequested += async (_, _) => await RefreshUsageAsync(showSuccessNotification: false);
        _toolbar.SettingsRequested += (_, _) => ShowSettings();
        _toolbar.AttachmentChanged += (_, attached) => _notifyIcon.Visible = !attached;
        _toolbar.SetDisplay("等待配置", "UsageTray - 等待配置", Color.FromArgb(124, 132, 145));
        _toolbar.Show();
        _notifyIcon.Visible = !_toolbar.IsAttached;

        ConfigureTimer();
        _updateTimer.Start();
        _ = CheckForUpdatesAfterStartupAsync();

        if (!string.IsNullOrWhiteSpace(warning))
        {
            ShowBalloon("配置读取失败", warning, ToolTipIcon.Warning);
        }

        if (!_settings.IsConfigured)
        {
            ShowSettings();
        }
        else
        {
            _ = RefreshUsageAsync(showSuccessNotification: false);
        }
    }

    private async Task RefreshUsageAsync(bool showSuccessNotification)
    {
        if (_refreshing || !_settings.IsConfigured)
        {
            return;
        }

        _refreshing = true;
        _refreshItem.Enabled = false;
        try
        {
            _toolbar.SetDisplay("刷新中…", "UsageTray - 正在读取用量", Color.FromArgb(69, 139, 226));
            UpdateIcon(null, TrayIconState.Loading);
            var usage = await _apiClient.GetUsageAsync(_settings.BaseUrl, _settings.GetApiKey());
            ApplyUsage(usage);
            _lastError = null;

            if (showSuccessNotification)
            {
                ShowBalloon("刷新成功", $"剩余 {FormatAmount(usage.Remaining)} {usage.Unit}",
                    ToolTipIcon.Info);
            }
        }
        catch (Exception ex)
        {
            ApplyError(ex.Message);
        }
        finally
        {
            _refreshing = false;
            _refreshItem.Enabled = true;
        }
    }

    private void ApplyUsage(UsageResult usage)
    {
        var amount = FormatAmount(usage.Remaining);
        var validity = usage.IsValid ? string.Empty : "（已停用）";
        _statusItem.Text = $"余额：{amount} {usage.Unit}{validity}";
        ApplyTodayMenuItems(usage);
        _updatedItem.Text = $"最后更新：{DateTime.Now:yyyy-MM-dd HH:mm:ss}";

        var details = usage.Total is not null
            ? $"剩余 {amount} / 总量 {FormatAmount(usage.Total.Value)} {usage.Unit}"
            : $"剩余 {amount} {usage.Unit}";
        var display = usage.Total is not null
            ? $"余 {FormatCompactAmount(usage.Remaining, usage.Unit)} / {FormatCompactAmount(usage.Total.Value, usage.Unit)}"
            : $"余额 {FormatCompactAmount(usage.Remaining, usage.Unit)}";
        var statusColor = !usage.IsValid
            ? Color.FromArgb(240, 161, 69)
            : usage.Total is > 0 && usage.Remaining / usage.Total.Value <= 0.1m
                ? Color.FromArgb(235, 83, 83)
                : usage.Total is > 0 && usage.Remaining / usage.Total.Value <= 0.25m
                    ? Color.FromArgb(240, 161, 69)
                    : Color.FromArgb(67, 190, 112);
        _toolbar.SetDisplay(display, BuildHoverText(usage, details, validity),
            statusColor);
        SetTooltip($"UsageTray - {details}{validity}");
        UpdateIcon(usage.Remaining, usage.IsValid ? TrayIconState.Healthy : TrayIconState.Invalid);
    }

    private void ApplyError(string message)
    {
        _statusItem.Text = "余额：读取失败";
        _todayCostItem.Text = "今日费用：--";
        _todayTokensItem.Text = "今日 Token：--";
        _updatedItem.Text = $"最后尝试：{DateTime.Now:yyyy-MM-dd HH:mm:ss}";
        SetTooltip("UsageTray - 读取失败");
        _toolbar.SetDisplay("读取失败", $"UsageTray\n{message}\n\n左键重试 · 双击设置 · 右键菜单",
            Color.FromArgb(235, 83, 83));
        UpdateIcon(null, TrayIconState.Error);

        if (!string.Equals(_lastError, message, StringComparison.Ordinal))
        {
            _lastError = message;
            ShowBalloon("用量读取失败", message, ToolTipIcon.Error);
        }
    }

    private void ShowSettings()
    {
        using var form = new SettingsForm(_settings);
        if (form.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        try
        {
            StartupManager.SetEnabled(form.Result.StartWithWindows);
            _settingsStore.Save(form.Result);
            _settings = form.Result;
            ConfigureTimer();
            _ = RefreshUsageAsync(showSuccessNotification: true);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存配置失败：{ex.Message}", "UsageTray",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ConfigureTimer()
    {
        _refreshTimer.Stop();
        _refreshTimer.Interval = checked(Math.Clamp(_settings.RefreshMinutes, 1, 1440) * 60 * 1000);
        if (_settings.IsConfigured)
        {
            _refreshTimer.Start();
        }
    }

    private async Task CheckForUpdatesAfterStartupAsync()
    {
        await Task.Delay(TimeSpan.FromSeconds(12));
        await CheckForUpdatesAsync(notifyWhenCurrent: false);
    }

    private async Task CheckForUpdatesAsync(bool notifyWhenCurrent)
    {
        if (_checkingUpdate || _installingUpdate)
        {
            return;
        }

        _checkingUpdate = true;
        _updateItem.Enabled = false;
        _updateItem.Text = "正在检查更新…";
        try
        {
            var release = await _updateService.CheckAsync();
            if (release is not null &&
                UpdateService.IsNewerVersion(release.Version, UpdateService.CurrentVersion))
            {
                _availableUpdate = release;
                _updateItem.Text = $"发现新版本 v{release.Version.ToString(3)}（点击更新）";
                _updateItem.Enabled = true;
                ShowBalloon("UsageTray 有新版本",
                    $"v{release.Version.ToString(3)} 已发布。右键工具条并选择更新。",
                    ToolTipIcon.Info);
                return;
            }

            _availableUpdate = null;
            _updateItem.Text = "检查更新…";
            if (notifyWhenCurrent)
            {
                MessageBox.Show($"当前已是最新版本 v{UpdateService.CurrentVersion.ToString(3)}。",
                    "UsageTray 更新", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            _updateItem.Text = "检查更新失败（点击重试）";
            if (notifyWhenCurrent)
            {
                MessageBox.Show($"检查更新失败。\n\n{ex.Message}", "UsageTray 更新",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        finally
        {
            _checkingUpdate = false;
            if (!_installingUpdate)
            {
                _updateItem.Enabled = true;
            }
        }
    }

    private async Task UpdateMenuItem_ClickAsync()
    {
        if (_showingUpdatePrompt || _installingUpdate)
        {
            return;
        }

        if (_availableUpdate is null)
        {
            await CheckForUpdatesAsync(notifyWhenCurrent: true);
            return;
        }

        _showingUpdatePrompt = true;
        _updateItem.Enabled = false;
        try
        {
            var release = _availableUpdate;
            var notes = string.IsNullOrWhiteSpace(release.Notes)
                ? "该版本未提供更新说明。"
                : release.Notes.Trim();
            if (notes.Length > 900)
            {
                notes = notes[..900] + "…";
            }

            var size = release.ExecutableSize is > 0
                ? $"\n下载大小：{FormatFileSize(release.ExecutableSize.Value)}"
                : string.Empty;
            var choice = MessageBox.Show(
                $"发现新版本 v{release.Version.ToString(3)}。{size}\n\n{notes}\n\n" +
                "下载完成后，UsageTray 会自动重启。是否立即更新？",
                "UsageTray 更新", MessageBoxButtons.YesNo, MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button1);
            if (choice == DialogResult.Yes)
            {
                await InstallUpdateAsync(release);
            }
        }
        finally
        {
            _showingUpdatePrompt = false;
            if (!_installingUpdate)
            {
                _updateItem.Enabled = true;
            }
        }
    }

    private async Task InstallUpdateAsync(UpdateRelease release)
    {
        _installingUpdate = true;
        _updateItem.Enabled = false;
        _updateItem.Text = "正在下载更新…";
        try
        {
            var progress = new Progress<int>(value =>
                _updateItem.Text = $"正在下载更新… {value}%");
            var downloaded = await _updateService.DownloadAndVerifyAsync(release, progress);
            _updateItem.Text = "正在安装并重启…";
            UpdateInstaller.Launch(downloaded.ExecutablePath, downloaded.Sha256);
            ExitThread();
        }
        catch (Exception ex)
        {
            _installingUpdate = false;
            _updateItem.Enabled = true;
            _updateItem.Text = $"更新 v{release.Version.ToString(3)}（点击重试）";
            MessageBox.Show($"更新失败，当前版本未被替换。\n\n{ex.Message}",
                "UsageTray 更新", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void UpdateIcon(decimal? remaining, TrayIconState state)
    {
        var next = TrayIconRenderer.Create(remaining, state);
        _notifyIcon.Icon = next;
        _currentIcon?.Dispose();
        _currentIcon = next;
    }

    private void SetTooltip(string text)
    {
        _notifyIcon.Text = text.Length <= 63 ? text : text[..62] + "…";
    }

    private void ShowBalloon(string title, string text, ToolTipIcon icon)
    {
        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = text.Length <= 240 ? text : text[..239] + "…";
        _notifyIcon.BalloonTipIcon = icon;
        _notifyIcon.ShowBalloonTip(4000);
    }

    private static string FormatAmount(decimal amount) =>
        amount.ToString("0.##", CultureInfo.CurrentCulture);

    private void ApplyTodayMenuItems(UsageResult usage)
    {
        var today = usage.Statistics?.Today;
        if (today is null)
        {
            _todayCostItem.Text = "今日费用：服务未提供";
            _todayTokensItem.Text = "今日 Token：服务未提供";
            return;
        }

        var cost = today.ActualCost ?? today.Cost;
        var costText = cost is not null
            ? FormatCompactAmount(cost.Value, usage.Unit)
            : "--";
        var requests = today.Requests is not null
            ? $" · {today.Requests.Value:N0} 次请求"
            : string.Empty;
        _todayCostItem.Text = $"今日费用：{costText}{requests}";
        _todayTokensItem.Text = today.TotalTokens is not null
            ? $"今日 Token：{FormatCompactCount(today.TotalTokens.Value)}"
            : "今日 Token：--";
    }

    private static string BuildHoverText(UsageResult usage, string balanceDetails, string validity)
    {
        var builder = new StringBuilder();
        var header = string.IsNullOrWhiteSpace(usage.PlanName)
            ? "UsageTray"
            : $"UsageTray · {usage.PlanName}";
        builder.AppendLine(header);
        builder.AppendLine($"{balanceDetails}{validity}");
        if (!string.IsNullOrWhiteSpace(usage.Mode))
        {
            builder.AppendLine($"计费模式：{usage.Mode}");
        }

        var statistics = usage.Statistics;
        if (statistics?.Today is { } today)
        {
            builder.AppendLine();
            builder.AppendLine("今日累计（服务端口径）");
            var cost = today.ActualCost ?? today.Cost;
            var costText = cost is null ? "--" : FormatCompactAmount(cost.Value, usage.Unit);
            var requestText = today.Requests is null ? "--" : today.Requests.Value.ToString("N0");
            builder.AppendLine($"费用 {costText}  ·  请求 {requestText} 次");
            if (today.TotalTokens is not null)
            {
                builder.AppendLine($"Token {today.TotalTokens.Value:N0}（{FormatCompactCount(today.TotalTokens.Value)}）");
            }

            var tokenParts = new List<string>();
            AddTokenPart(tokenParts, "输入", today.InputTokens);
            AddTokenPart(tokenParts, "输出", today.OutputTokens);
            AddTokenPart(tokenParts, "缓存读取", today.CacheReadTokens);
            AddTokenPart(tokenParts, "缓存写入", today.CacheCreationTokens);
            if (tokenParts.Count > 0)
            {
                builder.AppendLine(string.Join("  ·  ", tokenParts));
            }
        }

        if (statistics is not null)
        {
            var rateParts = new List<string>();
            if (statistics.TokensPerMinute is not null)
            {
                rateParts.Add($"TPM {FormatCompactDecimal(statistics.TokensPerMinute.Value)}");
            }

            if (statistics.RequestsPerMinute is not null)
            {
                rateParts.Add($"RPM {statistics.RequestsPerMinute.Value:0.##}");
            }

            if (statistics.AverageDurationMs is not null)
            {
                rateParts.Add($"平均响应 {FormatDuration(statistics.AverageDurationMs.Value)}");
            }

            if (rateParts.Count > 0)
            {
                builder.AppendLine(string.Join("  ·  ", rateParts));
            }

            if (statistics.AllTime is { } allTime)
            {
                builder.AppendLine();
                var allTimeCost = allTime.ActualCost ?? allTime.Cost;
                var allTimeCostText = allTimeCost is null
                    ? "--"
                    : FormatCompactAmount(allTimeCost.Value, usage.Unit);
                var allTimeRequests = allTime.Requests is null ? "--" : allTime.Requests.Value.ToString("N0");
                var allTimeTokens = allTime.TotalTokens is null
                    ? "--"
                    : FormatCompactCount(allTime.TotalTokens.Value);
                builder.AppendLine($"累计：{allTimeCostText}  ·  {allTimeRequests} 次请求  ·  {allTimeTokens} Token");
            }

            var models = statistics.Models
                .OrderByDescending(model => model.Cost ?? 0)
                .ThenByDescending(model => model.TotalTokens ?? 0)
                .Take(3)
                .ToArray();
            if (models.Length > 0)
            {
                builder.AppendLine("模型累计（Top 3）");
                foreach (var model in models)
                {
                    var tokens = model.TotalTokens is null ? "--" : FormatCompactCount(model.TotalTokens.Value);
                    var cost = model.Cost is null ? "--" : FormatCompactAmount(model.Cost.Value, usage.Unit);
                    builder.AppendLine($"{model.Model}：{tokens} Token  ·  {cost}");
                }
            }
        }

        builder.AppendLine();
        builder.AppendLine($"最后更新：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        builder.Append("左键刷新 · 双击设置 · 右键菜单");
        return builder.ToString();
    }

    private static void AddTokenPart(List<string> parts, string name, long? value)
    {
        if (value is not null)
        {
            parts.Add($"{name} {FormatCompactCount(value.Value)}");
        }
    }

    private static string FormatCompactCount(long value) => FormatCompactDecimal(value);

    private static string FormatCompactDecimal(decimal value)
    {
        var absolute = Math.Abs(value);
        return absolute switch
        {
            >= 1_000_000_000m => $"{value / 1_000_000_000m:0.##}B",
            >= 1_000_000m => $"{value / 1_000_000m:0.##}M",
            >= 1_000m => $"{value / 1_000m:0.##}K",
            _ => value.ToString("0.##", CultureInfo.CurrentCulture)
        };
    }

    private static string FormatDuration(decimal milliseconds) => milliseconds switch
    {
        >= 60_000m => $"{milliseconds / 60_000m:0.#} 分钟",
        >= 1_000m => $"{milliseconds / 1_000m:0.#} 秒",
        _ => $"{milliseconds:0} ms"
    };

    private static string FormatFileSize(long bytes) => bytes switch
    {
        >= 1024L * 1024 * 1024 => $"{bytes / (1024d * 1024 * 1024):0.##} GB",
        >= 1024L * 1024 => $"{bytes / (1024d * 1024):0.##} MB",
        >= 1024L => $"{bytes / 1024d:0.##} KB",
        _ => $"{bytes} B"
    };

    private static string FormatCompactAmount(decimal amount, string unit)
    {
        var number = amount.ToString("0.##", CultureInfo.CurrentCulture);
        return unit.Trim().ToUpperInvariant() switch
        {
            "USD" => "$" + number,
            "CNY" or "RMB" => "¥" + number,
            "EUR" => "€" + number,
            "GBP" => "£" + number,
            _ => number + " " + unit
        };
    }

    protected override void ExitThreadCore()
    {
        _refreshTimer.Stop();
        _refreshTimer.Dispose();
        _updateTimer.Stop();
        _updateTimer.Dispose();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _currentIcon?.Dispose();
        _toolbar.Close();
        _toolbar.Dispose();
        _apiClient.Dispose();
        _updateService.Dispose();
        base.ExitThreadCore();
    }
}
