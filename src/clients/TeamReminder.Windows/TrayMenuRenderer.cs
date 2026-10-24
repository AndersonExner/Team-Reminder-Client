using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace TeamReminder.Windows;

// Gives the WinForms tray menu the look of Windows 11 menus: theme-aware colours,
// rounded highlight and, where the system supports it, rounded window corners.
internal sealed class TrayMenuRenderer(bool dark) : Forms.ToolStripProfessionalRenderer(new TrayMenuColors(dark))
{
    private readonly Color text = dark ? Color.FromArgb(255, 255, 255) : Color.FromArgb(26, 26, 26);
    private readonly Color disabled = dark ? Color.FromArgb(150, 150, 150) : Color.FromArgb(120, 120, 120);
    private readonly Color highlight = dark ? Color.FromArgb(61, 61, 61) : Color.FromArgb(232, 232, 232);
    private readonly Color separator = dark ? Color.FromArgb(70, 70, 70) : Color.FromArgb(222, 222, 222);

    public static bool SystemUsesDarkTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("SystemUsesLightTheme") is int light && light == 0;
        }
        catch (Exception) { return false; }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public static void RoundCorners(Forms.ToolStripDropDown menu)
    {
        // DWMWA_WINDOW_CORNER_PREFERENCE = 33, DWMWCP_ROUND = 2; ignored before Windows 11.
        var preference = 2;
        try { _ = DwmSetWindowAttribute(menu.Handle, 33, ref preference, sizeof(int)); }
        catch (Exception) { }
    }

    protected override void OnRenderMenuItemBackground(Forms.ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Selected || !e.Item.Enabled) return;
        var bounds = new Rectangle(4, 1, e.Item.Width - 8, e.Item.Height - 2);
        using var path = Rounded(bounds, 4);
        using var brush = new SolidBrush(highlight);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.FillPath(brush, path);
    }

    protected override void OnRenderItemText(Forms.ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? text : disabled;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderSeparator(Forms.ToolStripSeparatorRenderEventArgs e)
    {
        using var pen = new Pen(separator);
        var y = e.Item.Height / 2;
        e.Graphics.DrawLine(pen, 12, y, e.Item.Width - 12, y);
    }

    protected override void OnRenderToolStripBorder(Forms.ToolStripRenderEventArgs e)
    {
        using var pen = new Pen(separator);
        e.Graphics.DrawRectangle(pen, 0, 0, e.AffectedBounds.Width - 1, e.AffectedBounds.Height - 1);
    }

    private static GraphicsPath Rounded(Rectangle r, int radius)
    {
        var path = new GraphicsPath();
        var d = radius * 2;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private sealed class TrayMenuColors(bool dark) : Forms.ProfessionalColorTable
    {
        private readonly Color background = dark ? Color.FromArgb(44, 44, 44) : Color.FromArgb(249, 249, 249);
        public override Color ToolStripDropDownBackground => background;
        public override Color ImageMarginGradientBegin => background;
        public override Color ImageMarginGradientMiddle => background;
        public override Color ImageMarginGradientEnd => background;
        public override Color MenuBorder => background;
        public override Color MenuItemBorder => Color.Transparent;
        public override Color MenuItemSelected => Color.Transparent;
    }
}
