using FlowAuto.Core;
using FlowAuto.Engine;

namespace FlowAuto;

/// <summary>
/// Overlay positioned on target window's client area for region selection.
/// Returns coordinates relative to the window's client area.
/// </summary>
public class WindowRegionPicker : Form
{
    private readonly Action<Engine.Region> _onRegionPicked;
    private Point _startPoint;
    private Point _endPoint;
    private bool _isSelecting;

    public WindowRegionPicker(IntPtr targetHwnd, Action<Engine.Region> onRegionPicked)
    {
        _onRegionPicked = onRegionPicked;

        var (left, top, width, height) = WindowHelper.GetClientBounds(targetHwnd);

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(left, top);
        Size = new Size(width, height);
        TopMost = true;
        ShowInTaskbar = false;
        BackColor = Color.Black;
        Opacity = 0.35;
        Cursor = Cursors.Cross;

        DoubleBuffered = true;
        KeyPreview = true;

        Text = $"Region Picker [{width}x{height}]";

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

            var rect = new Rectangle(
                Math.Min(_startPoint.X, _endPoint.X),
                Math.Min(_startPoint.Y, _endPoint.Y),
                Math.Abs(_endPoint.X - _startPoint.X),
                Math.Abs(_endPoint.Y - _startPoint.Y));

            if (rect.Width > 5 && rect.Height > 5)
            {
                _onRegionPicked(new Engine.Region
                {
                    X = rect.X,
                    Y = rect.Y,
                    Width = rect.Width,
                    Height = rect.Height
                });
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

            using var font = new Font("Consolas", 10);
            using var brush = new SolidBrush(Color.Lime);
            string info = $"({rect.X}, {rect.Y})  {rect.Width} x {rect.Height}";
            e.Graphics.DrawString(info, font, brush, rect.X + 2, rect.Y - 20);
        }
    }
}
