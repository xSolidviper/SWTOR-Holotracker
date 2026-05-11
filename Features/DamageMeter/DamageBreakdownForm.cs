using System.Runtime.InteropServices;

namespace SwtorDailyTool;

public sealed class DamageBreakdownForm : Form
{
    private const int WM_NCLBUTTONDOWN = 0xA1;
    private const int HT_CAPTION = 0x2;
    private const int HTBOTTOMRIGHT = 17;

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    private readonly DamageMeterStore _store;
    private readonly AbilityIconCache _icons;
    private readonly string _playerName;
    private readonly System.Windows.Forms.Timer _ticker = new() { Interval = 500 };
    private readonly Color _accent = Color.FromArgb(242, 184, 75);
    private readonly Color _muted = Color.FromArgb(122, 148, 170);
    private readonly Color _background = Color.FromArgb(11, 17, 25);
    private readonly Color _panel = Color.FromArgb(16, 26, 36);

    private readonly Label _headerLabel = new();
    private readonly Label _statsLabel = new();
    private readonly FlowLayoutPanel _list = new();
    private readonly Dictionary<string, AbilityRowControls> _rows = new(StringComparer.OrdinalIgnoreCase);
    private Panel? _resizeGrip;
    private Panel? _titleBar;
    private Panel? _footer;
    private Label? _closeButton;

