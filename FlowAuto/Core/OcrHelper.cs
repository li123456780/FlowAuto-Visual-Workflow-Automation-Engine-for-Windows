using System.Drawing.Imaging;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace FlowAuto.Core;

/// <summary>
/// Windows built-in OCR helper (requires Windows 10 18362+).
/// Note: Windows OCR is designed for document text, NOT game UI.
/// For game UI text recognition, Template Matching is strongly recommended.
/// </summary>
public static class OcrHelper
{
    private static readonly Lazy<(OcrEngine? Engine, string Language)> _engineLazy = new(() =>
    {
        // Try zh-Hans first for Chinese text recognition
        var zhLang = new Windows.Globalization.Language("zh-Hans");
        if (OcrEngine.IsLanguageSupported(zhLang))
            return (OcrEngine.TryCreateFromLanguage(zhLang), "zh-Hans");

        var userLang = OcrEngine.TryCreateFromUserProfileLanguages();
        if (userLang != null)
            return (userLang, "user-profile");

        var enEngine = OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("en-US"));
        return (enEngine, "en-US");
    });

    private static OcrEngine? Engine => _engineLazy.Value.Engine;
    public static string ActiveLanguage => _engineLazy.Value.Language;

    /// <summary>
    /// Search for text in a bitmap using Windows OCR.
    /// Returns the center point of the found text, or null.
    /// </summary>
    /// <param name="debugSavePath">If set, saves the preprocessed image for diagnosis.</param>
    /// <param name="allRecognizedText">Receives all OCR-recognized text for debugging.</param>
    public static Point? FindText(Bitmap bitmap, string searchText,
        out string? allRecognizedText, string? debugSavePath = null)
    {
        allRecognizedText = null;

        if (Engine == null)
            throw new InvalidOperationException(
                $"OCR engine not available. Active language: {ActiveLanguage}. " +
                "For Chinese text, install the Chinese (Simplified) OCR language pack " +
                "via Windows Settings → Time & Language → Language & region → " +
                "Add a language → 中文(简体) → Optional features → OCR.");

        var softwareBitmap = ConvertToSoftwareBitmap(bitmap);
        if (softwareBitmap == null)
            return null;

        try
        {
            var result = Engine.RecognizeAsync(softwareBitmap).GetAwaiter().GetResult();

            // Collect ALL recognized text for debugging
            var allLines = new List<string>();
            foreach (var line in result.Lines)
            {
                allLines.Add(line.Text);
            }
            allRecognizedText = allLines.Count > 0
                ? string.Join(" | ", allLines)
                : "(no text recognized)";

            // Save debug image if requested
            if (debugSavePath != null)
            {
                try
                {
                    var dir = Path.GetDirectoryName(debugSavePath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                    bitmap.Save(debugSavePath, ImageFormat.Png);
                }
                catch { /* ignore debug save errors */ }
            }

            // Step 1: Word-level search (best positioning for single words)
            foreach (var line in result.Lines)
            {
                foreach (var word in line.Words)
                {
                    if (word.Text.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                    {
                        int cx = (int)(word.BoundingRect.X + word.BoundingRect.Width / 2);
                        int cy = (int)(word.BoundingRect.Y + word.BoundingRect.Height / 2);
                        return new Point(cx, cy);
                    }
                }
            }

            // Step 2: Line-level fallback (for multi-word phrases)
            foreach (var line in result.Lines)
            {
                if (line.Text.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                {
                    int cx = line.Words.Count > 0
                        ? (int)((line.Words[0].BoundingRect.X + line.Words[^1].BoundingRect.X + line.Words[^1].BoundingRect.Width) / 2)
                        : 0;
                    int cy = line.Words.Count > 0
                        ? (int)(line.Words[0].BoundingRect.Y + line.Words[0].BoundingRect.Height / 2)
                        : 0;
                    return new Point(cx, cy);
                }
            }
        }
        finally
        {
            softwareBitmap.Dispose();
        }

        return null;
    }

    /// <summary>
    /// Convert a GDI+ Bitmap to a Windows SoftwareBitmap for OCR.
    /// Preserves original color — Windows OCR handles colored images better than grayscale.
    /// </summary>
    private static SoftwareBitmap? ConvertToSoftwareBitmap(Bitmap bitmap)
    {
        // IMPORTANT: Do NOT convert to grayscale.
        // Windows OCR is trained on color document images and works better with
        // original color data, especially for game UI where text color contrast
        // against background is critical. Grayscale conversion can destroy
        // the only contrast signal available.

        // Ensure the bitmap is in a format compatible with BitmapDecoder
        Bitmap safeBitmap;
        if (bitmap.PixelFormat == PixelFormat.Format24bppRgb ||
            bitmap.PixelFormat == PixelFormat.Format32bppRgb ||
            bitmap.PixelFormat == PixelFormat.Format32bppArgb)
        {
            safeBitmap = bitmap;
        }
        else
        {
            // Convert to 24bpp RGB if in an unsupported format
            safeBitmap = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format24bppRgb);
            using var g = Graphics.FromImage(safeBitmap);
            g.DrawImage(bitmap, new Rectangle(0, 0, safeBitmap.Width, safeBitmap.Height));
        }

        using var ms = new MemoryStream();
        safeBitmap.Save(ms, ImageFormat.Bmp);
        ms.Seek(0, SeekOrigin.Begin);

        var randomAccessStream = ms.AsRandomAccessStream();
        var decoder = BitmapDecoder.CreateAsync(randomAccessStream).GetAwaiter().GetResult();

        return decoder.GetSoftwareBitmapAsync().GetAwaiter().GetResult();
    }
}
