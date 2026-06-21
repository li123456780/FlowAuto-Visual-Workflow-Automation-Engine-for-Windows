using OpenCvSharp;
using OpenCvSharp.Extensions;
using Point = System.Drawing.Point;
using Size = OpenCvSharp.Size;

namespace FlowAuto.Core;

public static class ImageRecognition
{
    // Cache for template Mat objects to avoid repeated Bitmap->Mat conversion
    private static readonly Dictionary<string, Mat> TemplateCache = new();
    private static readonly object CacheLock = new();

    /// <summary>
    /// Load a template Mat from file path, with caching.
    /// </summary>
    public static Mat LoadTemplate(string imagePath)
    {
        lock (CacheLock)
        {
            if (TemplateCache.TryGetValue(imagePath, out var cached))
                return cached.Clone();

            var mat = Cv2.ImRead(imagePath, ImreadModes.Color);
            if (mat.Empty())
                throw new FileNotFoundException($"Template image not found or failed to load: {imagePath}");

            TemplateCache[imagePath] = mat.Clone();
            return mat;
        }
    }

    /// <summary>
    /// Clear the template cache.
    /// </summary>
    public static void ClearCache()
    {
        lock (CacheLock)
        {
            foreach (var mat in TemplateCache.Values)
                mat.Dispose();
            TemplateCache.Clear();
        }
    }

    /// <summary>
    /// Multi-scale template matching.
    /// Returns the center point (relative to the source image) if match found, or null.
    /// </summary>
    public static (Point point, double confidence)? FindTemplate(
        Bitmap screenSource, Mat templateMat,
        double minScale = 0.5, double maxScale = 1.5, double step = 0.1,
        double threshold = 0.8)
    {
        using var refMat = BitmapConverter.ToMat(screenSource);

        double bestMaxVal = 0;
        OpenCvSharp.Point bestLoc = default;
        double bestScale = 1.0;
        int bestTplW = templateMat.Cols;
        int bestTplH = templateMat.Rows;

        // Try original size first
        if (TryMatch(refMat, templateMat, out double maxVal, out OpenCvSharp.Point maxLoc))
        {
            if (maxVal > threshold)
            {
                return (new Point(maxLoc.X + templateMat.Cols / 2, maxLoc.Y + templateMat.Rows / 2), maxVal);
            }

            if (maxVal > bestMaxVal)
            {
                bestMaxVal = maxVal;
                bestLoc = maxLoc;
                bestScale = 1.0;
                bestTplW = templateMat.Cols;
                bestTplH = templateMat.Rows;
            }
        }

        // Multi-scale search
        for (double scale = minScale; scale <= maxScale + 1e-6; scale += step)
        {
            if (Math.Abs(scale - 1.0) < 1e-6) continue; // already tried

            int newW = (int)(templateMat.Cols * scale);
            int newH = (int)(templateMat.Rows * scale);

            // Skip if scaled template is larger than reference
            if (newW > refMat.Cols || newH > refMat.Rows) continue;
            if (newW <= 0 || newH <= 0) continue;

            using var resizedMat = new Mat();
            Cv2.Resize(templateMat, resizedMat, new Size(newW, newH));

            if (TryMatch(refMat, resizedMat, out maxVal, out maxLoc))
            {
                if (maxVal > bestMaxVal)
                {
                    bestMaxVal = maxVal;
                    bestLoc = maxLoc;
                    bestScale = scale;
                    bestTplW = newW;
                    bestTplH = newH;
                }

                if (maxVal > threshold)
                    break;
            }
        }

        if (bestMaxVal > threshold)
        {
            int centerX = bestLoc.X + bestTplW / 2;
            int centerY = bestLoc.Y + bestTplH / 2;
            return (new Point(centerX, centerY), bestMaxVal);
        }

        return null;
    }

