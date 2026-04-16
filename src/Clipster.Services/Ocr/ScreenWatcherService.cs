using Clipster.Core.Interfaces;
using Clipster.Core.Models;

namespace Clipster.Services.Ocr;

public class ScreenWatcherService : IDisposable
{
    private readonly IScreenCaptureService _screenCapture;
    private readonly IAiService _aiService;
    private Timer? _timer;
    private bool _running;
    private bool _analyzing;
    private DateTime? _snoozedUntil;
    private TimeSpan _interval = TimeSpan.FromMinutes(5);

    public event EventHandler<ScreenInsight>? InsightReady;

    public ScreenWatcherService(IScreenCaptureService screenCapture, IAiService aiService)
    {
        _screenCapture = screenCapture;
        _aiService = aiService;
    }

    public void Start(TimeSpan? interval = null)
    {
        if (_running) return;
        _running = true;
        if (interval.HasValue) _interval = interval.Value;
        ScheduleNext();
    }

    public void Stop()
    {
        _running = false;
        _timer?.Dispose();
        _timer = null;
    }

    public void Snooze(TimeSpan duration)
    {
        _snoozedUntil = DateTime.Now + duration;
    }

    public void SetInterval(TimeSpan interval)
    {
        _interval = interval;
        if (_running)
        {
            _timer?.Dispose();
            ScheduleNext();
        }
    }

    /// <summary>
    /// Manually trigger a screen analysis right now (for the hotkey).
    /// </summary>
    public async Task<ScreenInsight> AnalyzeNowAsync(CancellationToken ct = default)
    {
        var screenshot = await Task.Run(() => _screenCapture.CaptureFullScreen(), ct);
        return await _aiService.WatchScreenAsync(screenshot, ct);
    }

    private void ScheduleNext()
    {
        if (!_running) return;
        _timer?.Dispose();
        _timer = new Timer(_ => OnTimerTick(), null, _interval, Timeout.InfiniteTimeSpan);
    }

    private async void OnTimerTick()
    {
        if (!_running || _analyzing) return;

        // Check snooze
        if (_snoozedUntil.HasValue && DateTime.Now < _snoozedUntil.Value)
        {
            ScheduleNext();
            return;
        }
        _snoozedUntil = null;
        _analyzing = true;

        try
        {
            var insight = await AnalyzeNowAsync();
            if (insight.ShouldNotify)
            {
                InsightReady?.Invoke(this, insight);
            }
        }
        catch
        {
            // Silently fail -- don't bother the user if screen watch fails
        }
        finally
        {
            _analyzing = false;
            ScheduleNext();
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
