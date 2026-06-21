using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;

namespace GifGen;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== FlowAuto GIF Generator ===");

        string outputFile = args.Length > 0
            ? args[0]
            : @"d:\AutoScript\FlowAuto\TestScripts\gifs\flowauto_test_demo.gif";

        var outDir = Path.GetDirectoryName(outputFile);
        if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
            Directory.CreateDirectory(outDir!);

        // Generate a demo GIF showing the ball tracking concept
        GenerateBallTrackingGif(outputFile);
    }

    static void GenerateBallTrackingGif(string outputFile)
    {
        int w = 800, h = 400;
        int frameCount = 50;
        int delayMs = 100; // 100ms per frame = 10fps, total ~5 seconds

        // Ball state
        double bx = 100, by = 200;
        double bvx = 4.5, bvy = 3.2;
        int ballR = 22;
        Color ballColor = Color.FromArgb(49, 216, 183);

        Color[] palette = {
            Color.FromArgb(49, 216, 183),   // teal
            Color.FromArgb(255, 107, 107), // red
            Color.FromArgb(255, 217, 61),  // yellow
            Color.FromArgb(107, 203, 119), // green
            Color.FromArgb(77, 150, 255),  // blue
            Color.FromArgb(255, 146, 43),  // orange
            Color.FromArgb(204, 93, 232),  // purple
        };

        var frames = new List<Bitmap>();
        var rand = new Random(42); // Fixed seed for reproducibility

        for (int i = 0; i < frameCount; i++)
        {
            // Update ball position
            bx += bvx;
            by += bvy;

            // Bounce off walls with random energy variation
            if (bx - ballR < 0) { bx = ballR; bvx = Math.Abs(bvx) * (0.75 + rand.NextDouble() * 0.5); }
            if (bx + ballR > w) { bx = w - ballR; bvx = -Math.Abs(bvx) * (0.75 + rand.NextDouble() * 0.5); }
            if (by - ballR < 0) { by = ballR; bvy = Math.Abs(bvy) * (0.75 + rand.NextDouble() * 0.5); }
            if (by + ballR > h) { by = h - ballR; bvy = -Math.Abs(bvy) * (0.75 + rand.NextDouble() * 0.5); }

            // Random perturbation
            bvx += (rand.NextDouble() - 0.5) * 0.6;
            bvy += (rand.NextDouble() - 0.5) * 0.6;

            // Clamp speed
            double spd = Math.Sqrt(bvx * bvx + bvy * bvy);
            if (spd > 8) { bvx = bvx / spd * 8; bvy = bvy / spd * 8; }
            if (spd < 1) { bvx = (rand.NextDouble() - 0.5) * 5; bvy = (rand.NextDouble() - 0.5) * 5; }

            // Change color every ~12 frames
            if (i > 0 && i % 12 == 0)
                ballColor = palette[rand.Next(palette.Length)];

            // Render frame
            var bmp = new Bitmap(w, h, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;

                // Background
                g.Clear(Color.FromArgb(26, 26, 30));

                // Grid pattern (subtle)
                using var gridPen = new Pen(Color.FromArgb(15, 40, 40, 45));
                for (int gx = 0; gx < w; gx += 40)
                    g.DrawLine(gridPen, gx, 0, gx, h);
                for (int gy = 0; gy < h; gy += 40)
                    g.DrawLine(gridPen, 0, gy, w, gy);

                // Ball shadow
                using var shadowBrush = new SolidBrush(Color.FromArgb(40, 0, 0, 0));
                g.FillEllipse(shadowBrush, (float)bx - ballR + 3, (float)by - ballR + 3, ballR * 2, ballR * 2);

                // Ball body with radial gradient
                using var ballBrush = new SolidBrush(ballColor);
                g.FillEllipse(ballBrush, (float)bx - ballR, (float)by - ballR, ballR * 2, ballR * 2);

                // Ball highlight
                using var highlightBrush = new SolidBrush(Color.FromArgb(80, 255, 255, 255));
                g.FillEllipse(highlightBrush, (float)bx - ballR * 0.4f, (float)by - ballR * 0.5f, ballR * 0.7f, ballR * 0.7f);

                // Ball border
                using var borderPen = new Pen(Color.FromArgb(60, 255, 255, 255), 1.5f);
                g.DrawEllipse(borderPen, (float)bx - ballR, (float)by - ballR, ballR * 2, ballR * 2);

                // Crosshair at ball center
                using var crossPen = new Pen(Color.FromArgb(120, 255, 255, 255), 1f);
                crossPen.DashStyle = DashStyle.Dot;
                g.DrawLine(crossPen, (float)bx - ballR - 8, (float)by, (float)bx - 5, (float)by);
                g.DrawLine(crossPen, (float)bx + 5, (float)by, (float)bx + ballR + 8, (float)by);
                g.DrawLine(crossPen, (float)bx, (float)by - ballR - 8, (float)bx, (float)by - 5);
                g.DrawLine(crossPen, (float)bx, (float)by + 5, (float)bx, (float)by + ballR + 8);

                // Title bar at top
                using var titleBgBrush = new SolidBrush(Color.FromArgb(200, 30, 30, 35));
                g.FillRectangle(titleBgBrush, 0, 0, w, 36);
                using var titleFont = new Font("Consolas", 12, FontStyle.Bold);
                using var titleBrush = new SolidBrush(Color.FromArgb(66, 133, 244));
                g.DrawString("🧪 FlowAuto Test Suite — Ball Tracking Demo", titleFont, titleBrush, 10, 8);

                // Status text
                using var infoFont = new Font("Consolas", 10, FontStyle.Regular);
                using var infoBrush = new SolidBrush(Color.FromArgb(180, 180, 180));
                string dirSymbol = GetDirectionSymbol(bvx, bvy);
                g.DrawString($"Position: ({bx:F0}, {by:F0})  |  Speed: {spd:F1} px/s  |  Direction: {dirSymbol}  |  Color: #{ballColor.R:X2}{ballColor.G:X2}{ballColor.B:X2}",
                    infoFont, infoBrush, 10, h - 28);

                // Frame number
                using var frameFont = new Font("Consolas", 9);
                using var frameBrush = new SolidBrush(Color.FromArgb(100, 100, 100));
                g.DrawString($"Frame {i + 1}/{frameCount}", frameFont, frameBrush, w - 120, h - 26);

                // "Target" label near ball
                using var labelFont = new Font("Microsoft YaHei", 8);
                using var labelBrush = new SolidBrush(Color.FromArgb(180, 255, 255, 255));
                g.DrawString("TARGET", labelFont, labelBrush, (float)bx + ballR + 8, (float)by - ballR - 8);

                // Bottom bar
                using var bottomBgBrush = new SolidBrush(Color.FromArgb(200, 30, 30, 35));
                g.FillRectangle(bottomBgBrush, 0, h - 36, w, 36);
                using var bottomFont = new Font("Consolas", 9);
                using var bottomBrush = new SolidBrush(Color.FromArgb(150, 150, 150));
                g.DrawString("Test: ClickElement (Coordinate/TemplateMatch) | ColorMotion (MotionDetect/DirectionDetect) | WaitCondition",
                    bottomFont, bottomBrush, 10, h - 22);
            }

            frames.Add(bmp);
            Console.WriteLine($"  Frame {i + 1}: Ball at ({bx:F0},{by:F0}), speed={spd:F1}, color=#{ballColor.R:X2}{ballColor.G:X2}{ballColor.B:X2}");
        }

        // Save as animated GIF
        SaveAnimatedGif(outputFile, frames, delayMs);
        Console.WriteLine($"✅ GIF created: {outputFile}");
        Console.WriteLine($"   Frames: {frames.Count}, Size: {new FileInfo(outputFile).Length / 1024} KB");

        // Cleanup
        foreach (var f in frames) f.Dispose();
    }

    static string GetDirectionSymbol(double vx, double vy)
    {
        double angle = Math.Atan2(-vy, vx) * 180 / Math.PI;
        if (angle < 0) angle += 360;
        string[] dirs = { "→", "↗", "↑", "↖", "←", "↙", "↓", "↘" };
        int idx = (int)Math.Round(angle / 45) % 8;
        return dirs[idx];
    }

    static void SaveAnimatedGif(string path, List<Bitmap> frames, int delayMs)
    {
        var encoder = GetEncoder(ImageFormat.Gif);
        var encParams = new EncoderParameters(1);

        // Save first frame
        encParams.Param[0] = new EncoderParameter(Encoder.SaveFlag, (long)EncoderValue.MultiFrame);
        frames[0].Save(path, encoder, encParams);

        // Add subsequent frames
        encParams.Param[0] = new EncoderParameter(Encoder.SaveFlag, (long)EncoderValue.FrameDimensionTime);

        for (int i = 1; i < frames.Count; i++)
        {
            // Set frame delay via PropertyItem
            SetFrameDelay(frames[i], delayMs);

            encParams.Param[0] = new EncoderParameter(Encoder.SaveFlag, (long)EncoderValue.FrameDimensionTime);
            frames[0].SaveAdd(frames[i], encParams);
        }

        // Close the multi-frame file
        encParams.Param[0] = new EncoderParameter(Encoder.SaveFlag, (long)EncoderValue.Flush);
        frames[0].SaveAdd(encParams);
    }

    static void SetFrameDelay(Bitmap bmp, int delayMs)
    {
        // GIF delay is in 1/100ths of a second
        short delay = (short)(delayMs / 10);
        byte[] delayBytes = BitConverter.GetBytes(delay);

        var propItem = (PropertyItem)Activator.CreateInstance(
            typeof(PropertyItem), nonPublic: true)!;
        propItem.Id = 0x5100; // PropertyTagFrameDelay
        propItem.Type = 3;    // uint16
        propItem.Len = 2;
        propItem.Value = delayBytes;
        bmp.SetPropertyItem(propItem);
    }

    static ImageCodecInfo GetEncoder(ImageFormat format)
    {
        foreach (var codec in ImageCodecInfo.GetImageEncoders())
            if (codec.FormatID == format.Guid)
                return codec;
        throw new Exception($"No encoder found for {format}");
    }
}
