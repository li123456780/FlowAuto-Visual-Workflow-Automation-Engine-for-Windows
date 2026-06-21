using FlowAuto.Core;
using System.Drawing;

namespace FlowAuto;

public class ScreenshotOverlay : Form
{
    private readonly Action<string> _onScreenshotSaved;
    private Point _startPoint;
    private Point _endPoint;
    private bool _isSelecting;
    private Rectangle _selectedRect;

    // ── Optional HSV filter for ColorMotion snip ──
    private readonly Color? _hsvTargetColor;
    private readonly int _hsvHueTolerance;
    private readonly int _hsvSvTolerance;

    /// <summary>
    /// Create a screenshot overlay. If hsvTargetColor is provided, the captured image
    /// will be HSV-filtered so only the target color's shape remains.
    /// </summary>
    public ScreenshotOverlay(Action<string> onScreenshotSaved,
        Color? hsvTargetColor = null, int hsvHueTolerance = 8, int hsvSvTolerance = 30)
    {
        _onScreenshotSaved = onScreenshotSaved;
        _hsvTargetColor = hsvTargetColor;
        _hsvHueTolerance = hsvHueTolerance;
        _hsvSvTolerance = hsvSvTolerance;

        FormBorderStyle = FormBorderStyle.None;
        WindowState = FormWindowState.Maximized;
        TopMost = true;
        ShowInTaskbar = false;
        BackColor = Color.Black;
        Opacity = 0.5;
        Cursor = Cursors.Cross;

        DoubleBuffered = true;
        KeyPreview = true;

        KeyDown += OnKeyDown;
        MouseDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseUp += OnMouseUp;
        Paint += OnPaint;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            Close();
        }
    }

    private void OnMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _startPoint = e.Location;
            _isSelecting = true;
        }
    }

    private void OnMouseMove(object? sender, MouseEventArgs e)
    {
        if (_isSelecting)
        {
            _endPoint = e.Location;
            Invalidate();
        }
    }

    private void OnMouseUp(object? sender, MouseEventArgs e)
    {
        if (_isSelecting && e.Button == MouseButtons.Left)
        {
            _isSelecting = false;
            _endPoint = e.Location;

            _selectedRect = new Rectangle(
                Math.Min(_startPoint.X, _endPoint.X),
                Math.Min(_startPoint.Y, _endPoint.Y),
                Math.Abs(_endPoint.X - _startPoint.X),
                Math.Abs(_endPoint.Y - _startPoint.Y));

            if (_selectedRect.Width > 10 && _selectedRect.Height > 10)
            {
                // Hide overlay before capturing so the semi-transparent black layer
                // doesn't darken the underlying screen content.
                this.Visible = false;
                SaveScreenshot();
            }
            Close();
        }
    }

    private void OnPaint(object? sender, PaintEventArgs e)
    {
        if (_isSelecting)
        {
            var rect = new Rectangle(
                Math.Min(_startPoint.X, _endPoint.X),
                Math.Min(_startPoint.Y, _endPoint.Y),
                Math.Abs(_endPoint.X - _startPoint.X),
                Math.Abs(_endPoint.Y - _startPoint.Y));

            using var pen = new Pen(Color.Lime, 2);
            e.Graphics.DrawRectangle(pen, rect);

            // Size info
            using var font = new Font("Consolas", 10);
            using var brush = new SolidBrush(Color.Lime);
            string info = $"{rect.Width} x {rect.Height}";
            e.Graphics.DrawString(info, font, brush, rect.X, rect.Y - 20);
        }
    }

    private void SaveScreenshot()
    {
        try
        {
            // Ensure templates directory exists
            string templatesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates");
            Directory.CreateDirectory(templatesDir);

            string fileName = $"snip_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            string filePath = Path.Combine(templatesDir, fileName);

            using var bmp = ScreenCapture.CaptureRegion(
                _selectedRect.X, _selectedRect.Y,
                _selectedRect.Width, _selectedRect.Height);

            // ── Apply HSV filter for ColorMotion snip ──
            if (_hsvTargetColor.HasValue)
            {
                using var filtered = ImageRecognition.ApplyHsvFilter(
                    bmp, _hsvTargetColor.Value, _hsvHueTolerance, _hsvSvTolerance);
                ScreenCapture.SaveBitmap(filtered, filePath);
            }
            else
            {
                ScreenCapture.SaveBitmap(bmp, filePath);
            }

            _onScreenshotSaved(filePath);
        }
        catch (Exception)
        {
            // Silently fail - user can try again
        }
    }
}
