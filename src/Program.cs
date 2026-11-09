namespace CtrlSnap;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // Only one copy should own the keyboard hook.
        using var mutex = new Mutex(initiallyOwned: true, @"Local\CtrlSnap.SingleInstance", out bool isFirst);
        if (!isFirst) return;

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApp());
    }
}
