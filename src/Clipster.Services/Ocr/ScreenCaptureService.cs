using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Clipster.Core.Interfaces;

namespace Clipster.Services.Ocr;

public class ScreenCaptureService : IScreenCaptureService
{
    public byte[] CaptureFullScreen()
    {
        // Use primary screen dimensions via WPF SystemParameters
        var width = (int)System.Windows.SystemParameters.PrimaryScreenWidth;
        var height = (int)System.Windows.SystemParameters.PrimaryScreenHeight;
        return CaptureRegion(0, 0, width, height);
    }

    public byte[] CaptureRegion(int x, int y, int width, int height)
    {
        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(x, y, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);

        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }
}
