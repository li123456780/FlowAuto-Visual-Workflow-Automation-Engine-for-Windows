using System.Drawing.Imaging;
using FlowAuto.Core;

namespace FlowAuto;

/// <summary>
/// Visual color picker tool for HSV-based nodes (ColorMotion, ColorCal, Condition).
/// Captures screen pixel color under cursor and computes HSV parameters.
/// </summary>
public class ColorPickerForm : Form
{
    private readonly Action<Color, int, int>? _onColorPicked;

    // Display
    private Panel _colorPreview = null!;
    private Label _lblRgb = null!;
    private Label _lblHsv = null!;
    private Label _lblHex = null!;
    private Label _lblPos = null!;

    // Controls
    private TrackBar _trackHueTol = null!;
    private TrackBar _trackSvTol = null!;
    private Label _lblHueTolVal = null!;
    private Label _lblSvTolVal = null!;
    private Button _btnPick = null!;
    private Button _btnOk = null!;
    private Button _btnCancel = null!;

    // Picked state
    private Color _pickedColor = Color.FromArgb(49, 218, 183);
    private int _hueTolerance = 8;
    private int _svTolerance = 30;
    private bool _isPicking;

    public ColorPickerForm(Action<Color, int, int>? onColorPicked = null)
    {
        _onColorPicked = onColorPicked;
        Text = "Color Picker - HSV Configuration";
        Size = new Size(420, 480);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(37, 37, 42);
        ForeColor = Color.White;
        KeyPreview = true;

        InitializeControls();

        KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Escape) Close();
        };
    }

    private void InitializeControls()
    {
        int y = 12;

        // Title
        var title = new Label
        {
            Text = "HSV Color Picker",
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(16, y),
            AutoSize = true
        };
        Controls.Add(title);
        y += 36;

        // Color preview panel
        _colorPreview = new Panel
        {
            Location = new Point(16, y),
            Size = new Size(120, 80),
            BackColor = _pickedColor,
            BorderStyle = BorderStyle.FixedSingle
        };
        Controls.Add(_colorPreview);

        // Info labels
        var infoX = 152;
        _lblRgb = new Label
        {
            Text = $"RGB: ({_pickedColor.R}, {_pickedColor.G}, {_pickedColor.B})",
            Location = new Point(infoX, y),
            ForeColor = Color.White,
            AutoSize = true,
            Font = new Font("Consolas", 10)
        };
        Controls.Add(_lblRgb);

        _lblHsv = new Label
        {
            Text = GetHsvText(_pickedColor),
            Location = new Point(infoX, y + 24),
            ForeColor = Color.FromArgb(200, 200, 200),
            AutoSize = true,
            Font = new Font("Consolas", 10)
        };
        Controls.Add(_lblHsv);

        _lblHex = new Label
        {
            Text = $"HEX: #{_pickedColor.R:X2}{_pickedColor.G:X2}{_pickedColor.B:X2}",
            Location = new Point(infoX, y + 48),
            ForeColor = Color.FromArgb(180, 180, 180),
            AutoSize = true,
            Font = new Font("Consolas", 9)
        };
        Controls.Add(_lblHex);
        y += 90;

        // Position label
        _lblPos = new Label
        {
            Text = "Click 'Pick Color' then click anywhere on screen",
            Location = new Point(16, y),
            ForeColor = Color.FromArgb(150, 150, 150),
            AutoSize = true,
            Font = new Font("Segoe UI", 8)
        };
        Controls.Add(_lblPos);
        y += 28;

        // Separator
        var sep = new Label
        {
            Text = "",
            Height = 1,
            BackColor = Color.FromArgb(60, 60, 65),
            Location = new Point(16, y),
            Size = new Size(372, 1)
        };
        Controls.Add(sep);
        y += 16;

        // Pick button
        _btnPick = new Button
        {
            Text = "🎯 Pick Color from Screen",
            Location = new Point(16, y),
            Size = new Size(200, 36),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(66, 133, 244),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Cursor = Cursors.Cross
        };
        _btnPick.FlatAppearance.BorderSize = 0;
        _btnPick.Click += OnPickColorClick;
        Controls.Add(_btnPick);
        y += 48;

        // Hue Tolerance
        var lblHT = new Label
        {
            Text = "Hue Tolerance:",
            Location = new Point(16, y),
            ForeColor = Color.White,
            AutoSize = true
        };
        Controls.Add(lblHT);

        _lblHueTolVal = new Label
        {
            Text = _hueTolerance.ToString(),
            Location = new Point(370, y),
            ForeColor = Color.FromArgb(66, 133, 244),
            AutoSize = true,
            Font = new Font("Segoe UI", 10, FontStyle.Bold)
        };
        Controls.Add(_lblHueTolVal);
        y += 22;

        _trackHueTol = new TrackBar
        {
            Location = new Point(12, y),
            Size = new Size(380, 30),
            Minimum = 1,
            Maximum = 60,
            Value = _hueTolerance,
            TickFrequency = 5,
            BackColor = Color.FromArgb(37, 37, 42)
        };
        _trackHueTol.ValueChanged += (s, e) =>
        {
            _hueTolerance = _trackHueTol.Value;
            _lblHueTolVal.Text = _hueTolerance.ToString();
            UpdateColorPreview();
        };
        Controls.Add(_trackHueTol);
        y += 36;

        // SV Tolerance
        var lblSv = new Label
        {
            Text = "S/V Tolerance:",
            Location = new Point(16, y),
            ForeColor = Color.White,
            AutoSize = true
        };
        Controls.Add(lblSv);

        _lblSvTolVal = new Label
        {
            Text = _svTolerance.ToString(),
            Location = new Point(370, y),
            ForeColor = Color.FromArgb(52, 168, 83),
            AutoSize = true,
            Font = new Font("Segoe UI", 10, FontStyle.Bold)
        };
        Controls.Add(_lblSvTolVal);
        y += 22;

        _trackSvTol = new TrackBar
        {
            Location = new Point(12, y),
            Size = new Size(380, 30),
            Minimum = 1,
            Maximum = 120,
            Value = _svTolerance,
            TickFrequency = 10,
            BackColor = Color.FromArgb(37, 37, 42)
        };
        _trackSvTol.ValueChanged += (s, e) =>
        {
            _svTolerance = _trackSvTol.Value;
            _lblSvTolVal.Text = _svTolerance.ToString();
            UpdateColorPreview();
        };
        Controls.Add(_trackSvTol);
        y += 50;

        // HSV range info
        var hsvInfo = new Label
        {
            Text = $"HSV Range: H∈[{Math.Max(0, GetH(_pickedColor) - _hueTolerance)}-{Math.Min(179, GetH(_pickedColor) + _hueTolerance)}]  S,V∈[{Math.Max(0, GetS(_pickedColor) - _svTolerance)}-{Math.Min(255, GetV(_pickedColor) + _svTolerance)}]",
            Location = new Point(16, y),
            ForeColor = Color.FromArgb(120, 120, 130),
            AutoSize = true,
            Font = new Font("Consolas", 8)
        };
        Controls.Add(hsvInfo);
        y += 28;

        // Buttons
        _btnOk = new Button
        {
            Text = "✓ Apply to Node",
            Location = new Point(200, y),
            Size = new Size(120, 32),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(52, 168, 83),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9)
        };
        _btnOk.FlatAppearance.BorderSize = 0;
        _btnOk.Click += (s, e) =>
        {
            _onColorPicked?.Invoke(_pickedColor, _hueTolerance, _svTolerance);
            DialogResult = DialogResult.OK;
            Close();
        };
        Controls.Add(_btnOk);

        _btnCancel = new Button
        {
            Text = "Cancel",
            Location = new Point(330, y),
            Size = new Size(70, 32),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(60, 60, 65),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9)
        };
        _btnCancel.FlatAppearance.BorderSize = 0;
        _btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
        Controls.Add(_btnCancel);
    }

    private void OnPickColorClick(object? sender, EventArgs e)
    {
        _isPicking = true;
        _btnPick.Text = "Click anywhere on screen to pick...";
        _btnPick.BackColor = Color.FromArgb(233, 30, 99);
        this.Opacity = 0.7;

        // Start global mouse hook via a background thread
        var thread = new Thread(() =>
        {
            while (_isPicking)
            {
                // Check if left mouse button is clicked anywhere
                if ((Control.MouseButtons & MouseButtons.Left) != 0 && !this.Bounds.Contains(Cursor.Position))
                {
                    var screenPos = Cursor.Position;

                    // Capture 1x1 pixel
                    using var bmp = new Bitmap(1, 1, PixelFormat.Format24bppRgb);
                    using var g = Graphics.FromImage(bmp);
                    g.CopyFromScreen(screenPos.X, screenPos.Y, 0, 0, new Size(1, 1));
                    var pixel = bmp.GetPixel(0, 0);

                    this.Invoke(() =>
                    {
                        _pickedColor = pixel;
                        UpdateDisplay();
                        _isPicking = false;
                        _btnPick.Text = "🎯 Pick Color from Screen";
                        _btnPick.BackColor = Color.FromArgb(66, 133, 244);
                        this.Opacity = 1.0;
                        this.Activate();
                    });
                    break;
                }

                // Check Escape key
                if ((Control.ModifierKeys & Keys.Escape) == Keys.Escape)
                {
                    this.Invoke(() =>
                    {
                        _isPicking = false;
                        _btnPick.Text = "🎯 Pick Color from Screen";
                        _btnPick.BackColor = Color.FromArgb(66, 133, 244);
                        this.Opacity = 1.0;
                    });
                    break;
                }

                Thread.Sleep(50);
            }
        });
        thread.IsBackground = true;
        thread.Start();
    }

    private void UpdateDisplay()
    {
        _colorPreview.BackColor = _pickedColor;
        _lblRgb.Text = $"RGB: ({_pickedColor.R}, {_pickedColor.G}, {_pickedColor.B})";
        _lblHsv.Text = GetHsvText(_pickedColor);
        _lblHex.Text = $"HEX: #{_pickedColor.R:X2}{_pickedColor.G:X2}{_pickedColor.B:X2}";
        _lblPos.Text = $"Picked at screen position";
        UpdateColorPreview();
    }

    private void UpdateColorPreview()
    {
        // Show a gradient preview of the tolerance range
        var h = GetH(_pickedColor);
        var s = GetS(_pickedColor);
        var v = GetV(_pickedColor);

        var previewText = $"  H: {h}±{_hueTolerance}  S: {s}±{_svTolerance}  V: {v}±{_svTolerance}";
        // Update tooltip or additional label
    }

    private static string GetHsvText(Color c)
    {
        var (h, s, v) = ImageRecognitionHelper.RgbToHsvValues(c);
        return $"HSV: ({h:F0}°, {s:F0}, {v:F0})  [OpenCV: H={h/2:F0}, S={s*2.55:F0}, V={v*2.55:F0}]";
    }

    private static int GetH(Color c) { var (h, _, _) = ImageRecognitionHelper.RgbToHsvValues(c); return (int)(h / 2.0); }
    private static int GetS(Color c) { var (_, s, _) = ImageRecognitionHelper.RgbToHsvValues(c); return (int)(s * 2.55); }
    private static int GetV(Color c) { var (_, _, v) = ImageRecognitionHelper.RgbToHsvValues(c); return (int)(v * 2.55); }
}

/// <summary>
/// Helper to expose HSV conversion for UI purposes.
/// Mirrors the logic in ImageRecognition.RgbToHsv but returns in intuitive ranges.
/// </summary>
internal static class ImageRecognitionHelper
{
    public static (double h, double s, double v) RgbToHsvValues(Color c)
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

        // Return in intuitive ranges: H(0-360), S(0-100), V(0-100)
        return (h, s * 100, v * 100);
    }
}
