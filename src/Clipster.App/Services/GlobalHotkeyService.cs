using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Clipster.App.Services;

public class GlobalHotkeyService : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const int HOTKEY_QUICK_PROMPT = 9001;
    private const int HOTKEY_SCREEN_SCAN = 9002;

    private const uint MOD_CTRL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_NOREPEAT = 0x4000;

    private const uint VK_SPACE = 0x20;
    private const uint VK_S = 0x53;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private IntPtr _hwnd;
    private HwndSource? _source;

    public event EventHandler? QuickPromptPressed;
    public event EventHandler? ScreenScanPressed;

    public bool Register(Window window)
    {
        var helper = new WindowInteropHelper(window);
        _hwnd = helper.Handle;
        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(WndProc);

        var r1 = RegisterHotKey(_hwnd, HOTKEY_QUICK_PROMPT, MOD_CTRL | MOD_SHIFT | MOD_NOREPEAT, VK_SPACE);
        var r2 = RegisterHotKey(_hwnd, HOTKEY_SCREEN_SCAN, MOD_CTRL | MOD_SHIFT | MOD_NOREPEAT, VK_S);
        return r1 && r2;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            var id = wParam.ToInt32();
            if (id == HOTKEY_QUICK_PROMPT)
            {
                QuickPromptPressed?.Invoke(this, EventArgs.Empty);
                handled = true;
            }
            else if (id == HOTKEY_SCREEN_SCAN)
            {
                ScreenScanPressed?.Invoke(this, EventArgs.Empty);
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        _source?.RemoveHook(WndProc);
        if (_hwnd != IntPtr.Zero)
        {
            UnregisterHotKey(_hwnd, HOTKEY_QUICK_PROMPT);
            UnregisterHotKey(_hwnd, HOTKEY_SCREEN_SCAN);
        }
    }
}
