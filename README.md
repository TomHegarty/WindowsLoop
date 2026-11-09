# CtrlSnap

A tiny Windows tray app for snapping windows with one gesture:

1. Click into the window you want to move.
2. **Hold Ctrl** on its own. After a moment a blue preview appears.
3. **Move the mouse** toward where the window should go. The preview follows.
4. **Let go of Ctrl.** The window snaps there.

| Move the mouse… | Window goes to |
| --- | --- |
| barely / not at all | Maximize (fills the screen, minus the taskbar) |
| left / right | Left half / right half |
| up / down | Top half / bottom half |
| diagonally | The matching quarter |

Directions are measured from where the mouse was when you pressed Ctrl, so the
gesture works anywhere on screen. The window goes to the monitor the mouse was on
when you pressed Ctrl, so you can use it to move windows between screens too.

### It stays out of the way of normal Ctrl use

- A quick Ctrl tap does nothing (the preview only appears after ~180 ms).
- Pressing any other key while holding Ctrl (Ctrl+C, Ctrl+Tab, …) cancels the snap
  and the shortcut works as normal.
- Clicking or scrolling while holding Ctrl (Ctrl+click, Ctrl+scroll zoom) cancels it too.
- Ctrl together with Shift, Alt or Win never starts a snap.
- Nothing is ever swallowed: every key and click still reaches your apps.

## Tray menu

Right-click the grid icon in the system tray:

- **Enabled**: turn snapping on or off.
- **Start with Windows**: launch automatically when you sign in.
- **Exit**

## Download

Every push to `main` builds a ready-to-run `CtrlSnap.exe` (no .NET install needed).
Open the repo's **Actions** tab → latest **build** run → download the
**CtrlSnap-win-x64** artifact, unzip it, and run `CtrlSnap.exe`.

Because the exe isn't code-signed, Windows SmartScreen may warn the first time:
click **More info → Run anyway**.

## Build it yourself

Needs Windows 10/11 and the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
git clone <this repo>
cd <repo folder>

# Run straight from source
dotnet run --project src/CtrlSnap.csproj

# Or make a single self-contained exe in .\publish
dotnet publish src/CtrlSnap.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true -o publish
.\publish\CtrlSnap.exe
```

## Tweaking

The feel is set by two constants at the top of `src/SnapController.cs`:

- `HoldDelayMs` (default 180): how long Ctrl must be held before the preview shows.
- `DeadZonePx` (default 24): how far the mouse must move before a direction counts;
  inside it the choice is "maximize".

## Limitations

- Windows can't move windows of apps running **as administrator** unless CtrlSnap
  also runs as administrator (a Windows security rule).
- Some apps enforce their own minimum size, so a quarter can come out slightly bigger
  on small screens.

## How it works

- `SnapController.cs`: low-level keyboard and mouse hooks (`WH_KEYBOARD_LL`,
  `WH_MOUSE_LL`) track the Ctrl gesture; a 15 ms timer follows the mouse while it's held.
- `Layout.cs`: turns the mouse offset into one of nine positions (8 × 45° slices
  plus the centre) and the position into a rectangle on the monitor's work area.
- `PreviewForm.cs`: a click-through, translucent, always-on-top window for the preview.
- `WindowMover.cs`: restores maximized windows, corrects for Windows 10/11's invisible
  window borders, and moves the window with `SetWindowPos`.
