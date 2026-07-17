using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using UsageTray.Services;

namespace UsageTray;

internal sealed class TaskbarToolbarForm : Form
{
    private const int MinimumToolbarWidth = 150;
    private const int MaximumToolbarWidth = 440;
    private const float HorizontalFontSize = 11F;
    private const int VerticalToolbarHeight = 72;
    private const long WsChild = 0x40000000L;
    private const long WsPopup = 0x80000000L;
    private const int GwlStyle = -16;
    private const int GwlExStyle = -20;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;
    private const int SwpNoActivate = 0x0010;
    private const int SwpShowWindow = 0x0040;
    private static readonly IntPtr HwndTop = IntPtr.Zero;

    private readonly System.Windows.Forms.Timer _attachmentTimer = new() { Interval = 1500 };
    private readonly ToolTip _toolTip = new();
    private IntPtr _taskbarHandle;
    private Rectangle _lastTaskbarBounds;
    private Rectangle _lastTrayBounds;
    private string _displayText = "等待配置";
    private Color _statusColor = Color.FromArgb(124, 132, 145);
    private int _desiredToolbarWidth = MinimumToolbarWidth;
    private bool _hovered;
    private bool _pressed;

    public event EventHandler? RefreshRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler<bool>? AttachmentChanged;

    public bool IsAttached { get; private set; }

    public TaskbarToolbarForm(ContextMenuStrip contextMenu)
    {
        FormBorderStyle = FormBorderStyle.None;
        AutoScaleMode = AutoScaleMode.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(52, 57, 65);
        ContextMenuStrip = contextMenu;
        DoubleBuffered = true;
        Cursor = Cursors.Hand;

        _toolTip.AutoPopDelay = 20000;
        _toolTip.InitialDelay = 300;
        _toolTip.ReshowDelay = 100;
        _toolTip.ShowAlways = true;
        _toolTip.SetToolTip(this, "UsageTray");

        _attachmentTimer.Tick += (_, _) => AttachOrReposition();
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= WsExToolWindow | WsExNoActivate;
            return parameters;
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        AttachOrReposition(force: true);
        _attachmentTimer.Start();
    }

    public void SetDisplay(string text, string tooltip, Color statusColor)
    {
        _displayText = text;
        _statusColor = statusColor;
        _toolTip.SetToolTip(this, tooltip);
        UpdateDesiredToolbarWidth();
        AttachOrReposition(force: true);
        Invalidate();
    }

    public void AttachOrReposition(bool force = false)
    {
        var taskbar = FindWindow("Shell_TrayWnd", null);
        var tray = taskbar != IntPtr.Zero
            ? FindWindowEx(taskbar, IntPtr.Zero, "TrayNotifyWnd", null)
            : IntPtr.Zero;

        if (taskbar == IntPtr.Zero || tray == IntPtr.Zero ||
            !GetWindowRect(taskbar, out var taskbarRect) ||
            !GetWindowRect(tray, out var trayRect))
        {
            ChangeAttachmentState(false);
            return;
        }

        var taskbarBounds = taskbarRect.ToRectangle();
        var trayBounds = trayRect.ToRectangle();
        if (!force && taskbar == _taskbarHandle && IsAttached &&
            taskbarBounds == _lastTaskbarBounds && trayBounds == _lastTrayBounds &&
            GetParent(Handle) == taskbar)
        {
            return;
        }

        _taskbarHandle = taskbar;
        _lastTaskbarBounds = taskbarBounds;
        _lastTrayBounds = trayBounds;

        if (GetParent(Handle) != taskbar)
        {
            SetParent(Handle, taskbar);
            var style = GetWindowLongPtr(Handle, GwlStyle).ToInt64();
            style = (style | WsChild) & ~WsPopup;
            SetWindowLongPtr(Handle, GwlStyle, new IntPtr(style));

            var extendedStyle = GetWindowLongPtr(Handle, GwlExStyle).ToInt64();
            extendedStyle |= WsExToolWindow | WsExNoActivate;
            SetWindowLongPtr(Handle, GwlExStyle, new IntPtr(extendedStyle));
        }

        var horizontal = taskbarBounds.Width >= taskbarBounds.Height;
        int x;
        int y;
        int width;
        int height;

        if (horizontal)
        {
            width = Math.Min(_desiredToolbarWidth,
                Math.Max(80, trayBounds.Left - taskbarBounds.Left));
            var taskbarHeight = taskbarBounds.Height;
            height = taskbarHeight >= 36 ? 36 : Math.Max(1, taskbarHeight - 2);
            x = Math.Max(0, trayBounds.Left - taskbarBounds.Left - width);
            y = Math.Max(0, (taskbarHeight - height) / 2);
        }
        else
        {
            width = taskbarBounds.Width;
            height = Math.Min(VerticalToolbarHeight, Math.Max(48, trayBounds.Top - taskbarBounds.Top));
            x = 0;
            y = Math.Max(0, trayBounds.Top - taskbarBounds.Top - height);
        }

        var attached = GetParent(Handle) == taskbar &&
                       SetWindowPos(Handle, HwndTop, x, y, width, height,
                           SwpNoActivate | SwpShowWindow);
        if (attached)
        {
            ApplyRoundedWindowRegion(width, height);
        }

        ChangeAttachmentState(attached);
        Invalidate();
    }

