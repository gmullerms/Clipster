using System.Runtime.InteropServices;
using Clipster.Core.Interfaces;
using Clipster.Core.Models;

namespace Clipster.Services.Ocr;

public class ScreenWatcherService : IDisposable
{
    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    private readonly IScreenCaptureService _screenCapture;
    private readonly IAiService _aiService;
    private Timer? _periodicTimer;
    private Timer? _idleCheckTimer;
    private bool _running;
    private bool _analyzing;
    private DateTime? _snoozedUntil;
    private TimeSpan _periodicInterval = TimeSpan.FromMinutes(5);
    private TimeSpan _idleThreshold = TimeSpan.FromMinutes(2);
    private bool _idleNotifiedThisSession;
    private string _lastScreenContext = string.Empty;

    public event EventHandler<ScreenInsight>? InsightReady;

    public ScreenWatcherService(IScreenCaptureService screenCapture, IAiService aiService)
    {
        _screenCapture = screenCapture;
        _aiService = aiService;
    }

    public void Start(TimeSpan? periodicInterval = null)
    {
        if (_running) return;
        _running = true;
        if (periodicInterval.HasValue) _periodicInterval = periodicInterval.Value;

        // Periodic scan every N minutes
        SchedulePeriodicScan();

        // Idle detection: check every 30 seconds if user has been idle
        _idleCheckTimer = new Timer(_ => CheckIdleState(), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    public void Stop()
    {
        _running = false;
        _periodicTimer?.Dispose();
        _periodicTimer = null;
        _idleCheckTimer?.Dispose();
        _idleCheckTimer = null;
    }

    public void Snooze(TimeSpan duration)
    {
        _snoozedUntil = DateTime.Now + duration;
    }

    public void SetInterval(TimeSpan interval)
    {
        _periodicInterval = interval;
        if (_running)
        {
            _periodicTimer?.Dispose();
            SchedulePeriodicScan();
        }
    }

    public async Task<ScreenInsight> AnalyzeNowAsync(string context = "", CancellationToken ct = default)
    {
        var screenshot = await Task.Run(() => _screenCapture.CaptureFullScreen(), ct);
        return await _aiService.WatchScreenAsync(screenshot, ct);
    }

    private void SchedulePeriodicScan()
    {
        if (!_running) return;
        _periodicTimer?.Dispose();
        _periodicTimer = new Timer(_ => RunAnalysis("Periodic background scan."), null, _periodicInterval, Timeout.InfiniteTimeSpan);
    }

    private void CheckIdleState()
    {
        try
        {
            if (!_running || _analyzing || IsSnoozed()) return;

            var idleTime = GetIdleTime();

            if (idleTime >= _idleThreshold && !_idleNotifiedThisSession)
            {
                _idleNotifiedThisSession = true;
                RunAnalysis($"User has been idle for {idleTime.TotalSeconds:F0} seconds. They may be reading, thinking, or stuck. Be proactive and helpful.");
            }

            if (idleTime < TimeSpan.FromSeconds(10))
            {
                _idleNotifiedThisSession = false;
            }
        }
        catch
        {
            // Never let idle check crash the app
        }
    }

    private async void RunAnalysis(string context)
    {
        if (!_running || _analyzing || IsSnoozed()) return;
        _analyzing = true;

        try
        {
            var screenshot = await Task.Run(() => _screenCapture.CaptureFullScreen());
            var insight = await _aiService.WatchScreenAsync(screenshot);

            if (insight.ShouldNotify)
            {
                // Avoid repeating the same insight
                var currentContext = $"{insight.Type}:{insight.Summary}";
                if (currentContext != _lastScreenContext)
                {
                    _lastScreenContext = currentContext;
                    InsightReady?.Invoke(this, insight);
                }
            }
        }
        catch
        {
            // Silently fail
        }
        finally
        {
            _analyzing = false;
            if (_running) SchedulePeriodicScan();
        }
    }

    private bool IsSnoozed()
    {
        if (_snoozedUntil.HasValue && DateTime.Now < _snoozedUntil.Value)
            return true;
        _snoozedUntil = null;
        return false;
    }

    private static TimeSpan GetIdleTime()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (!GetLastInputInfo(ref info))
            return TimeSpan.Zero;

        var idleMs = (uint)Environment.TickCount - info.dwTime;
        return TimeSpan.FromMilliseconds(idleMs);
    }

    public void Dispose()
    {
        Stop();
    }
}
