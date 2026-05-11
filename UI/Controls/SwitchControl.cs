namespace SwtorDailyTool;

public sealed class SwitchControl : Control
{
    private bool _checked;

    public SwitchControl()
    {
        DoubleBuffered = true;
        Size = new Size(42, 22);
        Cursor = Cursors.Hand;
    }

    public event EventHandler? CheckedChanged;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value)
            {
                return;
            }

            _checked = value;
            Invalidate();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Color Accent { get; set; } = Color.DeepSkyBlue;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Color OffColor { get; set; } = Color.FromArgb(32, 41, 56);

    protected override void OnClick(EventArgs e)
    {
        Checked = !Checked;
        base.OnClick(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        var track = new Rectangle(0, 2, Width - 1, Height - 5);
        using var trackBrush = new SolidBrush(Checked ? Accent : OffColor);
        using var outline = new Pen(Color.FromArgb(170, 244, 247, 251), 1);
        e.Graphics.FillRoundedRectangle(trackBrush, track, track.Height / 2);
        e.Graphics.DrawRoundedRectangle(outline, track, track.Height / 2);

        var knobSize = Height - 8;
        var knobX = Checked ? Width - knobSize - 5 : 4;
        var knob = new Rectangle(knobX, 4, knobSize, knobSize);
        using var knobBrush = new SolidBrush(Checked ? Color.Black : Color.White);
        e.Graphics.FillEllipse(knobBrush, knob);
    }
}

public static class GraphicsExtensions
{
    public static void FillRoundedRectangle(this Graphics graphics, Brush brush, Rectangle bounds, int radius)
    {
        using var path = CreateRoundedRectangle(bounds, radius);
        graphics.FillPath(brush, path);
    }

    public static void DrawRoundedRectangle(this Graphics graphics, Pen pen, Rectangle bounds, int radius)
    {
        using var path = CreateRoundedRectangle(bounds, radius);
        graphics.DrawPath(pen, path);
    }

    private static System.Drawing.Drawing2D.GraphicsPath CreateRoundedRectangle(Rectangle bounds, int radius)
    {
        var diameter = radius * 2;
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
