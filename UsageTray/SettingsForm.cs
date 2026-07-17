using UsageTray.Services;

namespace UsageTray;

internal sealed class SettingsForm : Form
{
    private readonly TextBox _baseUrlTextBox = new();
    private readonly TextBox _apiKeyTextBox = new();
    private readonly NumericUpDown _refreshMinutesInput = new();
    private readonly CheckBox _showApiKeyCheckBox = new();
    private readonly CheckBox _startWithWindowsCheckBox = new();
    private readonly Label _endpointPreviewLabel = new();
    private readonly Button _testButton = new();
    private readonly Button _saveButton = new();

    public AppSettings Result { get; private set; }

    public SettingsForm(AppSettings settings)
    {
        Result = settings;
        Text = "UsageTray 设置";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(620, 390);
        MinimumSize = new Size(580, 420);
        Font = new Font("Microsoft YaHei UI", 9F);

        BuildInterface();
        LoadSettings(settings);
    }

    private void BuildInterface()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 22, 24, 18),
            ColumnCount = 2,
            RowCount = 8,
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _baseUrlTextBox.Dock = DockStyle.Fill;
        _baseUrlTextBox.Margin = new Padding(3, 1, 3, 8);
        _baseUrlTextBox.PlaceholderText = "https://example.com";
        _baseUrlTextBox.TextChanged += (_, _) => UpdateEndpointPreview();
        AddLabel(layout, "Base URL", 0);
        layout.Controls.Add(_baseUrlTextBox, 1, 0);

        _endpointPreviewLabel.Dock = DockStyle.Fill;
        _endpointPreviewLabel.ForeColor = SystemColors.GrayText;
        _endpointPreviewLabel.AutoEllipsis = true;
        _endpointPreviewLabel.TextAlign = ContentAlignment.MiddleLeft;
        _endpointPreviewLabel.MinimumSize = new Size(0, 24);
        _endpointPreviewLabel.Margin = new Padding(3, 0, 3, 10);
        layout.Controls.Add(_endpointPreviewLabel, 1, 1);

        _apiKeyTextBox.Dock = DockStyle.Fill;
        _apiKeyTextBox.Margin = new Padding(3, 1, 3, 8);
        _apiKeyTextBox.UseSystemPasswordChar = true;
        _apiKeyTextBox.PlaceholderText = "API Key";
        AddLabel(layout, "API Key", 2);
        layout.Controls.Add(_apiKeyTextBox, 1, 2);

        _showApiKeyCheckBox.Text = "显示 API Key";
        _showApiKeyCheckBox.AutoSize = true;
        _showApiKeyCheckBox.Margin = new Padding(3, 0, 3, 12);
        _showApiKeyCheckBox.CheckedChanged += (_, _) =>
            _apiKeyTextBox.UseSystemPasswordChar = !_showApiKeyCheckBox.Checked;
        layout.Controls.Add(_showApiKeyCheckBox, 1, 3);

        _refreshMinutesInput.Minimum = 1;
        _refreshMinutesInput.Maximum = 1440;
        _refreshMinutesInput.Width = 112;
        AddLabel(layout, "刷新间隔", 4);
        var refreshPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 10)
        };
        refreshPanel.Controls.Add(_refreshMinutesInput);
        refreshPanel.Controls.Add(new Label
        {
            Text = "分钟",
            AutoSize = true,
            Margin = new Padding(6, 5, 0, 0)
        });
        layout.Controls.Add(refreshPanel, 1, 4);

        _startWithWindowsCheckBox.Text = "登录 Windows 后自动启动";
        _startWithWindowsCheckBox.AutoSize = true;
        _startWithWindowsCheckBox.Margin = new Padding(3, 2, 3, 10);
        layout.Controls.Add(_startWithWindowsCheckBox, 1, 5);

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 10, 0, 0),
            Margin = new Padding(0)
        };
        _saveButton.Text = "保存";
        _saveButton.Size = new Size(96, 36);
        _saveButton.Margin = new Padding(8, 0, 0, 0);
        _saveButton.Click += SaveButton_Click;
        var cancelButton = new Button
        {
            Text = "取消",
            Size = new Size(96, 36),
            Margin = new Padding(8, 0, 0, 0),
            DialogResult = DialogResult.Cancel
        };
        _testButton.Text = "测试连接";
        _testButton.Size = new Size(110, 36);
        _testButton.Margin = new Padding(8, 0, 0, 0);
        _testButton.Click += TestButton_Click;
        buttons.Controls.Add(_saveButton);
        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(_testButton);
        layout.SetColumnSpan(buttons, 2);
        layout.Controls.Add(buttons, 0, 7);

        AcceptButton = _saveButton;
        CancelButton = cancelButton;
        Controls.Add(layout);
    }

    private static void AddLabel(TableLayoutPanel layout, string text, int row)
    {
        layout.Controls.Add(new Label
        {
            Text = text,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 4, 3, 8),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, row);
    }

    private void LoadSettings(AppSettings settings)
    {
        _baseUrlTextBox.Text = settings.BaseUrl;
        _refreshMinutesInput.Value = Math.Clamp(settings.RefreshMinutes, 1, 1440);
        _startWithWindowsCheckBox.Checked = settings.StartWithWindows;

        try
        {
            _apiKeyTextBox.Text = settings.GetApiKey();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"已保存的 API Key 无法解密，请重新输入。\n\n{ex.Message}",
                "UsageTray", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void UpdateEndpointPreview()
    {
        try
        {
            _endpointPreviewLabel.Text = "请求地址：" + UsageApiClient.BuildEndpoint(_baseUrlTextBox.Text);
        }
        catch
        {
            _endpointPreviewLabel.Text = "请求地址：Base URL + /v1/usage";
        }
    }

    private bool ValidateInputs()
    {
        try
        {
            _ = UsageApiClient.BuildEndpoint(_baseUrlTextBox.Text);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "配置有误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _baseUrlTextBox.Focus();
            return false;
        }

        if (string.IsNullOrWhiteSpace(_apiKeyTextBox.Text))
        {
            MessageBox.Show(this, "请输入 API Key。", "配置有误",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _apiKeyTextBox.Focus();
            return false;
        }

        return true;
    }

    private async void TestButton_Click(object? sender, EventArgs e)
    {
        if (!ValidateInputs())
        {
            return;
        }

        SetBusy(true);
        try
        {
            using var client = new UsageApiClient();
            var usage = await client.GetUsageAsync(_baseUrlTextBox.Text, _apiKeyTextBox.Text);
            var validity = usage.IsValid ? "有效" : "已停用";
            MessageBox.Show(this,
                $"连接成功\n\n状态：{validity}\n剩余：{usage.Remaining:N2} {usage.Unit}",
                "UsageTray", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "测试失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SaveButton_Click(object? sender, EventArgs e)
    {
        if (!ValidateInputs())
        {
            return;
        }

        try
        {
            var settings = new AppSettings
            {
                BaseUrl = _baseUrlTextBox.Text.Trim().TrimEnd('/'),
                RefreshMinutes = (int)_refreshMinutesInput.Value,
                StartWithWindows = _startWithWindowsCheckBox.Checked
            };
            settings.SetApiKey(_apiKeyTextBox.Text);
            Result = settings;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"无法加密保存 API Key：{ex.Message}",
                "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SetBusy(bool busy)
    {
        UseWaitCursor = busy;
        _testButton.Enabled = !busy;
        _saveButton.Enabled = !busy;
    }
}
