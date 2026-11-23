using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace ETS2CompanionLauncher.UI;

public enum TrayIconState
{
    Watching,
    GameRunning,
    Paused
}

public static class IconFactory
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public static Icon CreateAppIcon(TrayIconState state)
    {
        using var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        var bgColor = Color.FromArgb(20, 20, 20);
        var borderColor = state switch
        {
            TrayIconState.GameRunning => Color.FromArgb(34, 197, 94),
            TrayIconState.Paused => Color.FromArgb(95, 95, 95),
            _ => Color.FromArgb(180, 180, 180)
        };
        var accentColor = state switch
        {
            TrayIconState.GameRunning => Color.FromArgb(34, 197, 94),
            TrayIconState.Paused => Color.FromArgb(115, 115, 115),
            _ => Color.FromArgb(235, 235, 235)
        };

        // Rounded badge background
        using (var path = CreateRoundedRect(new Rectangle(1, 1, 29, 29), 7))
        using (var bgBrush = new SolidBrush(bgColor))
        using (var borderPen = new Pen(borderColor, 1.8f))
        {
            g.FillPath(bgBrush, path);
            g.DrawPath(borderPen, path);
        }

        // Stylized steering wheel emblem
        using (var ringPen = new Pen(Color.FromArgb(235, 235, 235), 2.1f))
        {
            g.DrawEllipse(ringPen, 7, 7, 17, 17);
            g.DrawLine(ringPen, 8, 15, 23, 15);
            g.DrawLine(ringPen, 15.5f, 15.5f, 15.5f, 23.5f);
        }

        // Center hub dot
        using (var hubBrush = new SolidBrush(accentColor))
        {
            g.FillEllipse(hubBrush, 12.5f, 12.5f, 6, 6);
        }

        // Status indicator dot at bottom-right
        using (var dotOutline = new SolidBrush(bgColor))
        using (var dotBrush = new SolidBrush(accentColor))
        {
            g.FillEllipse(dotOutline, 21, 21, 10, 10);
            g.FillEllipse(dotBrush, 22.5f, 22.5f, 7, 7);
        }

        var hIcon = bmp.GetHicon();
        try
        {
            using var tempIcon = Icon.FromHandle(hIcon);
            return (Icon)tempIcon.Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    private static GraphicsPath CreateRoundedRect(Rectangle bounds, int radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
