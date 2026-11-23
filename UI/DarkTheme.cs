using System.Drawing;
using System.Runtime.InteropServices;

namespace ETS2CompanionLauncher.UI;

public static class DarkTheme
{
    // Pure neutral dark palette (no blue/purple tint)
    public static readonly Color Background = Color.FromArgb(15, 15, 15);        // #0F0F0F
    public static readonly Color Surface = Color.FromArgb(24, 24, 24);           // #181818
    public static readonly Color Elevated = Color.FromArgb(34, 34, 34);          // #222222
    public static readonly Color Hover = Color.FromArgb(45, 45, 45);             // #2D2D2D
    public static readonly Color Selection = Color.FromArgb(52, 52, 52);         // #343434
    public static readonly Color Border = Color.FromArgb(46, 46, 46);            // #2E2E2E
    public static readonly Color LogBackground = Color.FromArgb(12, 12, 12);     // #0C0C0C

    public static readonly Color TextPrimary = Color.FromArgb(242, 242, 242);    // #F2F2F2
    public static readonly Color TextSecondary = Color.FromArgb(160, 160, 160);  // #A0A0A0
    public static readonly Color TextMuted = Color.FromArgb(128, 128, 128);      // #808080

    // Monochrome primary accent + minimal semantic indicators
    public static readonly Color AccentPrimary = Color.FromArgb(235, 235, 235);  // #EBEBEB
    public static readonly Color AccentOnPrimary = Color.FromArgb(15, 15, 15);   // #0F0F0F
    public static readonly Color Success = Color.FromArgb(34, 197, 94);          // #22C55E
    public static readonly Color Warning = Color.FromArgb(234, 179, 8);          // #EAB308
    public static readonly Color Danger = Color.FromArgb(239, 68, 68);           // #EF4444

    private const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    public static void ApplyDarkTitleBar(Form form)
    {
        void Apply()
        {
            if (!form.IsHandleCreated) return;
            try
            {
                int useDark = 1;
                if (DwmSetWindowAttribute(form.Handle, DwmwaUseImmersiveDarkMode, ref useDark, sizeof(int)) != 0)
                {
                    DwmSetWindowAttribute(form.Handle, DwmwaUseImmersiveDarkModeBefore20H1, ref useDark, sizeof(int));
                }

                // Windows 11 custom pure dark title bar color (COLORREF is 0x00BBGGRR)
                int captionColor = ColorTranslator.ToWin32(Background);
                DwmSetWindowAttribute(form.Handle, DwmwaCaptionColor, ref captionColor, sizeof(int));

                int textColor = ColorTranslator.ToWin32(TextPrimary);
                DwmSetWindowAttribute(form.Handle, DwmwaTextColor, ref textColor, sizeof(int));
            }
            catch
            {
                // Ignore on older Windows builds that do not support these DWM attributes.
            }
        }

        if (form.IsHandleCreated)
        {
            Apply();
        }
        else
        {
            form.HandleCreated += (_, _) => Apply();
        }
    }

    public static void StyleMenu(ContextMenuStrip menu)
    {
        menu.BackColor = Surface;
        menu.ForeColor = TextPrimary;
        menu.ShowImageMargin = false;
        menu.Renderer = new DarkMenuRenderer();
    }

    public static Button CreateButton(string text, int x, int y, int width, int height, bool primary = false, Color? foreColorOverride = null)
    {
        var back = primary ? AccentPrimary : Elevated;
        var fore = foreColorOverride ?? (primary ? AccentOnPrimary : TextPrimary);

        var btn = new Button
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(width, height),
            FlatStyle = FlatStyle.Flat,
            BackColor = back,
            ForeColor = fore,
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderColor = primary ? AccentPrimary : Border;
        btn.FlatAppearance.BorderSize = 1;
        btn.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(215, 215, 215) : Hover;
        btn.FlatAppearance.MouseDownBackColor = primary ? Color.FromArgb(195, 195, 195) : Selection;
        return btn;
    }

    public static Button CreateAutoButton(string text, bool primary = false, Color? foreColorOverride = null)
    {
        var back = primary ? AccentPrimary : Elevated;
        var fore = foreColorOverride ?? (primary ? AccentOnPrimary : TextPrimary);

        var btn = new Button
        {
            Text = text,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(72, 32),
            Padding = new Padding(12, 4, 12, 4),
            Margin = new Padding(0, 0, 8, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = back,
            ForeColor = fore,
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderColor = primary ? AccentPrimary : Border;
        btn.FlatAppearance.BorderSize = 1;
        btn.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(215, 215, 215) : Hover;
        btn.FlatAppearance.MouseDownBackColor = primary ? Color.FromArgb(195, 195, 195) : Selection;
        return btn;
    }

    private sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkColorTable())
        {
            RoundedEdges = false;
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? TextPrimary : TextMuted;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            var y = e.Item.Height / 2;
            using var pen = new Pen(Border);
            e.Graphics.DrawLine(pen, 8, y, e.Item.Width - 8, y);
        }
    }

    private sealed class DarkColorTable : ProfessionalColorTable
    {
        public override Color MenuBorder => Border;
        public override Color MenuItemBorder => Hover;
        public override Color MenuItemSelected => Hover;
        public override Color MenuItemSelectedGradientBegin => Hover;
        public override Color MenuItemSelectedGradientEnd => Hover;
        public override Color MenuItemPressedGradientBegin => Selection;
        public override Color MenuItemPressedGradientEnd => Selection;
        public override Color ToolStripDropDownBackground => Surface;
        public override Color ImageMarginGradientBegin => Surface;
        public override Color ImageMarginGradientMiddle => Surface;
        public override Color ImageMarginGradientEnd => Surface;
        public override Color CheckBackground => Selection;
        public override Color CheckSelectedBackground => Hover;
        public override Color CheckPressedBackground => Selection;
    }
}
