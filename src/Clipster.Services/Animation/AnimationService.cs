using Clipster.Core.Interfaces;
using Clipster.Core.Models;

namespace Clipster.Services.Animation;

public class AnimationService : IAnimationService
{
    private static readonly Dictionary<AnimationState, int> Priorities = new()
    {
        [AnimationState.Idle] = 0,
        [AnimationState.Greeting] = 1,
        [AnimationState.Pointing] = 1,
        [AnimationState.Celebrating] = 1,
        [AnimationState.Talking] = 2,
        [AnimationState.Looking] = 2,
        [AnimationState.Confused] = 2,
        [AnimationState.Thinking] = 3, // Highest - active API call
    };

    private readonly Queue<(AnimationState State, Action? Callback)> _queue = new();
    private Action? _onCompleteCallback;
    private bool _isPlayingOnce;
    private Timer? _autoReturnTimer;

    public AnimationState CurrentState { get; private set; } = AnimationState.Idle;
    public event EventHandler<AnimationState>? StateChanged;

    public void TransitionTo(AnimationState newState)
    {
        if (CurrentState == newState) return;

        _autoReturnTimer?.Dispose();
        _autoReturnTimer = null;
        _isPlayingOnce = false;
        _onCompleteCallback = null;
        _queue.Clear();

        CurrentState = newState;
        StateChanged?.Invoke(this, newState);
    }

    public void PlayOnce(AnimationState animation, Action? onComplete = null)
    {
        // If a higher-priority animation is active, queue this one
        if (_isPlayingOnce && GetPriority(CurrentState) > GetPriority(animation))
        {
            _queue.Enqueue((animation, onComplete));
            return;
        }

        _autoReturnTimer?.Dispose();
        _autoReturnTimer = null;
        _isPlayingOnce = true;
        _onCompleteCallback = onComplete;
        CurrentState = animation;
        StateChanged?.Invoke(this, animation);

        // Auto-return to Idle after a timeout in case OnAnimationCompleted is never called
        _autoReturnTimer = new Timer(_ => OnAnimationCompleted(), null, TimeSpan.FromSeconds(5), Timeout.InfiniteTimeSpan);
    }

    public void OnAnimationCompleted()
    {
        if (!_isPlayingOnce) return;

        _autoReturnTimer?.Dispose();
        _autoReturnTimer = null;
        _isPlayingOnce = false;
        var callback = _onCompleteCallback;
        _onCompleteCallback = null;

        // Play queued animation if any
        if (_queue.TryDequeue(out var next))
        {
            PlayOnce(next.State, next.Callback);
            callback?.Invoke();
            return;
        }

        CurrentState = AnimationState.Idle;
        StateChanged?.Invoke(this, AnimationState.Idle);
        callback?.Invoke();
    }

    private static int GetPriority(AnimationState state)
    {
        return Priorities.GetValueOrDefault(state, 0);
    }
}
