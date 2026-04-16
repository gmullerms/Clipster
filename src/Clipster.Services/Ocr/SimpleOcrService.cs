using Clipster.Core.Interfaces;
using Clipster.Core.Models;

namespace Clipster.Services.Ocr;

/// <summary>
/// Lightweight OCR service. Since we send screenshots to GPT-4o vision
/// which has its own OCR capability, this provides supplementary text extraction.
/// Falls back gracefully if OCR is unavailable.
/// </summary>
public class SimpleOcrService : IOcrService
{
    public Task<OcrResult> RecognizeAsync(byte[] imagePng, CancellationToken ct = default)
    {
        // GPT-4o vision handles the actual text recognition from the screenshot.
        // This service acts as a passthrough -- the image is the primary input.
        // We return an empty result; the AI service will use the image directly.
        return Task.FromResult(new OcrResult
        {
            Text = string.Empty,
            Confidence = 0f
        });
    }
}
