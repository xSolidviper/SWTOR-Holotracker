using System.Runtime.InteropServices;

namespace SwtorDailyTool;

public sealed class DamageOverlayForm : Form
{
    private const int WM_NCLBUTTONDOWN = 0xA1;
    private const int WM_NCHITTEST = 0x84;
    private const int HT_CAPTION = 0x2;
    private const int HTLEFT = 10;
    private const int HTRIGHT = 11;
    private const int HTTOP = 12;
    private const int HTTOPLEFT = 13;
    private const int HTTOPRIGHT = 14;
    private const int HTBOTTOM = 15;
    private const int HTBOTTOMLEFT = 16;
    private const int HTBOTTOMRIGHT = 17;
    private const int ResizeBorder = 6;

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    private readonly DamageMeterStore _store;
    private readonly AbilityIconCache _icons;
    private readonly AppSettings _settings;
    private readonly System.Windows.Forms.Timer _ticker = new() { Interval = 500 };
    private readonly Dictionary<string, DamageBreakdownForm> _openBreakdowns =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly Color[] PlayerColorPalette =
    [
        Color.FromArgb(242, 184, 75),    // gold (matches main app)
        Color.FromArgb(120, 200, 230),   // cyan
        Color.FromArgb(120, 230, 140),   // green
        Color.FromArgb(230, 120, 200),   // pink
        Color.FromArgb(240, 140, 60),    // orange
        Color.FromArgb(180, 140, 230),   // purple
        Color.FromArgb(230, 80, 90),     // red
        Color.FromArgb(100, 150, 230),   // blue
    ];
    private readonly Color _accent = Color.FromArgb(242, 184, 75);
    private readonly Color _muted = Color.FromArgb(122, 148, 170);
    private readonly Color _background = Color.FromArgb(11, 17, 25);
    private readonly Color _panel = Color.FromArgb(16, 26, 36);

    private readonly Label _statsLabel = new();
    private readonly ComboBox _fightDropdown = new();
    private readonly FlowLayoutPanel _list = new();
    private readonly Dictionary<string, RowControls> _rows = new(StringComparer.OrdinalIgnoreCase);
    private Label? _resetButton;
    private Label? _closeButton;
    private Panel? _titleBar;
    private Panel? _resizeGrip;

    private const int OverlayWidth = 460;
    private int _selectedHistoryIndex = -1; // -1 = active fight
    private bool _suppressDropdownEvent;
    private int RowsToShow => Math.Clamp(_settings.OverlayPlayerRows, 3, 10);

