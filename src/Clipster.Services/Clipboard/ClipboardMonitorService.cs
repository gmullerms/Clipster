using System.Runtime.InteropServices;
using Clipster.Core.Events;
using Clipster.Core.Interfaces;
using Clipster.Core.Models;

namespace Clipster.Services.Clipboard;

public class ClipboardMonitorService : IClipboardService
{
    private const int WM_CLIPBOARDUPDATE = 0x031D;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    private IntPtr _hwnd;
    private System.Windows.Interop.HwndSource? _hwndSource;
    private readonly ClipboardAnalyzer _analyzer = new();

    // Debounce: ignore rapid clipboard changes
    private DateTime _lastEvent = DateTime.MinValue;
    private string _lastContent = string.Empty;
    private static readonly TimeSpan DebounceInterval = TimeSpan.FromSeconds(2);

    public event EventHandler<ClipboardChangedEventArgs>? ClipboardChanged;

    public void StartMonitoring(IntPtr windowHandle)
    {
        _hwnd = windowHandle;
        _hwndSource = System.Windows.Interop.HwndSource.FromHwnd(windowHandle);
        _hwndSource?.AddHook(WndProc);
        AddClipboardFormatListener(windowHandle);
    }

    public void StopMonitoring()
    {
        if (_hwnd != IntPtr.Zero)
        {
            RemoveClipboardFormatListener(_hwnd);
            _hwndSource?.RemoveHook(WndProc);
            _hwnd = IntPtr.Zero;
            _hwndSource = null;
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_CLIPBOARDUPDATE)
        {
            OnClipboardUpdate();
        }
        return IntPtr.Zero;
    }

    private void OnClipboardUpdate()
    {
        try
        {
            // Must access clipboard on the UI thread
            if (!System.Windows.Clipboard.ContainsText())
                return;

            var text = System.Windows.Clipboard.GetText();
            if (string.IsNullOrWhiteSpace(text))
                return;

            // Debounce: skip if same content or too soon
            var now = DateTime.Now;
            if (text == _lastContent && (now - _lastEvent) < DebounceInterval)
                return;

            _lastContent = text;
            _lastEvent = now;

            var contentType = _analyzer.Classify(text);
            var content = new ClipboardContent
            {
                Text = text,
                ContentType = contentType,
                Timestamp = now
            };

            ClipboardChanged?.Invoke(this, new ClipboardChangedEventArgs { Content = content });
        }
        catch
        {
            // Clipboard access can fail if another app has it locked
        }
    }
}
