using UsageTray.Models;
using UsageTray.Services;

namespace UsageTray;

internal sealed class UpdatePromptForm : Form
{
    private readonly RichTextBox _notesTextBox = new();

    public UpdatePromptForm(UpdateRelease release)
    {
        Text = "UsageTray 更新";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(700, 475);
        MinimumSize = new Size(620, 430);
        Font = new Font("Microsoft YaHei UI", 9F);

        BuildInterface(release);
    }

    private void BuildInterface(UpdateRelease release)
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 22, 24, 18),
            ColumnCount = 2,
            RowCount = 6
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 54));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var icon = new PictureBox
        {
            Image = SystemIcons.Information.ToBitmap(),
            SizeMode = PictureBoxSizeMode.CenterImage,
            Size = new Size(46, 46),
            Margin = new Padding(0, 0, 8, 8)
        };
        root.Controls.Add(icon, 0, 0);

        var heading = new Label
        {
            AutoSize = true,
            Text = $"发现新版本 v{release.Version}",
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(0, 5, 0, 3)
        };
        root.Controls.Add(heading, 1, 0);

        var metadataParts = new List<string> { $"当前版本 v{UpdateService.CurrentVersion.ToString(3)}" };
        if (release.ExecutableSize is > 0)
        {
            metadataParts.Add($"下载大小 {FormatFileSize(release.ExecutableSize.Value)}");
        }

        var metadata = new Label
        {
            AutoSize = true,
            Text = string.Join("  ·  ", metadataParts),
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 0, 0, 16)
        };
        root.Controls.Add(metadata, 1, 1);

        var notesLabel = new Label
        {
            AutoSize = true,
            Text = "更新说明",
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 7)
        };
        root.Controls.Add(notesLabel, 1, 2);

        _notesTextBox.Dock = DockStyle.Fill;
        _notesTextBox.ReadOnly = true;
        _notesTextBox.DetectUrls = false;
        _notesTextBox.WordWrap = true;
        _notesTextBox.ScrollBars = RichTextBoxScrollBars.Vertical;
        _notesTextBox.BackColor = SystemColors.Window;
        _notesTextBox.BorderStyle = BorderStyle.FixedSingle;
        _notesTextBox.Text = ReleaseNotesFormatter.ToPlainText(release.Notes);
        _notesTextBox.Margin = new Padding(0, 0, 0, 12);
        root.Controls.Add(_notesTextBox, 1, 3);

        var restartHint = new Label
        {
            AutoSize = true,
            Text = "下载并校验完成后，UsageTray 会自动重启。",
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 0, 0, 14),
        };
        root.Controls.Add(restartHint, 1, 4);

        var actionPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Margin = new Padding(0),
            Padding = new Padding(0, 4, 0, 0)
        };
        var updateButton = new Button
        {
            Text = "立即更新",
            Size = new Size(112, 36),
            DialogResult = DialogResult.Yes,
            Margin = new Padding(8, 0, 0, 0)
        };
        var laterButton = new Button
        {
            Text = "稍后再说",
            Size = new Size(112, 36),
            DialogResult = DialogResult.No,
            Margin = new Padding(8, 0, 0, 0)
        };
        actionPanel.Controls.Add(updateButton);
        actionPanel.Controls.Add(laterButton);
        root.Controls.Add(actionPanel, 1, 5);

        AcceptButton = updateButton;
        CancelButton = laterButton;
        Controls.Add(root);

        Shown += (_, _) =>
        {
            _notesTextBox.SelectionStart = 0;
            _notesTextBox.SelectionLength = 0;
            updateButton.Focus();
        };
    }

    private static string FormatFileSize(long bytes) => bytes switch
    {
        >= 1024L * 1024 * 1024 => $"{bytes / (1024d * 1024 * 1024):0.##} GB",
        >= 1024L * 1024 => $"{bytes / (1024d * 1024):0.##} MB",
        >= 1024L => $"{bytes / 1024d:0.##} KB",
        _ => $"{bytes} B"
    };
}
