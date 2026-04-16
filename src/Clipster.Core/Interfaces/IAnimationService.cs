using Clipster.Core.Models;

namespace Clipster.Core.Interfaces;

public interface IAnimationService
{
    AnimationState CurrentState { get; }
    event EventHandler<AnimationState>? StateChanged;
    void TransitionTo(AnimationState newState);
    void PlayOnce(AnimationState animation, Action? onComplete = null);
}
