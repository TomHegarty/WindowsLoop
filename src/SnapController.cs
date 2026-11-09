using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using static CtrlSnap.Native;

namespace CtrlSnap;

/// <summary>
/// Watches the keyboard and mouse. Holding Ctrl on its own (no other key, no click)
/// for a moment shows a preview; moving the mouse picks a position; releasing Ctrl
/// moves the window that had focus when Ctrl went down.
/// </summary>
internal sealed class SnapController : IDisposable
{
    /// <summary>How long Ctrl must be held before the preview appears. Quick Ctrl taps do nothing.</summary>
    private const int HoldDelayMs = 180;

    /// <summary>Mouse movement (in pixels) below which the choice stays on "maximize".</summary>
    private const int DeadZonePx = 24;

    private enum State { Idle, Pending, Active }

    private readonly LowLevelProc _keyboardProc;
    private readonly LowLevelProc _mouseProc;
    private readonly IntPtr _keyboardHook;
    private readonly IntPtr _mouseHook;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly PreviewForm _preview;
    private readonly uint _ownProcessId = (uint)Environment.ProcessId;

    private State _state = State.Idle;
    private long _pressedAt;
    private Point _anchor;
    private IntPtr _target;
    private SnapPosition? _current;

    public bool Enabled { get; set; } = true;

    public SnapController()
    {
        _preview = new PreviewForm();
        _timer = new System.Windows.Forms.Timer { Interval = 15 };
        _timer.Tick += OnTick;

        // Keep the delegates in fields so the GC can't collect them while Windows still calls them.
        _keyboardProc = KeyboardProc;
        _mouseProc = MouseProc;
        IntPtr module = GetModuleHandle(null);

        _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, module, 0);
        if (_keyboardHook == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not install keyboard hook");

        _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, module, 0);
        if (_mouseHook == IntPtr.Zero)
        {
            UnhookWindowsHookEx(_keyboardHook);
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not install mouse hook");
        }
    }

    private IntPtr KeyboardProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var info = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            // Ignore keys sent by other programs (macros, remote tools) so they can't trigger a snap.
            if ((info.flags & LLKHF_INJECTED) == 0)
            {
                int msg = (int)wParam;
                bool down = msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN;
                HandleKey((int)info.vkCode, down);
            }
        }
        // Never swallow anything: Ctrl+C, Ctrl+Tab and so on keep working as normal.
        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    private IntPtr MouseProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && _state != State.Idle)
        {
            switch ((int)wParam)
            {
                case WM_LBUTTONDOWN:
                case WM_RBUTTONDOWN:
                case WM_MBUTTONDOWN:
                case WM_XBUTTONDOWN:
                case WM_MOUSEWHEEL:
                case WM_MOUSEHWHEEL:
                    // Ctrl+click or Ctrl+scroll: the user wants normal Ctrl behaviour.
                    Cancel();
                    break;
            }
        }
        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    private void HandleKey(int vk, bool down)
    {
        bool isCtrl = vk is VK_LCONTROL or VK_RCONTROL or VK_CONTROL;

        if (isCtrl)
        {
            if (down)
            {
                // Auto-repeat sends more key-downs while held; only the first one starts anything.
                if (_state == State.Idle && Enabled && !OtherModifiersHeld())
                    Begin();
            }
            else if (_state == State.Active)
            {
                Commit();
            }
            else
            {
                Cancel();
            }
        }
        else if (down && _state != State.Idle)
        {
            // Any other key while Ctrl is held means it's a shortcut, not a snap.
            Cancel();
        }
    }

    private static bool OtherModifiersHeld() =>
        IsDown(VK_SHIFT) || IsDown(VK_MENU) || IsDown(VK_LWIN) || IsDown(VK_RWIN);

    private static bool IsDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

    private void Begin()
    {
        IntPtr target = FindTargetWindow();
        if (target == IntPtr.Zero) return;

        _target = target;
        _anchor = Cursor.Position;
        _pressedAt = Environment.TickCount64;
        _current = null;
        _state = State.Pending;
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        // Safety net: if we missed the key-up (e.g. a UAC prompt took the desktop), stop.
        if (!IsDown(VK_CONTROL))
        {
            Cancel();
            return;
        }

        if (_state == State.Pending && Environment.TickCount64 - _pressedAt >= HoldDelayMs)
            _state = State.Active;

        if (_state != State.Active) return;

        Point p = Cursor.Position;
        SnapPosition position = Layout.Pick(p.X - _anchor.X, p.Y - _anchor.Y, DeadZonePx);
        if (position == _current) return;

        _current = position;
        _preview.ShowAt(Layout.Bounds(position, WorkArea()));
    }

    private void Commit()
    {
        SnapPosition? position = _current;
        IntPtr target = _target;
        Rectangle area = WorkArea();
        Cancel();

        if (position is { } pos)
        {
            Rectangle bounds = Layout.Bounds(pos, area);
            // Leave the hook callback quickly; do the actual move on the next message-loop turn.
            _preview.BeginInvoke(() => WindowMover.Move(target, bounds));
        }
    }

    private void Cancel()
    {
        _state = State.Idle;
        _timer.Stop();
        _current = null;
        _preview.HideOverlay();
    }

    /// <summary>The monitor under the mouse when Ctrl was pressed decides where the window goes.</summary>
    private Rectangle WorkArea() => Screen.FromPoint(_anchor).WorkingArea;

    private IntPtr FindTargetWindow()
    {
        IntPtr hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return IntPtr.Zero;
        hwnd = GetAncestor(hwnd, GA_ROOT);
        if (hwnd == IntPtr.Zero || !IsWindowVisible(hwnd)) return IntPtr.Zero;

        GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == _ownProcessId) return IntPtr.Zero;

        var cls = new StringBuilder(64);
        GetClassName(hwnd, cls, cls.Capacity);
        return cls.ToString() switch
        {
            // Desktop and taskbar.
            "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" => IntPtr.Zero,
            _ => hwnd,
        };
    }

    public void Dispose()
    {
        Cancel();
        UnhookWindowsHookEx(_keyboardHook);
        UnhookWindowsHookEx(_mouseHook);
        _timer.Dispose();
        _preview.Dispose();
    }
}
