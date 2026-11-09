using static CtrlSnap.Native;

namespace CtrlSnap;

/// <summary>A see-through, click-through rectangle showing where the window will go.</summary>
internal sealed class PreviewForm : Form
{
    private const int Inset = 6;

    public PreviewForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Color.FromArgb(40, 120, 215);
        Opacity = 0.30;
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        _ = Handle; // create the window now so the first preview has no delay
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_LAYERED | WS_EX_TOPMOST;
            return cp;
        }
    }

    public void ShowAt(Rectangle bounds)
    {
        bounds.Inflate(-Inset, -Inset);
        SetWindowPos(Handle, HWND_TOPMOST, bounds.X, bounds.Y, bounds.Width, bounds.Height,
            SWP_NOACTIVATE | SWP_SHOWWINDOW);
        Invalidate();
    }

    public void HideOverlay()
    {
        if (IsHandleCreated) ShowWindow(Handle, SW_HIDE);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        using var pen = new Pen(Color.White, 4);
        e.Graphics.DrawRectangle(pen, 2, 2, ClientSize.Width - 4, ClientSize.Height - 4);
    }

    protected override void WndProc(ref Message m)
    {
        // We place this window in physical pixels ourselves; don't let WinForms
        // rescale it when it lands on a monitor with a different DPI.
        if (m.Msg == WM_DPICHANGED) return;
        base.WndProc(ref m);
    }
}
