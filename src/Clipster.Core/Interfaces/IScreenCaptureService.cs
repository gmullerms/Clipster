namespace Clipster.Core.Interfaces;

public interface IScreenCaptureService
{
    byte[] CaptureFullScreen();
    byte[] CaptureRegion(int x, int y, int width, int height);
}
