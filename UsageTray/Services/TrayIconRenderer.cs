using System.Drawing.Drawing2D;
using System.Globalization;
using System.Runtime.InteropServices;

namespace UsageTray.Services;

internal enum TrayIconState
{
    Loading,
    Healthy,
    Invalid,
    Error
}

internal static class TrayIconRenderer
{
    public static Icon Create(decimal? remaining, TrayIconState state)
    {
        const int size = 32;
        using var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var background = state switch
        {
            TrayIconState.Healthy => Color.FromArgb(31, 143, 78),
            TrayIconState.Invalid => Color.FromArgb(214, 126, 20),
            TrayIconState.Error => Color.FromArgb(196, 48, 43),
            _ => Color.FromArgb(49, 109, 184)
        };

        using (var brush = new SolidBrush(background))
        {
            graphics.FillEllipse(brush, 1, 1, size - 2, size - 2);
        }

        var text = remaining is null ? "…" : Compact(remaining.Value);
        var fontSize = text.Length switch
        {
            <= 2 => 16f,
            3 => 12f,
            _ => 9f
        };
        using var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
        using var textBrush = new SolidBrush(Color.White);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        graphics.DrawString(text, font, textBrush, new RectangleF(0, 0, size, size), format);

        var handle = bitmap.GetHicon();
        try
        {
            using var temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    private static string Compact(decimal value)
    {
        var absolute = Math.Abs(value);
        if (absolute >= 1_000_000)
        {
            return (value / 1_000_000m).ToString("0.#", CultureInfo.InvariantCulture) + "M";
        }

        if (absolute >= 1_000)
        {
            return (value / 1_000m).ToString("0.#", CultureInfo.InvariantCulture) + "K";
        }

        if (absolute >= 100)
        {
            return decimal.Truncate(value).ToString(CultureInfo.InvariantCulture);
        }

        if (absolute >= 10)
        {
            return value.ToString("0.#", CultureInfo.InvariantCulture);
        }

        if (absolute >= 1)
        {
            return value.ToString("0.#", CultureInfo.InvariantCulture);
        }

        return value.ToString("0.##", CultureInfo.InvariantCulture).TrimStart('0');
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
}
