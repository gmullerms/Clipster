using Clipster.Core.Events;

namespace Clipster.Core.Interfaces;

public interface ITipService
{
    event EventHandler<TipReadyEventArgs>? TipReady;
    void Start();
    void Stop();
    void Snooze(TimeSpan duration);
    void ConfigureInterval(TimeSpan minInterval, TimeSpan maxInterval);
}