    public DamageOverlayForm(DamageMeterStore store, AbilityIconCache icons, AppSettings settings)
    {
        _store = store;
        _icons = icons;
        _settings = settings;

        Text = "Holotracker Overlay";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        ShowInTaskbar = false;
        BackColor = _background;
        Opacity = Math.Clamp(_settings.OverlayOpacityPercent, 50, 100) / 100.0;
        Width = OverlayWidth;
        Height = 200;
        MinimumSize = new Size(OverlayWidth, 120);

        BuildUi();

        _ticker.Tick += (_, _) => RefreshOverlay();
        _ticker.Start();
        RefreshOverlay();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            const int wsExToolWindow = 0x80;
            const int wsExLayered = 0x80000;
            const int wsExTransparent = 0x20;
            var cp = base.CreateParams;
            cp.ExStyle |= wsExToolWindow;
            // CreateParams is called by the Form base constructor BEFORE our fields are
            // assigned, so _settings can be null on first access. Use null-conditional.
            if (_settings is not null && _settings.OverlayClickThrough)
            {
                cp.ExStyle |= wsExLayered | wsExTransparent;
            }
            return cp;
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_NCHITTEST)
        {
            // LParam holds screen coordinates as two 16-bit signed values.
            var x = (short)(m.LParam.ToInt32() & 0xFFFF);
            var y = (short)((m.LParam.ToInt32() >> 16) & 0xFFFF);
            var pos = PointToClient(new Point(x, y));
            var hit = HitTest(pos);
            if (hit != 0)
            {
                m.Result = (IntPtr)hit;
                return;
            }
        }
        base.WndProc(ref m);
    }

    private int HitTest(Point pos)
    {
        // Resize edges win first.
        var top = pos.Y < ResizeBorder;
        var bottom = pos.Y > Height - ResizeBorder;
        var left = pos.X < ResizeBorder;
        var right = pos.X > Width - ResizeBorder;
        if (top && left) return HTTOPLEFT;
        if (top && right) return HTTOPRIGHT;
        if (bottom && left) return HTBOTTOMLEFT;
        if (bottom && right) return HTBOTTOMRIGHT;
        if (top) return HTTOP;
        if (bottom) return HTBOTTOM;
        if (left) return HTLEFT;
        if (right) return HTRIGHT;

        // Title bar is the entire drag handle — except where there are clickable
        // controls (dropdown, Reset, Close). Returning HTCAPTION makes Windows handle
        // the drag natively, so it always works regardless of focus.
        if (_titleBar is not null && pos.Y < _titleBar.Bottom)
        {
            if (_fightDropdown.Bounds.Contains(pos)) return 0;
            if (_resetButton is not null && _resetButton.Bounds.Contains(pos)) return 0;
            if (_closeButton is not null && _closeButton.Bounds.Contains(pos)) return 0;
            return HT_CAPTION;
        }
        return 0;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        // Title bar's real width is only known after the form is rendered.
        PositionTitleButtons();
        UpdateRowWidths();
    }

    private void BuildUi()
    {
        // ── Title bar — fight dropdown + Reset + Close, bigger so text renders reliably.
        var titleBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 40,
            BackColor = _panel,
            Cursor = Cursors.SizeAll
        };

        // HOLOTRACKER drag handle on the left.
        var titleLabel = new Label
        {
            AutoSize = false,
            Width = 100,
            Height = 36,
            Location = new Point(8, 2),
            ForeColor = _accent,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Text = "HOLOTRACKER",
            TextAlign = ContentAlignment.MiddleLeft,
            Cursor = Cursors.SizeAll,
            BackColor = Color.Transparent
        };
        titleBar.Controls.Add(titleLabel);
        EnableDrag(titleLabel);
        EnableDrag(titleBar);

        // Fight dropdown in the middle.
        _fightDropdown.DropDownStyle = ComboBoxStyle.DropDownList;
        _fightDropdown.Width = 200;
        _fightDropdown.Height = 24;
        _fightDropdown.Location = new Point(112, 8);
        _fightDropdown.FlatStyle = FlatStyle.Flat;
        _fightDropdown.BackColor = Color.FromArgb(28, 38, 52);
        _fightDropdown.ForeColor = Color.White;
        _fightDropdown.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
        _fightDropdown.SelectedIndexChanged += (_, _) =>
        {
            if (_suppressDropdownEvent) return;
            _selectedHistoryIndex = _fightDropdown.SelectedIndex - 1;
            RefreshOverlay();
        };
        titleBar.Controls.Add(_fightDropdown);

        // Reset and Close buttons as Labels. Position explicitly on resize — anchor
        // is unreliable on FormBorderStyle.None forms.
        _resetButton = MakeLabelButton("Reset", 64, 26, _accent);
        _resetButton.Click += (_, _) =>
        {
            _store.Reset();
            _selectedHistoryIndex = -1;
            RefreshOverlay();
        };
        titleBar.Controls.Add(_resetButton);

        _closeButton = MakeLabelButton("Close", 50, 26, Color.FromArgb(200, 80, 90));
        _closeButton.Click += (_, _) => Close();
        titleBar.Controls.Add(_closeButton);

        _titleBar = titleBar;
        PositionTitleButtons();

        // ── Stats line: total / DPS / duration
        var statsBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 24,
            BackColor = _background
        };

        _statsLabel.AutoSize = false;
        _statsLabel.Dock = DockStyle.Fill;
        _statsLabel.TextAlign = ContentAlignment.MiddleLeft;
        _statsLabel.ForeColor = Color.White;
        _statsLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
        _statsLabel.Padding = new Padding(12, 0, 12, 0);
        _statsLabel.Text = "Waiting for combat";
        _statsLabel.BackColor = Color.Transparent;
        statsBar.Controls.Add(_statsLabel);
        EnableDrag(_statsLabel);

        // ── Player rows
        _list.Dock = DockStyle.Fill;
        _list.FlowDirection = FlowDirection.TopDown;
        _list.WrapContents = false;
        _list.AutoScroll = true;
        _list.Padding = new Padding(8, 4, 8, 8);
        _list.BackColor = _background;
        Resize += (_, _) =>
        {
            UpdateRowWidths();
            PositionTitleButtons();
            PositionResizeGrip();
        };

        // Add controls in REVERSE visual order so each Dock.Top push lands them correctly.
        Controls.Add(_list);
        Controls.Add(statsBar);
        Controls.Add(titleBar);

        // Visible resize grip at the bottom-right corner. Children cover the form's
        // edges so WM_NCHITTEST resize-borders aren't reachable — an explicit grip
        // gives the user something concrete to grab.
        _resizeGrip = new Panel
        {
            Width = 16,
            Height = 16,
            BackColor = Color.Transparent,
            Cursor = Cursors.SizeNWSE
        };
        _resizeGrip.Paint += (_, e) =>
        {
            using var pen = new Pen(_accent, 1);
            // Three short diagonal lines indicating a grip.
            e.Graphics.DrawLine(pen, 14, 4, 4, 14);
            e.Graphics.DrawLine(pen, 14, 8, 8, 14);
            e.Graphics.DrawLine(pen, 14, 12, 12, 14);
        };
        _resizeGrip.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Left || IsDisposed) return;
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, new IntPtr(HTBOTTOMRIGHT), IntPtr.Zero);
        };
        Controls.Add(_resizeGrip);
        _resizeGrip.BringToFront();
        PositionResizeGrip();

        // Subtle gold border around the whole window.
        Paint += (_, e) =>
        {
            using var pen = new Pen(_accent, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        };

        RefreshFightDropdown();
    }

    private Label MakeLabelButton(string text, int width, int height, Color borderColor)
    {
        var label = new Label
        {
            Text = text,
            AutoSize = false,
            Width = width,
            Height = height,
            BackColor = Color.FromArgb(28, 38, 52),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor = Cursors.Hand,
            BorderStyle = BorderStyle.None
        };
        // Custom paint for a thin colored border so the button is clearly visible.
        label.Paint += (_, e) =>
        {
            using var pen = new Pen(borderColor, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, label.Width - 1, label.Height - 1);
        };
        // Highlight on hover.
        label.MouseEnter += (_, _) => label.BackColor = Color.FromArgb(40, 56, 78);
        label.MouseLeave += (_, _) => label.BackColor = Color.FromArgb(28, 38, 52);
        return label;
    }

    private void RefreshFightDropdown()
    {
        var history = _store.History;
        var labels = new List<string> { "Current Fight" };
        for (var i = history.Count - 1; i >= 0; i--)
        {
            var fight = history[i];
            var humanIndex = history.Count - i;
            var dur = fight.Duration;
            var durText = dur.TotalHours >= 1
                ? $"{(int)dur.TotalHours}h{dur.Minutes:D2}m"
                : $"{dur.Minutes:D1}m{dur.Seconds:D2}s";
            labels.Add($"#{humanIndex} ({durText})");
        }

        _suppressDropdownEvent = true;
        try
        {
            _fightDropdown.BeginUpdate();
            _fightDropdown.Items.Clear();
            foreach (var label in labels)
            {
                _fightDropdown.Items.Add(label);
            }
            var idx = Math.Clamp(_selectedHistoryIndex + 1, 0, _fightDropdown.Items.Count - 1);
            _fightDropdown.SelectedIndex = idx;
        }
        finally
        {
            _fightDropdown.EndUpdate();
            _suppressDropdownEvent = false;
        }
    }

    private void EnableDrag(Control control)
    {
        control.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Left || IsDisposed)
            {
                return;
            }
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, new IntPtr(HT_CAPTION), IntPtr.Zero);
        };
    }

    private FightSegment? ResolveSelectedFight()
    {
        if (_selectedHistoryIndex < 0)
        {
            return _store.ActiveOrLastFight;
        }
        var history = _store.History;
        if (_selectedHistoryIndex >= history.Count)
        {
            _selectedHistoryIndex = -1;
            return _store.ActiveOrLastFight;
        }
        return history[history.Count - 1 - _selectedHistoryIndex];
    }

    private void RefreshOverlay()
    {
        RefreshFightDropdown();

        var fight = ResolveSelectedFight();
        if (fight is null)
        {
            _statsLabel.Text = "Waiting for combat";
            ClearRows();
            return;
        }

        var dur = fight.Duration;
        var durText = dur.TotalHours >= 1
            ? $"{(int)dur.TotalHours:D2}:{dur.Minutes:D2}:{dur.Seconds:D2}"
            : $"{dur.Minutes:D2}:{dur.Seconds:D2}";
        _statsLabel.Text = $"{Format(fight.TotalDamage)}   ·   {Format(fight.DamagePerSecond)} dps   ·   {durText}";

        var ordered = fight.Participants.Values
            .OrderByDescending(p => p.TotalDamage)
            .Where(p => p.TotalDamage > 0)
            .Take(RowsToShow)
            .ToList();
        if (ordered.Count == 0)
        {
            ClearRows();
            return;
        }

        var live = new HashSet<string>(ordered.Select(p => p.Name), StringComparer.OrdinalIgnoreCase);
        foreach (var stale in _rows.Keys.Where(k => !live.Contains(k)).ToList())
        {
            var ctrl = _rows[stale];
            _list.Controls.Remove(ctrl.Row);
            ctrl.Row.Dispose();
            _rows.Remove(stale);
        }

        var max = ordered[0].TotalDamage;
        var seconds = Math.Max(1, fight.Duration.TotalSeconds);
        for (var i = 0; i < ordered.Count; i++)
        {
            var p = ordered[i];
            if (!_rows.TryGetValue(p.Name, out var ctrl))
            {
                ctrl = BuildRow(p.Name);
                _rows[p.Name] = ctrl;
                _list.Controls.Add(ctrl.Row);
            }
            _list.Controls.SetChildIndex(ctrl.Row, i);

            var fraction = max > 0 ? (double)p.TotalDamage / max : 0;
            var share = fight.TotalDamage > 0 ? (double)p.TotalDamage / fight.TotalDamage : 0;
            var dps = (long)(p.TotalDamage / seconds);

            ctrl.Name.Text = p.Name;
            ctrl.Total.Text = Format(p.TotalDamage);
            ctrl.Dps.Text = $"{Format(dps)} dps";
            ctrl.Share.Text = $"{share:P0}";
            ctrl.Row.Tag = new RowPaintState(fraction, GetPlayerColor(p.Name, _accent));
            ctrl.Row.Invalidate();
        }

    }

    private void PositionResizeGrip()
    {
        if (_resizeGrip is null) return;
        _resizeGrip.Location = new Point(Width - _resizeGrip.Width - 2, Height - _resizeGrip.Height - 2);
        _resizeGrip.BringToFront();
    }

    private void PositionTitleButtons()
    {
        if (_titleBar is null || _resetButton is null || _closeButton is null)
        {
            return;
        }

        // Layout from the right: [Reset] [Close], with 8px right padding and 4px gap.
        var w = _titleBar.ClientSize.Width;
        _closeButton.Location = new Point(w - _closeButton.Width - 8, 7);
        _resetButton.Location = new Point(w - _closeButton.Width - 8 - _resetButton.Width - 4, 7);
        _resetButton.BringToFront();
        _closeButton.BringToFront();
    }

    private void UpdateRowWidths()
    {
        var listWidth = _list.ClientSize.Width - _list.Padding.Horizontal;
        if (listWidth <= 0) return;
        foreach (var ctrl in _rows.Values)
        {
            ctrl.Row.Width = listWidth;
            // Keep the right-side numeric labels anchored to the right edge.
            ctrl.Total.Location = new Point(ctrl.Row.Width - 188, 1);
            ctrl.Dps.Location = new Point(ctrl.Row.Width - 110, 1);
            ctrl.Share.Location = new Point(ctrl.Row.Width - 40, 1);
            ctrl.Row.Invalidate();
        }
    }

    private RowControls BuildRow(string name)
    {
        var initialWidth = Math.Max(200, _list.ClientSize.Width - _list.Padding.Horizontal);
        var row = new Panel
        {
            Width = initialWidth,
            Height = 22,
            BackColor = _panel,
            Margin = new Padding(0, 0, 0, 2),
            Tag = 0.0
        };
        row.Paint += (_, e) => PaintRow(e, row);

        var nameLabel = new Label
        {
            AutoSize = false,
            Width = 130,
            Height = 20,
            Location = new Point(10, 1),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = name,
            Cursor = Cursors.Hand
        };
        var totalLabel = new Label
        {
            AutoSize = false,
            Width = 76,
            Height = 20,
            Location = new Point(row.Width - 188, 1),
            ForeColor = _accent,
            Font = new Font("Consolas", 9F, FontStyle.Bold),
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleRight
        };
        var dpsLabel = new Label
        {
            AutoSize = false,
            Width = 76,
            Height = 20,
            Location = new Point(row.Width - 110, 1),
            ForeColor = Color.FromArgb(180, 200, 220),
            Font = new Font("Consolas", 8.5F, FontStyle.Regular),
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleRight
        };
        var shareLabel = new Label
        {
            AutoSize = false,
            Width = 36,
            Height = 20,
            Location = new Point(row.Width - 40, 1),
            ForeColor = _muted,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleRight
        };
        row.Controls.Add(nameLabel);
        row.Controls.Add(totalLabel);
        row.Controls.Add(dpsLabel);
        row.Controls.Add(shareLabel);
        EnableDrag(row);
        EnableDrag(totalLabel);
        EnableDrag(dpsLabel);
        EnableDrag(shareLabel);

        // Click on the player NAME opens a breakdown panel for that player. We wire only
        // the name label so dragging from elsewhere on the row still moves the window.
        if (_settings.OverlayDoubleClickToOpen)
        {
            nameLabel.DoubleClick += (_, _) => OpenBreakdown(name);
        }
        else
        {
            nameLabel.Click += (_, _) => OpenBreakdown(name);
        }

        return new RowControls(row, nameLabel, totalLabel, dpsLabel, shareLabel);
    }

    private void OpenBreakdown(string playerName)
    {
        if (_openBreakdowns.TryGetValue(playerName, out var existing) && !existing.IsDisposed)
        {
            existing.BringToFront();
            existing.Activate();
            return;
        }

        var breakdown = new DamageBreakdownForm(_store, _icons, playerName);
        // Center on the user's primary screen.
        var screen = Screen.FromControl(this).WorkingArea;
        breakdown.Location = new Point(
            screen.Left + (screen.Width - breakdown.Width) / 2,
            screen.Top + (screen.Height - breakdown.Height) / 2);
        breakdown.FormClosed += (_, _) =>
        {
            _openBreakdowns.Remove(playerName);
        };
        _openBreakdowns[playerName] = breakdown;
        breakdown.Show();
    }

    private static Color GetPlayerColor(string playerName, Color fallback)
    {
        if (string.IsNullOrEmpty(playerName)) return fallback;
        // Stable hash → palette index. Same player keeps the same color across refreshes.
        var hash = 0;
        foreach (var c in playerName)
        {
            hash = (hash * 31 + c) & 0x7FFFFFFF;
        }
        return PlayerColorPalette[hash % PlayerColorPalette.Length];
    }

    private void PaintRow(PaintEventArgs e, Panel row)
    {
        if (row.Tag is not RowPaintState state)
        {
            return;
        }
        var graphics = e.Graphics;
        using var stripe = new SolidBrush(state.BarColor);
        graphics.FillRectangle(stripe, 0, 0, 4, row.Height);

        var w = (int)((row.Width - 6) * Math.Clamp(state.Fraction, 0, 1));
        if (w > 0)
        {
            using var brush = new SolidBrush(Color.FromArgb(70, state.BarColor));
            graphics.FillRectangle(brush, 4, 2, w, row.Height - 4);
        }
    }

    private sealed record RowPaintState(double Fraction, Color BarColor);

    private void ClearRows()
    {
        foreach (var ctrl in _rows.Values)
        {
            _list.Controls.Remove(ctrl.Row);
            ctrl.Row.Dispose();
        }
        _rows.Clear();
    }

    private static string Format(long v)
    {
        if (v >= 1_000_000) return $"{v / 1_000_000.0:F2}M";
        if (v >= 10_000) return $"{v / 1_000.0:F1}K";
        return v.ToString("N0");
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        try { _ticker.Stop(); } catch { }
        try { _ticker.Dispose(); } catch { }

        // Close any breakdown windows the user opened from this overlay so they don't
        // outlive their parent and try to access disposed state on their next tick.
        foreach (var breakdown in _openBreakdowns.Values.ToList())
        {
            try
            {
                if (!breakdown.IsDisposed)
                {
                    breakdown.Close();
                    breakdown.Dispose();
                }
            }
            catch { }
        }
        _openBreakdowns.Clear();

        base.OnFormClosed(e);
    }

    private sealed record RowControls(
        Panel Row, Label Name, Label Total, Label Dps, Label Share);
}
