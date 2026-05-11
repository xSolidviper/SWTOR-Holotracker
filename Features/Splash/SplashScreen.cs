namespace SwtorDailyTool;

public sealed class SplashScreen : Form
{
    private readonly System.Windows.Forms.Timer _fadeTimer = new() { Interval = 16 };
    private readonly System.Windows.Forms.Timer _holdTimer = new() { Interval = 1200 };
    private readonly Color _accent = Color.FromArgb(242, 184, 75);
    private readonly Color _bg = Color.FromArgb(11, 17, 25);
    private readonly Label _statusLabel = new();
    private float _spinnerAngle;
    private readonly System.Windows.Forms.Timer _spinTimer = new() { Interval = 30 };

    public SplashScreen()
    {
        Text = "SWTOR Holotracker";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(440, 260);
        BackColor = _bg;
        ShowInTaskbar = false;
        TopMost = true;
        Opacity = 0;
        DoubleBuffered = true;

        var iconPath = Path.Combine(AppContext.BaseDirectory, "data", "images", "Icon-removebg-preview.ico");
        if (File.Exists(iconPath))
        {
            try { Icon = new Icon(iconPath, 256, 256); } catch { /* best-effort */ }
        }

        // App icon
        var iconImagePath = Path.Combine(AppContext.BaseDirectory, "data", "images", "AppData", "Icon-removebg-preview.png");
        if (File.Exists(iconImagePath))
        {
            var icon = new PictureBox
            {
                Width = 96,
                Height = 96,
                SizeMode = PictureBoxSizeMode.Zoom,
                ImageLocation = iconImagePath,
                Location = new Point((Width - 96) / 2, 32),
                BackColor = Color.Transparent
            };
            Controls.Add(icon);
        }

        var title = new Label
        {
            AutoSize = false,
            Width = Width,
            Height = 30,
            Location = new Point(0, 138),
            Text = "SWTOR HOLOTRACKER",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent
        };
        Controls.Add(title);

        _statusLabel.AutoSize = false;
        _statusLabel.Width = Width;
        _statusLabel.Height = 22;
        _statusLabel.Location = new Point(0, 172);
        _statusLabel.Text = "Loading…";
        _statusLabel.ForeColor = _accent;
        _statusLabel.Font = new Font("Segoe UI", 9F, FontStyle.Italic);
        _statusLabel.TextAlign = ContentAlignment.MiddleCenter;
        _statusLabel.BackColor = Color.Transparent;
        Controls.Add(_statusLabel);

        Paint += (_, e) =>
        {
            // Gold border around the splash.
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var pen = new Pen(_accent, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);

            // Animated spinner ring at the bottom.
            var ringRect = new Rectangle((Width - 28) / 2, 210, 28, 28);
            using var trackPen = new Pen(Color.FromArgb(60, _accent), 2);
            e.Graphics.DrawArc(trackPen, ringRect, 0, 360);
            using var spinPen = new Pen(_accent, 3);
            e.Graphics.DrawArc(spinPen, ringRect, _spinnerAngle, 90);
        };

        // Fade-in
        _fadeTimer.Tick += (_, _) =>
        {
            if (Opacity < 1.0)
            {
                Opacity = Math.Min(1.0, Opacity + 0.08);
            }
            else
            {
                _fadeTimer.Stop();
            }
        };
        _fadeTimer.Start();

        _spinTimer.Tick += (_, _) =>
        {
            _spinnerAngle = (_spinnerAngle + 8) % 360f;
            Invalidate();
        };
        _spinTimer.Start();
    }

    public void SetStatus(string text)
    {
        _statusLabel.Text = text;
    }

    public void StartFadeOut(Action whenDone)
    {
        _holdTimer.Tick += (_, _) =>
        {
            _holdTimer.Stop();
            _holdTimer.Dispose();

            var fade = new System.Windows.Forms.Timer { Interval = 16 };
            fade.Tick += (_, _) =>
            {
                if (Opacity > 0.05)
                {
                    Opacity = Math.Max(0, Opacity - 0.12);
                }
                else
                {
                    fade.Stop();
                    fade.Dispose();
                    whenDone();
                    Close();
                }
            };
            fade.Start();
        };
        _holdTimer.Start();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _fadeTimer.Stop();
        _fadeTimer.Dispose();
        _spinTimer.Stop();
        _spinTimer.Dispose();
        base.OnFormClosed(e);
    }
}