    private static bool TryMatch(Mat refMat, Mat tplMat, out double maxVal, out OpenCvSharp.Point maxLoc)
    {
        maxVal = 0;
        maxLoc = default;

        if (tplMat.Width > refMat.Width || tplMat.Height > refMat.Height)
            return false;

        using var resultMat = new Mat();
        Cv2.MatchTemplate(refMat, tplMat, resultMat, TemplateMatchModes.CCoeffNormed);
        Cv2.MinMaxLoc(resultMat, out _, out maxVal, out _, out maxLoc);
        return true;
    }

    /// <summary>
    /// Multi-scale template matching within a single ROI.
    /// Returns the best match across all scales (even below threshold).
    /// </summary>
    private static bool TryMatchMultiScale(Mat roi, Mat templateMat,
        double minScale, double maxScale, double step,
        out double bestMaxVal, out OpenCvSharp.Point bestLoc,
        out int bestTplW, out int bestTplH)
    {
        bestMaxVal = 0;
        bestLoc = default;
        bestTplW = templateMat.Cols;
        bestTplH = templateMat.Rows;

        // Original size
        if (TryMatch(roi, templateMat, out double maxVal, out OpenCvSharp.Point maxLoc))
        {
            if (maxVal > bestMaxVal)
            {
                bestMaxVal = maxVal;
                bestLoc = maxLoc;
                bestTplW = templateMat.Cols;
                bestTplH = templateMat.Rows;
            }
        }

        // Scaled sizes
        for (double scale = minScale; scale <= maxScale + 1e-6; scale += step)
        {
            if (Math.Abs(scale - 1.0) < 1e-6) continue;
            int newW = (int)(templateMat.Cols * scale);
            int newH = (int)(templateMat.Rows * scale);
            if (newW > roi.Cols || newH > roi.Rows) continue;
            if (newW <= 0 || newH <= 0) continue;

            using var resizedMat = new Mat();
            Cv2.Resize(templateMat, resizedMat, new Size(newW, newH));

            if (TryMatch(roi, resizedMat, out maxVal, out maxLoc))
            {
                if (maxVal > bestMaxVal)
                {
                    bestMaxVal = maxVal;
                    bestLoc = maxLoc;
                    bestTplW = newW;
                    bestTplH = newH;
                }
            }
        }

        return bestMaxVal > 0;
    }

    /// <summary>
    /// Detect a color center using HSV color space thresholding.
    /// Returns the center point (relative to the source image) or null.
    /// </summary>
    public static Point? DetectColorCenter(Bitmap screenFrame, Color targetRgb, int hueTolerance = 5, int svTolerance = 10)
    {
        using var frame = BitmapConverter.ToMat(screenFrame);
        using var hsvMat = new Mat();
        Cv2.CvtColor(frame, hsvMat, ColorConversionCodes.BGR2HSV);

        var (h, s, v) = RgbToHsv(targetRgb);

        var lower = new Scalar(
            Math.Max(0, h - hueTolerance),
            Math.Max(0, s - svTolerance),
            Math.Max(0, v - svTolerance));

        var upper = new Scalar(
            Math.Min(179, h + hueTolerance),
            Math.Min(255, s + svTolerance),
            Math.Min(255, v + svTolerance));

        using var mask = new Mat();
        Cv2.InRange(hsvMat, lower, upper, mask);

        // Morphological close to fill gaps
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(20, 15));
        Cv2.MorphologyEx(mask, mask, MorphTypes.Close, kernel);

        Cv2.FindContours(mask, out OpenCvSharp.Point[][] contours, out HierarchyIndex[] hierarchy,
            RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        if (contours.Length == 0) return null;

        var largestContour = contours.OrderByDescending(c => Cv2.ContourArea(c)).First();
        var rect = Cv2.BoundingRect(largestContour);

        return new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
    }

