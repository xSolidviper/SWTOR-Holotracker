using System.Drawing.Drawing2D;

namespace SwtorDailyTool;

/// <summary>
/// Marquee-style scrolling news ticker. Concatenates a list of (text, url) items
/// with a bullet separator and scrolls them right-to-left at a constant pixel rate.
/// Pauses on hover, edge fade for a polished broadcast look.
/// </summary>
public sealed class NewsTickerPanel : Panel
{
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 30 };
    private List<(string Text, string Url)> _items = [];
    private float _offset;
    private bool _hovered;
    private DateTime _lastTick = DateTime.UtcNow;

    public NewsTickerPanel()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                 | ControlStyles.OptimizedDoubleBuffer, true);
        _timer.Tick += (_, _) => OnTick();
        _timer.Start();
        Cursor = Cursors.Hand;
    }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Color TextColor { get; set; } = Color.White;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Color SeparatorColor { get; set; } = Color.Goldenrod;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public float PixelsPerSecond { get; set; } = 55;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string PlaceholderText { get; set; } = "Loading SWTOR news…";
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int EdgeFadeWidth { get; set; } = 32;

    public event Action<string>? ItemClicked;

    public void SetItems(IEnumerable<(string Text, string Url)> items)
    {
        _items = items
            .Where(i => !string.IsNullOrWhiteSpace(i.Text))
            .ToList();
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hovered = true;
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hovered = false;
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        // Find which item is currently under the click X position.
        if (_items.Count == 0)
        {
            return;
        }
        var item = ItemAtX(e.X);
        if (item is { } selected)
        {
            ItemClicked?.Invoke(selected.Url);
        }
    }

    private (string Text, string Url)? ItemAtX(int clickX)
    {
        using var g = CreateGraphics();
        var separator = "    •    ";
        var sepW = TextRenderer.MeasureText(g, separator, Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Width;

        // Compute item positions in the cycle.
        var itemWidths = _items.Select(i =>
            TextRenderer.MeasureText(g, i.Text, Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Width).ToList();
        var totalW = itemWidths.Sum() + (sepW * _items.Count);
        if (totalW <= 0) return null;

        // Walk through cycles: find which cycle contains clickX.
        // First copy starts at -effectiveOffset.
        var effective = (int)(_offset % totalW);
        var leftEdge = -effective;

        // For each item in each cycle:
        for (var copy = 0; copy < 4; copy++)
        {
            var x = leftEdge + (copy * totalW);
            for (var i = 0; i < _items.Count; i++)
            {
                var iw = itemWidths[i];
                if (clickX >= x && clickX < x + iw)
                {
                    return _items[i];
                }
                x += iw + sepW;
            }
            if (x > Width) break;
        }
        return null;
    }

    private void OnTick()
    {
        var now = DateTime.UtcNow;
        var elapsed = (float)(now - _lastTick).TotalSeconds;
        _lastTick = now;
        if (_hovered)
        {
            return; // pause on hover
        }
        _offset += PixelsPerSecond * elapsed;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        if (_items.Count == 0)
        {
            // Placeholder
            var phSize = TextRenderer.MeasureText(graphics, PlaceholderText, Font);
            TextRenderer.DrawText(graphics, PlaceholderText, Font,
                new Point(8, (Height - phSize.Height) / 2),
                Color.FromArgb(150, TextColor),
                TextFormatFlags.NoPadding);
            return;
        }

        // Measure cycle width (all items + separators).
        var separator = "    •    ";
        var sepW = TextRenderer.MeasureText(graphics, separator, Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Width;
        var itemWidths = _items.Select(i =>
            TextRenderer.MeasureText(graphics, i.Text, Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Width).ToList();
        var cycleW = itemWidths.Sum() + (sepW * _items.Count);
        if (cycleW <= 0) return;

        var effective = (int)(_offset % cycleW);
        var leftEdge = -effective;
        var y = (Height - itemWidths.Count > 0 ? Font.Height : 0) / 2;
        var textY = (Height - Font.Height) / 2;

        // Draw enough copies to fill the visible area.
        for (var copy = 0; copy < 6; copy++)
        {
            var x = leftEdge + (copy * cycleW);
            if (x > Width) break;
            for (var i = 0; i < _items.Count; i++)
            {
                if (x + itemWidths[i] >= 0 && x <= Width)
                {
                    TextRenderer.DrawText(graphics, _items[i].Text, Font,
                        new Point(x, textY), TextColor,
                        TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
                }
                x += itemWidths[i];
                // Draw separator
                if (x + sepW >= 0 && x <= Width)
                {
                    TextRenderer.DrawText(graphics, separator, Font,
                        new Point(x, textY), SeparatorColor,
                        TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
                }
                x += sepW;
            }
        }

        // Edge fades for a polished broadcast look — fade to BackColor at both ends.
        if (EdgeFadeWidth > 0 && BackColor.A > 0)
        {
            using var leftBrush = new LinearGradientBrush(
                new Point(0, 0), new Point(EdgeFadeWidth, 0),
                BackColor, Color.FromArgb(0, BackColor));
            graphics.FillRectangle(leftBrush, 0, 0, EdgeFadeWidth, Height);

            using var rightBrush = new LinearGradientBrush(
                new Point(Width - EdgeFadeWidth, 0), new Point(Width, 0),
                Color.FromArgb(0, BackColor), BackColor);
            graphics.FillRectangle(rightBrush, Width - EdgeFadeWidth, 0, EdgeFadeWidth, Height);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Stop();
            _timer.Dispose();
        }
        base.Dispose(disposing);
    }
}
