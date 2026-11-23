using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
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

    public static Icon CreateAppIcon(TrayIconState state, int size = 32)
    {
        using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);

        float s = size;
        float pad = Math.Max(1f, s * 0.055f);
        float rectSize = s - pad * 2f;
        float radius = s * 0.22f;
        var bgRect = new RectangleF(pad, pad, rectSize, rectSize);

        var borderColor = state switch
        {
            TrayIconState.GameRunning => Color.FromArgb(34, 197, 94),
            TrayIconState.Paused => Color.FromArgb(62, 62, 62),
            _ => Color.FromArgb(72, 72, 72)
        };

        var dotColor = state switch
        {
            TrayIconState.GameRunning => Color.FromArgb(34, 197, 94),
            TrayIconState.Paused => Color.FromArgb(115, 115, 115),
            _ => Color.FromArgb(235, 235, 235)
        };

        using (var path = CreateRoundedRect(bgRect, radius))
        using (var bgBrush = new LinearGradientBrush(bgRect, Color.FromArgb(30, 30, 30), Color.FromArgb(14, 14, 14), LinearGradientMode.Vertical))
        using (var borderPen = new Pen(borderColor, Math.Max(1.2f, s * 0.04f)))
        {
            g.FillPath(bgBrush, path);
            g.DrawPath(borderPen, path);
        }

        float cx = s * 0.50f;
        float cy = s * 0.50f;
        float wheelRadius = s * 0.285f;
        float rimWidth = Math.Max(1.8f, s * 0.072f);
        float spokeWidth = Math.Max(1.6f, s * 0.062f);

        var wheelColor = state == TrayIconState.Paused
            ? Color.FromArgb(150, 150, 150)
            : Color.FromArgb(242, 242, 242);

        using (var rimPen = new Pen(wheelColor, rimWidth))
        {
            g.DrawEllipse(rimPen, cx - wheelRadius, cy - wheelRadius, wheelRadius * 2f, wheelRadius * 2f);
        }

        using (var spokePen = new Pen(wheelColor, spokeWidth))
        {
            spokePen.StartCap = LineCap.Round;
            spokePen.EndCap = LineCap.Round;
            float spokeY = cy + wheelRadius * 0.06f;
            g.DrawLine(spokePen, cx - wheelRadius * 0.92f, spokeY, cx + wheelRadius * 0.92f, spokeY);
            g.DrawLine(spokePen, cx, cy, cx, cy + wheelRadius * 0.94f);
        }

        float hubRadius = s * 0.095f;
        using (var hubBg = new SolidBrush(Color.FromArgb(20, 20, 20)))
        using (var hubRing = new Pen(wheelColor, Math.Max(1.3f, s * 0.042f)))
        {
            g.FillEllipse(hubBg, cx - hubRadius, cy - hubRadius, hubRadius * 2f, hubRadius * 2f);
            g.DrawEllipse(hubRing, cx - hubRadius, cy - hubRadius, hubRadius * 2f, hubRadius * 2f);
        }

        float dotOuterR = s * 0.145f;
        float dotInnerR = s * 0.10f;
        float dotX = s * 0.78f;
        float dotY = s * 0.78f;
        using (var cutBrush = new SolidBrush(Color.FromArgb(16, 16, 16)))
        using (var dotBrush = new SolidBrush(dotColor))
        {
            g.FillEllipse(cutBrush, dotX - dotOuterR, dotY - dotOuterR, dotOuterR * 2f, dotOuterR * 2f);
            g.FillEllipse(dotBrush, dotX - dotInnerR, dotY - dotInnerR, dotInnerR * 2f, dotInnerR * 2f);
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

    private static GraphicsPath CreateRoundedRect(RectangleF bounds, float radius)
    {
        float d = radius * 2f;
        var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