    /// <summary>
    /// Apply HSV color filter to isolate regions of a target color, then perform template matching
    /// within those isolated regions. Returns the best match center point and confidence, or null.
    /// This combines Steps 3-5 (HSV filtering + morphology + contour detection) with template matching.
    /// </summary>
    public static (Point point, double confidence)? FindTemplateWithColorFilter(
        Bitmap screenFrame, Mat templateMat,
        Color targetRgb, int hueTolerance = 5, int svTolerance = 10,
        double templateThreshold = 0.8)
    {
        using var frame = BitmapConverter.ToMat(screenFrame);
        using var hsvMat = new Mat();
        Cv2.CvtColor(frame, hsvMat, ColorConversionCodes.BGR2HSV);

        var (h, s, v) = RgbToHsv(targetRgb);

        var lower = new Scalar(
            Math.Max(0, h - hueTolerance),
            Math.Max(0, s - svTolerance),
            Math.Max(0, v - svTolerance));

        var upper = new Scalar(
            Math.Min(179, h + hueTolerance),
            Math.Min(255, s + svTolerance),
            Math.Min(255, v + svTolerance));

        using var mask = new Mat();
        Cv2.InRange(hsvMat, lower, upper, mask);

        // Morphological close to connect broken regions
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(20, 15));
        Cv2.MorphologyEx(mask, mask, MorphTypes.Close, kernel);

        // Build a filtered frame where only matching-color pixels are kept (rest black)
        using var filteredFrame = new Mat();
        frame.CopyTo(filteredFrame, mask);

        // Find contours, and for each bounding rect, run template matching on filtered frame
        Cv2.FindContours(mask, out OpenCvSharp.Point[][] contours, out HierarchyIndex[] hierarchy,
            RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        if (contours.Length == 0) return null;

        double bestMaxVal = 0;
        OpenCvSharp.Point bestLoc = default;
        int bestRectW = 0, bestRectH = 0;
        int bestRectX = 0, bestRectY = 0;
        int bestMatchedTplW = templateMat.Cols;
        int bestMatchedTplH = templateMat.Rows;
        const double msMin = 0.5, msMax = 1.5, msStep = 0.05;
        int minTplW = (int)(templateMat.Cols * msMin);
        int minTplH = (int)(templateMat.Rows * msMin);

        // Try matching in each color-filtered contour's bounding rect (multi-scale)
        foreach (var contour in contours)
        {
            var rect = Cv2.BoundingRect(contour);
            if (rect.Width < minTplW || rect.Height < minTplH) continue;

            using var roi = new Mat(filteredFrame, rect);
            if (roi.Empty()) continue;

            if (!TryMatchMultiScale(roi, templateMat, msMin, msMax, msStep,
                out double maxVal, out OpenCvSharp.Point maxLoc, out int matchedTplW, out int matchedTplH)) continue;

            if (maxVal > bestMaxVal)
            {
                bestMaxVal = maxVal;
                bestLoc = maxLoc;
                bestRectX = rect.X;
                bestRectY = rect.Y;
                bestRectW = rect.Width;
                bestRectH = rect.Height;
                bestMatchedTplW = matchedTplW;
                bestMatchedTplH = matchedTplH;
            }

            if (maxVal > templateThreshold)
                break; // good enough
        }

        if (bestMaxVal > templateThreshold)
        {
            int centerX = bestRectX + bestLoc.X + bestMatchedTplW / 2;
            int centerY = bestRectY + bestLoc.Y + bestMatchedTplH / 2;
            return (new Point(centerX, centerY), bestMaxVal);
        }

        return null;
    }

    /// <summary>
    /// Detect multiple color centers using HSV filtering.
    /// Returns up to maxTargets center points, sorted by contour area (largest first).
    /// </summary>
    public static List<Point> DetectMultipleColorCenters(Bitmap screenFrame, Color targetRgb, int hueTolerance = 5, int svTolerance = 10, int maxTargets = 5)
    {
        var results = new List<Point>();
        using var frame = BitmapConverter.ToMat(screenFrame);
        using var hsvMat = new Mat();
        Cv2.CvtColor(frame, hsvMat, ColorConversionCodes.BGR2HSV);

        var (h, s, v) = RgbToHsv(targetRgb);

        var lower = new Scalar(
            Math.Max(0, h - hueTolerance),
            Math.Max(0, s - svTolerance),
            Math.Max(0, v - svTolerance));

        var upper = new Scalar(
            Math.Min(179, h + hueTolerance),
            Math.Min(255, s + svTolerance),
            Math.Min(255, v + svTolerance));

        using var mask = new Mat();
        Cv2.InRange(hsvMat, lower, upper, mask);

        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(20, 15));
        Cv2.MorphologyEx(mask, mask, MorphTypes.Close, kernel);