    private void ChangeAttachmentState(bool attached)
    {
        if (IsAttached == attached)
        {
            return;
        }

        IsAttached = attached;
        AttachmentChanged?.Invoke(this, attached);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var bounds = ClientRectangle;
        var vertical = bounds.Height > bounds.Width;
        var pill = Rectangle.Inflate(bounds, -1, -1);
        if (pill.Width <= 0 || pill.Height <= 0)
        {
            return;
        }

        var fillColor = _pressed
            ? Color.FromArgb(82, 87, 96)
            : _hovered
                ? Color.FromArgb(69, 74, 83)
                : Color.FromArgb(52, 57, 65);
        e.Graphics.Clear(fillColor);

        using var path = RoundedRectangle(pill, Math.Min(10, pill.Height / 2));
        using (var borderPen = new Pen(_hovered
                   ? Color.FromArgb(128, 139, 154)
                   : Color.FromArgb(77, 84, 94)))
        {
            e.Graphics.DrawPath(borderPen, path);
        }

        var dotSize = vertical ? 8 : 9;
        var dotX = pill.Left + (vertical ? (pill.Width - dotSize) / 2 : 12);
        var dotY = vertical ? pill.Top + 8 : pill.Top + (pill.Height - dotSize) / 2;
        using (var statusBrush = new SolidBrush(_statusColor))
        {
            e.Graphics.FillEllipse(statusBrush, dotX, dotY, dotSize, dotSize);
        }

        var textBounds = vertical
            ? new Rectangle(pill.Left + 3, dotY + dotSize + 4, pill.Width - 6,
                Math.Max(1, pill.Bottom - dotY - dotSize - 6))
            : new Rectangle(dotX + dotSize + 8, pill.Top + 1,
                Math.Max(1, pill.Right - dotX - dotSize - 16), pill.Height - 2);
        using var font = new Font("Microsoft YaHei UI", vertical ? 9F : HorizontalFontSize,
            FontStyle.Regular, GraphicsUnit.Point);
        using var textBrush = new SolidBrush(Color.FromArgb(242, 244, 247));
        using var format = new StringFormat
        {
            Alignment = vertical ? StringAlignment.Center : StringAlignment.Near,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap
        };
        e.Graphics.DrawString(_displayText, font, textBrush, textBounds, format);
    }

    private void UpdateDesiredToolbarWidth()
    {
        if (!IsHandleCreated)
        {
            return;
        }

        using var graphics = CreateGraphics();
        using var font = new Font("Microsoft YaHei UI", HorizontalFontSize,
            FontStyle.Regular, GraphicsUnit.Point);
        using var format = new StringFormat(StringFormat.GenericTypographic)
        {
            FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces
        };
        var textWidth = (int)Math.Ceiling(
            graphics.MeasureString(_displayText, font, int.MaxValue, format).Width);

        // 文字从 x=30 左右开始。高 DPI 下 CreateGraphics 与任务栏子窗口的
        // GDI 字形宽度会有少量偏差，因此额外保留约 14px 安全余量。
        _desiredToolbarWidth = Math.Clamp(textWidth + 60,
            MinimumToolbarWidth, MaximumToolbarWidth);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hovered = false;
        _pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _pressed = true;
            Invalidate();
        }

        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && _pressed)
        {
            _pressed = false;
            Invalidate();
            RefreshRequested?.Invoke(this, EventArgs.Empty);
        }

        base.OnMouseUp(e);
    }

    protected override void OnDoubleClick(EventArgs e)
    {
        SettingsRequested?.Invoke(this, EventArgs.Empty);
        base.OnDoubleClick(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _attachmentTimer.Stop();
            _attachmentTimer.Dispose();
            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }

    private static GraphicsPath RoundedRectangle(Rectangle rectangle, int radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private void ApplyRoundedWindowRegion(int width, int height)
    {
        var radius = Math.Min(12, Math.Max(4, height / 3));
        var region = CreateRoundRectRgn(0, 0, width + 1, height + 1, radius * 2, radius * 2);
        if (region == IntPtr.Zero)
        {
            return;
        }

        // SetWindowRgn 成功后区域句柄归系统所有；失败时由当前进程释放。
        if (SetWindowRgn(Handle, region, redraw: true) == 0)
        {
            DeleteObject(region);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public Rectangle ToRectangle() => Rectangle.FromLTRB(Left, Top, Right, Bottom);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? className, string? windowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(
        IntPtr parent,
        IntPtr childAfter,
        string? className,
        string? windowName);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out NativeRect rectangle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(IntPtr child, IntPtr newParent);

    [DllImport("user32.dll")]
    private static extern IntPtr GetParent(IntPtr window);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr window,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        int flags);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateRoundRectRgn(
        int left,
        int top,
        int right,
        int bottom,
        int ellipseWidth,
        int ellipseHeight);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowRgn(IntPtr window, IntPtr region, [MarshalAs(UnmanagedType.Bool)] bool redraw);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr objectHandle);

    private static IntPtr GetWindowLongPtr(IntPtr window, int index) => IntPtr.Size == 8
        ? GetWindowLongPtr64(window, index)
        : new IntPtr(GetWindowLong32(window, index));

    private static IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr newValue) => IntPtr.Size == 8
        ? SetWindowLongPtr64(window, index, newValue)
        : new IntPtr(SetWindowLong32(window, index, newValue.ToInt32()));

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong32(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern int SetWindowLong32(IntPtr window, int index, int newValue);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr window, int index, IntPtr newValue);
}
