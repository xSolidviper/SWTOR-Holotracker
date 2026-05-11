namespace SwtorDailyTool;

/// <summary>
/// Vertically-scrollable panel with a custom dark scrollbar — no system scrollbar rendered.
/// Add exactly one child (the content FlowLayoutPanel). Width/Top are managed here.
/// </summary>
public sealed class DarkScrollPanel : Panel
{
    private int _scrollOffset;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Color ThumbColor { get; set; } = Color.FromArgb(80, 100, 120);
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Color TrackColor { get; set; } = Color.FromArgb(10, 14, 27);

    private const int BarWidth = 8;

    public DarkScrollPanel()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        AutoScroll = false;
        MouseWheel += OnWheel;
        Resize += (_, _) => Reflow();
    }

    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        if (e.Control is null) return;
        e.Control.Dock = DockStyle.None;
        e.Control.SizeChanged += (_, _) => Reflow();
        Reflow();
    }

    private Control? Content => Controls.Count > 0 ? Controls[0] : null;
    private int ContentH => Content?.Height ?? 0;
    private int ViewH => ClientSize.Height;
    private int MaxOffset => Math.Max(0, ContentH - ViewH);

    private void Reflow()
    {
        var c = Content;
        if (c is null) return;
        c.Left = 0;
        c.Width = ClientSize.Width - BarWidth;
        _scrollOffset = Math.Clamp(_scrollOffset, 0, MaxOffset);
        c.Top = -_scrollOffset;
        Invalidate();
    }

    private void OnWheel(object? sender, MouseEventArgs e)
    {
        _scrollOffset = Math.Clamp(_scrollOffset - e.Delta / 3, 0, MaxOffset);
        Reflow();
    }

    // --- thumb drag ---
    private bool _dragging;
    private int _dragStartY, _dragStartOffset;

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (!ThumbRect.Contains(e.Location)) return;
        _dragging = true;
        _dragStartY = e.Y;
        _dragStartOffset = _scrollOffset;
        Capture = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_dragging || ContentH <= ViewH) return;
        var thumbTrack = ViewH - ThumbH;
        if (thumbTrack <= 0) return;
        var ratio = (double)MaxOffset / thumbTrack;
        _scrollOffset = Math.Clamp(_dragStartOffset + (int)((e.Y - _dragStartY) * ratio), 0, MaxOffset);
        Reflow();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _dragging = false;
        Capture = false;
    }

    // --- scrollbar geometry ---
    private int ThumbH
    {
        get
        {
            if (ContentH <= 0) return ViewH;
            return Math.Max(24, (int)((double)ViewH / ContentH * ViewH));
        }
    }

    private Rectangle ThumbRect
    {
        get
        {
            var thumbTop = MaxOffset > 0
                ? (int)((double)_scrollOffset / MaxOffset * (ViewH - ThumbH))
                : 0;
            return new Rectangle(ClientSize.Width - BarWidth, thumbTop, BarWidth, ThumbH);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (ContentH <= ViewH) return;
        using var track = new SolidBrush(TrackColor);
        e.Graphics.FillRectangle(track, ClientSize.Width - BarWidth, 0, BarWidth, ViewH);
        using var thumb = new SolidBrush(ThumbColor);
        e.Graphics.FillRectangle(thumb, ThumbRect);
    }
}
