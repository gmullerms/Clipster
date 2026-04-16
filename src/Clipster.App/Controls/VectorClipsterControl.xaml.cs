using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using Clipster.Core.Models;

namespace Clipster.App.Controls;

public partial class VectorClipsterControl : UserControl
{
    private Storyboard? _currentStoryboard;

    public event EventHandler? AnimationCompleted;

    public VectorClipsterControl()
    {
        InitializeComponent();
        Loaded += (_, _) => PlayState(AnimationState.Idle);
    }

    public void PlayState(AnimationState state)
    {
        _currentStoryboard?.Stop(this);
        ResetTransforms();

        var storyboardKey = state switch
        {
            AnimationState.Idle => "IdleAnimation",
            AnimationState.Thinking => "ThinkingAnimation",
            AnimationState.Greeting => "GreetingAnimation",
            AnimationState.Talking => "TalkingAnimation",
            AnimationState.Pointing => "PointingAnimation",
            AnimationState.Confused => "ConfusedAnimation",
            AnimationState.Celebrating => "CelebratingAnimation",
            AnimationState.Looking => "LookingAnimation",
            _ => "IdleAnimation"
        };

        if (Resources[storyboardKey] is Storyboard sb)
        {
            _currentStoryboard = sb;

            // For one-shot animations, listen for completion
            if (state != AnimationState.Idle && state != AnimationState.Thinking && state != AnimationState.Looking)
            {
                EventHandler? handler = null;
                handler = (_, _) =>
                {
                    sb.Completed -= handler;
                    AnimationCompleted?.Invoke(this, EventArgs.Empty);
                };
                sb.Completed += handler;
            }

            sb.Begin(this, true);
        }
    }

    public void StopAnimation()
    {
        _currentStoryboard?.Stop(this);
        ResetTransforms();
    }

    private void ResetTransforms()
    {
        BodyPos.X = 0;
        BodyPos.Y = 0;
        BodyRotation.Angle = 0;
        BodyScale.ScaleX = 1;
        BodyScale.ScaleY = 1;
        LeftPupilPos.X = 0;
        LeftPupilPos.Y = 0;
        RightPupilPos.X = 0;
        RightPupilPos.Y = 0;
        LeftEyeScale.ScaleY = 1;
        RightEyeScale.ScaleY = 1;
        LeftArmRotation.Angle = 0;
        RightArmRotation.Angle = 0;
        LeftBrowPos.Y = 0;
        RightBrowPos.Y = 0;
        MouthScale.ScaleX = 1;
        MouthScale.ScaleY = 0.3; // Default resting smile
        Shadow.Opacity = 0.18;
        Sparkle1.Opacity = 0;
        Sparkle2.Opacity = 0;
        Sparkle3.Opacity = 0;
    }
}
