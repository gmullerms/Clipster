using Clipster.Core.Models;

namespace Clipster.Core.Interfaces;

public interface IOcrService
{
    Task<OcrResult> RecognizeAsync(byte[] imagePng, CancellationToken ct = default);
}
