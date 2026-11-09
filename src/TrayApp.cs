using System.Drawing.Drawing2D;
using Microsoft.Win32;

namespace CtrlSnap;

internal sealed class TrayApp : ApplicationContext
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValue = "CtrlSnap";

    private readonly NotifyIcon _icon;
    private readonly SnapController _snap;

    public TrayApp()
    {
        _snap = new SnapController();

        var enabled = new ToolStripMenuItem("Enabled") { Checked = true, CheckOnClick = true };
        enabled.CheckedChanged += (_, _) => _snap.Enabled = enabled.Checked;

        var startup = new ToolStripMenuItem("Start with Windows") { Checked = IsStartupEnabled(), CheckOnClick = true };
        startup.CheckedChanged += (_, _) => SetStartup(startup.Checked);

        var menu = new ContextMenuStrip();
        menu.Items.Add(enabled);
        menu.Items.Add(startup);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitThread());

        _icon = new NotifyIcon
        {
            Icon = CreateIcon(),
            Text = "CtrlSnap: hold Ctrl and move the mouse",
            ContextMenuStrip = menu,
            Visible = true,
        };
    }

    protected override void ExitThreadCore()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _snap.Dispose();
        base.ExitThreadCore();
    }

    private static bool IsStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(RunValue) is string;
    }

    private static void SetStartup(bool on)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (on) key.SetValue(RunValue, $"\"{Environment.ProcessPath}\"");
        else key.DeleteValue(RunValue, throwOnMissingValue: false);
    }

    /// <summary>A simple 2x2 grid with one tile filled, drawn at runtime so there's no icon file to ship.</summary>
    private static Icon CreateIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var outline = new Pen(Color.White, 2.5f);
            using var fill = new SolidBrush(Color.FromArgb(70, 150, 240));
            var tiles = new[] { new Rectangle(3, 3, 12, 12), new Rectangle(17, 3, 12, 12), new Rectangle(3, 17, 12, 12), new Rectangle(17, 17, 12, 12) };
            g.FillRectangle(fill, tiles[0]);
            foreach (var t in tiles) g.DrawRectangle(outline, t);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }
}