        Cv2.FindContours(mask, out OpenCvSharp.Point[][] contours, out HierarchyIndex[] hierarchy,
            RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        if (contours.Length == 0) return results;

        // Sort by contour area descending and take up to maxTargets
        var sortedContours = contours
            .Select(c => new { Contour = c, Area = Cv2.ContourArea(c) })
            .Where(x => x.Area > 50) // Filter out noise
            .OrderByDescending(x => x.Area)
            .Take(maxTargets);

        foreach (var item in sortedContours)
        {
            var rect = Cv2.BoundingRect(item.Contour);
            results.Add(new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2));
        }

        return results;
    }

    /// <summary>
    /// Detect multiple targets using HSV color filter + template matching.
    /// Returns up to maxTargets center points.
    /// </summary>
    public static List<Point> DetectMultipleTargets(Bitmap screenFrame, Mat templateMat, Color targetRgb, int hueTolerance = 5, int svTolerance = 10, double templateThreshold = 0.8, int maxTargets = 5)
    {
        var results = new List<Point>();
        using var frame = BitmapConverter.ToMat(screenFrame);
        using var hsvMat = new Mat();
        Cv2.CvtColor(frame, hsvMat, ColorConversionCodes.BGR2HSV);

        var (h, s, v) = RgbToHsv(targetRgb);

        var lower = new Scalar(
            Math.Max(0, h - hueTolerance),
            Math.Max(0, s - svTolerance),
            Math.Max(0, v - svTolerance));

        var upper = new Scalar(
            Math.Min(179, h + hueTolerance),
            Math.Min(255, s + svTolerance),
            Math.Min(255, v + svTolerance));

        using var mask = new Mat();
        Cv2.InRange(hsvMat, lower, upper, mask);

        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(20, 15));
        Cv2.MorphologyEx(mask, mask, MorphTypes.Close, kernel);

        // Build a filtered frame where only matching-color pixels are kept (rest black)
        using var filteredFrame = new Mat();
        frame.CopyTo(filteredFrame, mask);

        Cv2.FindContours(mask, out OpenCvSharp.Point[][] contours, out HierarchyIndex[] hierarchy,
            RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        if (contours.Length == 0) return results;

        var matches = new List<(Point point, double confidence, double area)>();
        const double msMin = 0.5, msMax = 1.5, msStep = 0.05;
        int minTplW = (int)(templateMat.Cols * msMin);
        int minTplH = (int)(templateMat.Rows * msMin);

        foreach (var contour in contours)
        {
            var rect = Cv2.BoundingRect(contour);
            if (rect.Width < minTplW || rect.Height < minTplH) continue;

            using var roi = new Mat(filteredFrame, rect);
            if (roi.Empty()) continue;

            if (!TryMatchMultiScale(roi, templateMat, msMin, msMax, msStep,
                out double maxVal, out OpenCvSharp.Point maxLoc, out int matchedTplW, out int matchedTplH)) continue;

            if (maxVal > templateThreshold)
            {
                int centerX = rect.X + maxLoc.X + matchedTplW / 2;
                int centerY = rect.Y + maxLoc.Y + matchedTplH / 2;
                matches.Add((new Point(centerX, centerY), maxVal, Cv2.ContourArea(contour)));
            }
        }

        // Sort by confidence descending, then by area, take up to maxTargets
        return matches
            .OrderByDescending(m => m.confidence)
            .ThenByDescending(m => m.area)
            .Take(maxTargets)
            .Select(m => m.point)
            .ToList();
    }

    /// <summary>
    /// Calculate the ratio of pixels matching the target color within the frame.
    /// Returns 0.0 to 1.0.
    /// </summary>
    public static double CalculateColorFillRatio(Bitmap screenFrame, Color targetRgb, int hueTolerance = 5, int svTolerance = 10)
    {
        using var frame = BitmapConverter.ToMat(screenFrame);
        using var hsvMat = new Mat();
        Cv2.CvtColor(frame, hsvMat, ColorConversionCodes.BGR2HSV);

        var (h, s, v) = RgbToHsv(targetRgb);

        var lower = new Scalar(
            Math.Max(0, h - hueTolerance),
            Math.Max(0, s - svTolerance),
            Math.Max(0, v - svTolerance));

        var upper = new Scalar(
            Math.Min(179, h + hueTolerance),
            Math.Min(255, s + svTolerance),
            Math.Min(255, v + svTolerance));

        using var mask = new Mat();
        Cv2.InRange(hsvMat, lower, upper, mask);

        double matchingPixels = Cv2.CountNonZero(mask);
        double totalPixels = mask.Rows * mask.Cols;

        return totalPixels > 0 ? matchingPixels / totalPixels : 0.0;
    }

    /// <summary>
    /// Apply HSV color filter to a Bitmap, keeping only pixels matching the target color.
    /// Non-matching pixels become black. Useful for creating clean reference images
    /// for ColorMotion snip.
    /// </summary>
    public static Bitmap ApplyHsvFilter(Bitmap source, Color targetRgb, int hueTolerance = 5, int svTolerance = 10)
    {
        using var frame = BitmapConverter.ToMat(source);
        using var hsvMat = new Mat();
        Cv2.CvtColor(frame, hsvMat, ColorConversionCodes.BGR2HSV);

        // Use OpenCV itself to convert the target colour so channel order and
        // rounding are guaranteed identical to the image conversion.
        using var targetMat = new Mat(1, 1, MatType.CV_8UC3);
        targetMat.SetTo(new Scalar(targetRgb.B, targetRgb.G, targetRgb.R));
        using var targetHsv = new Mat();
        Cv2.CvtColor(targetMat, targetHsv, ColorConversionCodes.BGR2HSV);
        var targetHsvData = targetHsv.Get<Vec3b>(0, 0);
        double h = targetHsvData.Item0;
        double s = targetHsvData.Item1;
        double v = targetHsvData.Item2;

        var lower = new Scalar(
            Math.Max(0, h - hueTolerance),
            Math.Max(0, s - svTolerance),
            Math.Max(0, v - svTolerance));
        var upper = new Scalar(
            Math.Min(179, h + hueTolerance),
            Math.Min(255, s + svTolerance),
            Math.Min(255, v + svTolerance));

        using var mask = new Mat();
        Cv2.InRange(hsvMat, lower, upper, mask);

        var result = new Mat(frame.Size(), MatType.CV_8UC3, Scalar.Black);
        frame.CopyTo(result, mask);
        return BitmapConverter.ToBitmap(result);
    }

    private static (double h, double s, double v) RgbToHsv(Color c)
    {
        double r = c.R / 255.0;
        double g = c.G / 255.0;
        double b = c.B / 255.0;

        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;

        double h = 0;
        if (delta > 1e-6)
        {
            if (Math.Abs(max - r) < 1e-6)
                h = 60 * (((g - b) / delta) % 6);
            else if (Math.Abs(max - g) < 1e-6)
                h = 60 * ((b - r) / delta + 2);
            else
                h = 60 * ((r - g) / delta + 4);
        }
        if (h < 0) h += 360;

        double s = max > 1e-6 ? delta / max : 0;
        double v = max;

        // OpenCV HSV: H(0-179), S(0-255), V(0-255)
        return (h / 2.0, s * 255, v * 255);
    }
}
