namespace SwtorDailyTool;

public sealed class ScanOverlayForm : Form
{
    private readonly List<Rectangle> _regions;
    private readonly bool _activeScreen;

    public ScanOverlayForm(Rectangle bounds, List<Rectangle> regions, bool activeScreen)
    {
        _regions = regions;
        _activeScreen = activeScreen;
        Bounds = bounds;
        StartPosition = FormStartPosition.Manual;
        FormBorderStyle = FormBorderStyle.None;
        TopMost = true;
        ShowInTaskbar = false;
        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta;
        Opacity = 0.85;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            const int wsExTransparent = 0x20;
            const int wsExToolWindow = 0x80;
            var cp = base.CreateParams;
            cp.ExStyle |= wsExTransparent | wsExToolWindow;
            return cp;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var panelPen = new Pen(Color.FromArgb(255, 241, 196, 83), 3);
        using var slotPen  = new Pen(Color.FromArgb(255, 72, 191, 227), 2);
        using var brush    = new SolidBrush(Color.FromArgb(220, 241, 196, 83));
        using var font     = new Font("Segoe UI", 10F, FontStyle.Bold);

        for (var i = 0; i < _regions.Count; i++)
        {
            var region = _regions[i];
            if (i == 0)
            {
                // First region = panel outline
                e.Graphics.DrawRectangle(panelPen, region);
                e.Graphics.DrawString("Crew Skills", font, brush, region.X + 8, region.Y + 8);
            }
            else
            {
                // Subsequent regions = companion slots
                e.Graphics.DrawRectangle(slotPen, region);
                e.Graphics.DrawString($"Slot {i}", font, brush, region.X + 8, region.Y + 4);
            }
        }

        if (!_activeScreen)
            e.Graphics.DrawString("Inactive screen", font, brush, 16, 16);
    }
}
