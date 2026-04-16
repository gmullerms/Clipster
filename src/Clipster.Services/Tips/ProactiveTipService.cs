using Clipster.Core.Events;
using Clipster.Core.Interfaces;

namespace Clipster.Services.Tips;

public class ProactiveTipService : ITipService
{
    private readonly IAiService _aiService;
    private Timer? _timer;
    private TimeSpan _minInterval = TimeSpan.FromMinutes(20);
    private TimeSpan _maxInterval = TimeSpan.FromMinutes(40);
    private DateTime? _snoozedUntil;
    private bool _running;
    private static readonly Random Rng = new();

    public event EventHandler<TipReadyEventArgs>? TipReady;

    public ProactiveTipService(IAiService aiService)
    {
        _aiService = aiService;
    }

    public void Start()
    {
        if (_running) return;
        _running = true;
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
        _timer?.Dispose();
        _timer = new Timer(_ => OnTimerTick(), null, duration, Timeout.InfiniteTimeSpan);
    }

    public void ConfigureInterval(TimeSpan minInterval, TimeSpan maxInterval)
    {
        _minInterval = minInterval;
        _maxInterval = maxInterval;
    }

    private void ScheduleNext()
    {
        if (!_running) return;

        var delayMs = Rng.Next((int)_minInterval.TotalMilliseconds, (int)_maxInterval.TotalMilliseconds);
        _timer?.Dispose();
        _timer = new Timer(_ => OnTimerTick(), null, delayMs, Timeout.Infinite);
    }

    private async void OnTimerTick()
    {
        try
        {
            if (!_running) return;

            if (_snoozedUntil.HasValue && DateTime.Now < _snoozedUntil.Value)
            {
                ScheduleNext();
                return;
            }
            _snoozedUntil = null;

            var tip = await GenerateTip();
            TipReady?.Invoke(this, new TipReadyEventArgs { TipText = tip });

            ScheduleNext();
        }
        catch
        {
            // Never let tip generation crash the app
            ScheduleNext();
        }
    }

    private async Task<string> GenerateTip()
    {
        try
        {
            // Try AI-generated tip first
            var context = $"Time of day: {DateTime.Now:HH:mm}. Day: {DateTime.Now:dddd}.";
            var tip = await _aiService.GenerateTipAsync(context);
            if (!string.IsNullOrWhiteSpace(tip))
                return tip;
        }
        catch
        {
            // AI failed -- fall back to built-in tips
        }

        return TipRepository.GetRandomTip();
    }
}
