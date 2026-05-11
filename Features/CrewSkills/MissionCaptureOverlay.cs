namespace SwtorDailyTool;

public sealed class MissionCaptureOverlay : Form
{
    private readonly Rectangle _bounds;
    private readonly List<(Rectangle Rect, Color Color, string Label)> _items;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1500 };

    public MissionCaptureOverlay(MissionSendDetectedEventArgs args)
    {
        var screen = Screen.FromPoint(args.ClickPoint);
        _bounds = screen.Bounds;
        _items =
        [
            (args.Layout.DialogRect, Color.FromArgb(255, 241, 196, 83), "Missions Dialog"),
            (args.Layout.MissionListRect, Color.FromArgb(255, 90, 200, 130), "Mission List"),
            (args.Layout.CompanionPanelRect, Color.FromArgb(255, 230, 120, 200), "Companion Panel"),
            (args.Layout.SendCompanionRect, Color.FromArgb(255, 235, 80, 90), "SEND COMPANION"),
            (args.SelectedMissionRowOnScreen, Color.FromArgb(255, 90, 200, 230), "Selected Row")
        ];

        Bounds = _bounds;
        StartPosition = FormStartPosition.Manual;
        FormBorderStyle = FormBorderStyle.None;
        TopMost = true;
        ShowInTaskbar = false;
        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta;
        Opacity = 0.85;
        DoubleBuffered = true;

        _timer.Tick += (_, _) => Close();
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

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _timer.Start();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _timer.Stop();
        _timer.Dispose();
        base.OnFormClosed(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var font = new Font("Segoe UI", 10F, FontStyle.Bold);
        foreach (var (rect, color, label) in _items)
        {
            var local = new Rectangle(rect.X - _bounds.X, rect.Y - _bounds.Y, rect.Width, rect.Height);
            using var pen = new Pen(color, 3);
            e.Graphics.DrawRectangle(pen, local);
            using var brush = new SolidBrush(color);
            e.Graphics.DrawString(label, font, brush, local.X + 6, local.Y + 4);
        }
    }
}
