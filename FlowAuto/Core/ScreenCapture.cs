using System.Drawing.Imaging;

namespace FlowAuto.Core;

public static class ScreenCapture
{
    /// <summary>
    /// Capture the client area of a window.
    /// </summary>
    public static Bitmap? CaptureWindow(IntPtr hWnd)
    {
        var (clientLeft, clientTop, clientWidth, clientHeight) = WindowHelper.GetClientBounds(hWnd);

        if (clientWidth <= 0 || clientHeight <= 0) return null;

        var bmp = new Bitmap(clientWidth, clientHeight, PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(bmp);
        g.CopyFromScreen(clientLeft, clientTop, 0, 0, new Size(clientWidth, clientHeight));

        return bmp;
    }

    /// <summary>
    /// Capture a specific screen region.
    /// </summary>
    public static Bitmap CaptureRegion(int screenX, int screenY, int width, int height)
    {
        var bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(bmp);
        g.CopyFromScreen(screenX, screenY, 0, 0, new Size(width, height));
        return bmp;
    }

    /// <summary>
    /// Capture a region relative to the window client area.
    /// </summary>
    public static Bitmap? CaptureWindowRegion(IntPtr hWnd, int regionX, int regionY, int regionWidth, int regionHeight)
    {
        var (clientLeft, clientTop, clientWidth, clientHeight) = WindowHelper.GetClientBounds(hWnd);

        // Clamp region to client bounds
        int screenX = clientLeft + regionX;
        int screenY = clientTop + regionY;
        int w = Math.Min(regionWidth, clientWidth - regionX);
        int h = Math.Min(regionHeight, clientHeight - regionY);

        if (w <= 0 || h <= 0) return null;

        return CaptureRegion(screenX, screenY, w, h);
    }

    /// <summary>
    /// Save a bitmap to file.
    /// </summary>
    public static void SaveBitmap(Bitmap bmp, string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        bmp.Save(filePath, ImageFormat.Png);
    }
}
