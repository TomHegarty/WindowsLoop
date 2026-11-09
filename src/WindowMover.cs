using System.Runtime.InteropServices;
using static CtrlSnap.Native;

namespace CtrlSnap;

internal static class WindowMover
{
    /// <summary>Moves a window so its visible frame fills <paramref name="bounds"/> exactly.</summary>
    public static void Move(IntPtr hwnd, Rectangle bounds)
    {
        if (hwnd == IntPtr.Zero || IsHungAppWindow(hwnd)) return;

        if (IsZoomed(hwnd) || IsIconic(hwnd))
            ShowWindow(hwnd, SW_RESTORE);

        Apply(hwnd, bounds);
        // Moving to a monitor with a different scale makes the app rescale itself after the
        // first move, which can leave it the wrong size. A second pass settles it.
        Apply(hwnd, bounds);
    }

    private static void Apply(IntPtr hwnd, Rectangle bounds)
    {
        // Windows 10/11 windows have invisible resize borders outside the visible frame.
        // Measure them and grow the target so the visible edge lands where we want.
        int left = 0, top = 0, right = 0, bottom = 0;
        if (GetWindowRect(hwnd, out RECT window) &&
            DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out RECT frame, Marshal.SizeOf<RECT>()) == 0)
        {
            left = frame.Left - window.Left;
            top = frame.Top - window.Top;
            right = window.Right - frame.Right;
            bottom = window.Bottom - frame.Bottom;
        }

        SetWindowPos(hwnd, IntPtr.Zero,
            bounds.X - left,
            bounds.Y - top,
            bounds.Width + left + right,
            bounds.Height + top + bottom,
            SWP_NOZORDER | SWP_NOACTIVATE);
    }
}