    public DamageBreakdownForm(DamageMeterStore store, AbilityIconCache icons, string playerName)
    {
        _store = store;
        _icons = icons;
        _playerName = playerName;

        Text = $"Holotracker — {playerName}";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        ShowInTaskbar = false;
        BackColor = _background;
        Opacity = 0.96;
        Width = 540;
        Height = 360;
        MinimumSize = new Size(360, 180);

        BuildUi();

        _ticker.Tick += (_, _) => RefreshBreakdown();
        _ticker.Start();
        RefreshBreakdown();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            const int wsExToolWindow = 0x80;
            var cp = base.CreateParams;
            cp.ExStyle |= wsExToolWindow;
            return cp;
        }
    }

    private void BuildUi()
    {
        var titleBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 36,
            BackColor = _panel,
            Cursor = Cursors.SizeAll
        };
        _titleBar = titleBar;

        _headerLabel.AutoSize = false;
        _headerLabel.Dock = DockStyle.Fill;
        _headerLabel.TextAlign = ContentAlignment.MiddleLeft;
        _headerLabel.ForeColor = _accent;
        _headerLabel.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        _headerLabel.Padding = new Padding(14, 0, 60, 0);
        _headerLabel.Text = _playerName.ToUpperInvariant();
        _headerLabel.BackColor = Color.Transparent;
        _headerLabel.Cursor = Cursors.SizeAll;
        titleBar.Controls.Add(_headerLabel);
        EnableDrag(_headerLabel);
        EnableDrag(titleBar);

        _closeButton = MakeLabelButton("Close", 56, 26, Color.FromArgb(200, 80, 90));
        _closeButton.Click += (_, _) => Close();
        titleBar.Controls.Add(_closeButton);

        var statsBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 26,
            BackColor = _background
        };
        _statsLabel.AutoSize = false;
        _statsLabel.Dock = DockStyle.Fill;
        _statsLabel.TextAlign = ContentAlignment.MiddleLeft;
        _statsLabel.ForeColor = Color.White;
        _statsLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
        _statsLabel.Padding = new Padding(14, 0, 12, 0);
        _statsLabel.Text = "";
        _statsLabel.BackColor = Color.Transparent;
        statsBar.Controls.Add(_statsLabel);
        EnableDrag(_statsLabel);

        _list.Dock = DockStyle.Fill;
        _list.FlowDirection = FlowDirection.TopDown;
        _list.WrapContents = false;
        _list.AutoScroll = true;
        _list.Padding = new Padding(10, 6, 10, 12);
        _list.BackColor = _background;

        // Footer panel hosts the resize grip — by docking it Bottom, the list panel's
        // scroll bar terminates above the grip instead of overlapping it.
        _footer = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 18,
            BackColor = _background
        };
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
        _footer.Controls.Add(_resizeGrip);

        Controls.Add(_list);
        Controls.Add(_footer);
        Controls.Add(statsBar);
        Controls.Add(titleBar);

        PositionResizeGrip();
        PositionCloseButton();

        Resize += (_, _) =>
        {
            UpdateRowWidths();
            PositionResizeGrip();
            PositionCloseButton();
        };

        Paint += (_, e) =>
        {
            using var pen = new Pen(_accent, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        };
    }

    private void PositionResizeGrip()
    {
        if (_resizeGrip is null || _footer is null) return;
        // Inside the footer panel — anchored to its right edge.
        _resizeGrip.Location = new Point(_footer.ClientSize.Width - _resizeGrip.Width - 2, 1);
        _resizeGrip.BringToFront();
    }

    private void PositionCloseButton()
    {
        if (_closeButton is null || _titleBar is null) return;
        _closeButton.Location = new Point(_titleBar.ClientSize.Width - _closeButton.Width - 8, 5);
        _closeButton.BringToFront();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        // Real client widths only known after the form has rendered.
        PositionResizeGrip();
        PositionCloseButton();
        UpdateRowWidths();
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
        label.Paint += (_, e) =>
        {
            using var pen = new Pen(borderColor, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, label.Width - 1, label.Height - 1);
        };
        label.MouseEnter += (_, _) => label.BackColor = Color.FromArgb(40, 56, 78);
        label.MouseLeave += (_, _) => label.BackColor = Color.FromArgb(28, 38, 52);
        return label;
    }

    private void EnableDrag(Control control)
    {
        control.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Left || IsDisposed) return;
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, new IntPtr(HT_CAPTION), IntPtr.Zero);
        };
    }

    private void RefreshBreakdown()
    {
        var fight = _store.ActiveOrLastFight;
        if (fight is null || !fight.Participants.TryGetValue(_playerName, out var player) || player.TotalDamage == 0)
        {
            _statsLabel.Text = "No damage recorded for this player.";
            ClearRows();
            return;
        }

        var dur = fight.Duration;
        var durText = dur.TotalHours >= 1
            ? $"{(int)dur.TotalHours:D2}:{dur.Minutes:D2}:{dur.Seconds:D2}"
            : $"{dur.Minutes:D2}:{dur.Seconds:D2}";
        var seconds = Math.Max(1, dur.TotalSeconds);
        var dps = (long)(player.TotalDamage / seconds);
        _statsLabel.Text = $"Total {Format(player.TotalDamage)}   ·   {Format(dps)} dps   ·   {durText}";

        var ordered = player.DamageAbilities.Values
            .OrderByDescending(a => a.TotalAmount)
            .Where(a => a.TotalAmount > 0)
            .ToList();

        if (ordered.Count == 0)
        {
            ClearRows();
            return;
        }

        var live = new HashSet<string>(ordered.Select(a => a.Name), StringComparer.OrdinalIgnoreCase);
        foreach (var stale in _rows.Keys.Where(k => !live.Contains(k)).ToList())
        {
            var ctrl = _rows[stale];
            _list.Controls.Remove(ctrl.Row);
            ctrl.Row.Dispose();
            _rows.Remove(stale);
        }

        var max = ordered[0].TotalAmount;
        for (var i = 0; i < ordered.Count; i++)
        {
            var ability = ordered[i];
            if (!_rows.TryGetValue(ability.Name, out var ctrl))
            {
                ctrl = BuildAbilityRow(ability.Name);
                _rows[ability.Name] = ctrl;
                _list.Controls.Add(ctrl.Row);
            }
            _list.Controls.SetChildIndex(ctrl.Row, i);

            var fraction = max > 0 ? (double)ability.TotalAmount / max : 0;
            var share = player.TotalDamage > 0 ? (double)ability.TotalAmount / player.TotalDamage : 0;

            ctrl.Title.Text = ability.Name;
            ctrl.Detail.Text =
                $"{ability.HitCount} hits · avg {Format(ability.AverageHit)} · max {Format(ability.MaxHit)} · crit {ability.CritRate:P0}";
            ctrl.Value.Text = Format(ability.TotalAmount);
            ctrl.Share.Text = $"{share:P1}";
            ctrl.Row.Tag = fraction;
            ctrl.Row.Invalidate();
        }
    }

    private void UpdateRowWidths()
    {
        var listWidth = _list.ClientSize.Width - _list.Padding.Horizontal;
        if (listWidth <= 0) return;
        foreach (var ctrl in _rows.Values)
        {
            ctrl.Row.Width = listWidth;
            ctrl.Value.Location = new Point(ctrl.Row.Width - 130, 6);
            ctrl.Share.Location = new Point(ctrl.Row.Width - 70, 26);
            ctrl.Row.Invalidate();
        }
    }

    private AbilityRowControls BuildAbilityRow(string abilityName)
    {
        const int iconSize = 28;
        const int textLeft = 14 + iconSize + 10;
        var initialWidth = Math.Max(280, _list.ClientSize.Width - _list.Padding.Horizontal);

        var row = new Panel
        {
            Width = initialWidth,
            Height = 50,
            BackColor = _panel,
            Margin = new Padding(0, 0, 0, 4),
            Tag = 0.0
        };
        row.Paint += (_, e) => PaintAbilityRow(e, row);

        var icon = _icons.GetIcon(abilityName);
        if (icon is not null)
        {
            row.Controls.Add(new PictureBox
            {
                Image = icon,
                SizeMode = PictureBoxSizeMode.Zoom,
                Width = iconSize,
                Height = iconSize,
                Location = new Point(14, 11),
                BackColor = Color.FromArgb(20, 28, 38)
            });
        }
        else
        {
            var tile = new Panel
            {
                Width = iconSize,
                Height = iconSize,
                Location = new Point(14, 11),
                BackColor = Color.FromArgb(28, 50, 72)
            };
            var letter = string.IsNullOrEmpty(abilityName) ? "?" : abilityName[..1].ToUpperInvariant();
            tile.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = _accent,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                Text = letter,
                BackColor = Color.Transparent
            });
            row.Controls.Add(tile);
        }

        var title = new Label
        {
            AutoSize = true,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Text = abilityName,
            Location = new Point(textLeft, 8),
            BackColor = Color.Transparent
        };
        var detail = new Label
        {
            AutoSize = true,
            ForeColor = _muted,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
            Text = "",
            Location = new Point(textLeft, 28),
            BackColor = Color.Transparent
        };
        var value = new Label
        {
            AutoSize = false,
            Width = 110,
            Height = 20,
            Location = new Point(initialWidth - 130, 6),
            ForeColor = _accent,
            Font = new Font("Consolas", 11F, FontStyle.Bold),
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleRight
        };
        var share = new Label
        {
            AutoSize = false,
            Width = 60,
            Height = 16,
            Location = new Point(initialWidth - 70, 26),
            ForeColor = _muted,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleRight
        };
        row.Controls.Add(title);
        row.Controls.Add(detail);
        row.Controls.Add(value);
        row.Controls.Add(share);

        return new AbilityRowControls(row, title, detail, value, share);
    }

    private void PaintAbilityRow(PaintEventArgs e, Panel row)
    {
        if (row.Tag is not double fraction) return;
        var graphics = e.Graphics;
        using var stripe = new SolidBrush(_accent);
        graphics.FillRectangle(stripe, 0, 0, 4, row.Height);

        var w = (int)((row.Width - 6) * Math.Clamp(fraction, 0, 1));
        if (w > 0)
        {
            using var brush = new SolidBrush(Color.FromArgb(60, _accent));
            graphics.FillRectangle(brush, 4, 4, w, row.Height - 8);
        }
    }

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
        _ticker.Stop();
        _ticker.Dispose();
        base.OnFormClosed(e);
    }

    private sealed record AbilityRowControls(
        Panel Row, Label Title, Label Detail, Label Value, Label Share);
}
