namespace SwtorDailyTool;

public partial class Form1 : Form
{
    private readonly List<Panel> _cards = [];
    private readonly Dictionary<string, List<CardBinding>> _cardBindings = new(StringComparer.OrdinalIgnoreCase);
    private Rectangle _activeDatacronMapRect;
    private readonly DailyToolData _data;
    private readonly DatacronGuideData _datacrons;
    private readonly RedeemCodeData _redeemCodes;
    private readonly ProgressStore _progress;
    private readonly ThemeData _theme;
    private readonly Color _accent;
    private readonly Color _accentAlt;
    private readonly Color _background;
    private readonly Color _panel;
    private readonly Color _panelAlt;
    private readonly Color _text;
    private readonly Color _muted;
    private readonly System.Windows.Forms.Timer _resetTimer = new() { Interval = 1000 };
    private Label? _dailyCountdownLabel;
    private Label? _weeklyCountdownLabel;
    private Label? _headerPatchTitleLabel;
    private Label? _headerPatchDateLabel;
    private Panel? _headerPatchPanel;
    private string? _headerPatchUrl;
    private NewsTickerPanel? _newsTicker;
    private Label? _versionChipLabel;
    private Label? _loreFooterLabel;
    private System.Windows.Forms.Timer? _loreFooterTimer;
    private int _loreFooterIndex;
    private static readonly Random _loreRandom = new();
    private Label? _conquestCountdownLabel;
    private bool _hideCompleted;
    private OcrMonitorForm? _questTracker;
    private readonly AppSettings _settings;
    private readonly DamageMeterStore _damageStore = new();
    private readonly CombatLogTailer _damageTailer = new();
    private readonly AbilityIconCache _abilityIcons = new(AppContext.BaseDirectory);
    private FlowLayoutPanel? _damageBarsPanel;
    private Label? _damageHeaderTotal;
    private Label? _damageHeaderDps;
    private Label? _damageHeaderDuration;
    private Label? _damageStatePill;
    private Label? _damageStatusLabel;
    private Button? _damageBackButton;
    private string? _damageDrilledPlayer;
    private readonly HashSet<string> _damageExpandedAbilities = new(StringComparer.OrdinalIgnoreCase);
    private System.Windows.Forms.Timer? _damageTickTimer;
    private ComboBox? _damageFightSelector;
    private DamageOverlayForm? _damageOverlay;
    private Button? _damageOverlayButton;
    private int _damageSelectedHistoryIndex = -1; // -1 = current/active fight
    private bool _damageSuppressSelectorEvent;
    private DamageMeterView _damageActiveView = DamageMeterView.None;
    private readonly Dictionary<string, DamagePlayerRowControls> _damagePlayerRows =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DamageAbilityRowControls> _damageAbilityRows =
        new(StringComparer.OrdinalIgnoreCase);
    private Label? _damageEmptyLabel;
    private Panel? _damageHistoryGraph;
    private FlowLayoutPanel? _damageTopAbilitiesPanel;
    private FlowLayoutPanel? _damageAnalyticsSummaryPanel;
    private readonly List<(Button Tab, Panel Page)> _damageSubTabPages = [];
    private int _damageSelectedSubTab;

    private enum DamageMeterView { None, Players, Abilities }
    private const int DamagePanelWidth = 900;
    private static readonly Color[] DamageSeriesColors =
    [
        Color.FromArgb(70, 190, 255),
        Color.FromArgb(255, 184, 64),
        Color.FromArgb(130, 225, 120),
        Color.FromArgb(205, 130, 255),
        Color.FromArgb(255, 105, 130),
        Color.FromArgb(100, 225, 205),
        Color.FromArgb(255, 145, 80),
        Color.FromArgb(150, 170, 255)
    ];
    private static readonly Color DamageAbilityColor = Color.FromArgb(255, 116, 116);

    private sealed record DamagePlayerRowControls(
        Panel Row, Label Title, Label Detail, Label Value, Label Share);

    private sealed record DamageAbilityRowControls(
        Panel Row, Label Title, Label Detail, Label Value, Label Share,
        Label TimesUsedHeader, Label MinHeader, Label MaxHeader, Label AvgHeader, Label CritHeader,
        Label TimesUsed, Label Min, Label Max, Label Avg, Label Crit);
    private CrewMissionWatcher? _crewMissionWatcher;
    private Label? _crewMissionStatus;
    private Button? _crewMissionToggle;
    private Label? _crewMissionStatePill;
    private Label? _crewMissionCountLabel;
    private FlowLayoutPanel? _crewMissionListPanel;
    private Label? _crewMissionEmptyLabel;
    private readonly Dictionary<string, CrewMissionRowControls> _crewMissionRows =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _crewMissionNotified = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CompletionBurst> _crewBursts = new(StringComparer.OrdinalIgnoreCase);
    private System.Windows.Forms.Timer? _crewBurstTimer;
    private readonly CrewMissionStore _crewMissionStore = new();
    private MissionOcrPipeline? _missionOcrPipeline;
    private System.Windows.Forms.Timer? _crewMissionTickTimer;
    private NotifyIcon? _trayIcon;
    private Panel? _questSubBar;

    private sealed record CrewMissionRowControls(
        Panel Row,
        Label Companion,
        Label Mission,
        Label Details,
        Label Countdown);
    private readonly List<(Button Tab, Panel Page)> _questTabPages = [];
    private int _questSelectedTab = 0;
    private readonly SwtorServerStatus _serverStatus = new();
    private Label? _serverLastCheckedLabel;
    private readonly List<(ServerInfo Info, Label StatusDot, Label StatusText, Label NameLabel)> _serverCards = [];
    private readonly SwtorNewsService _newsService = new();
    private Panel? _patchNotesListPanel;
    private Panel? _newsListPanel;
    private Panel? _articleViewerPanel;

    public Form1()
    {
        _data = DataLoader.LoadDailyData(AppContext.BaseDirectory);
        _datacrons = DataLoader.LoadDatacrons(AppContext.BaseDirectory);
        _redeemCodes = DataLoader.LoadRedeemCodes(AppContext.BaseDirectory);
        _progress = new ProgressStore(AppContext.BaseDirectory);
        _settings = AppSettings.Load(AppContext.BaseDirectory);
        _theme = DataLoader.LoadTheme(AppContext.BaseDirectory);
        _accent = ParseColor(_theme.Accent);
        _accentAlt = ParseColor(_theme.AccentAlt);
        _background = ParseColor(_theme.Background);
        _panel = ParseColor(_theme.Panel);
        _panelAlt = ParseColor(_theme.PanelAlt);
        _text = ParseColor(_theme.Text);
        _muted = ParseColor(_theme.MutedText);
        MigrateDatacronProgressKeys();

        var iconPath = Path.Combine(AppContext.BaseDirectory, "data", "images", "Icon-removebg-preview.ico");
        if (File.Exists(iconPath))
            Icon = new Icon(iconPath, 256, 256);

        InitializeShell();
        _progress.Changed += (_, _) => RefreshProgressState();
        _resetTimer.Tick += (_, _) => UpdateResetTimer();
        _resetTimer.Start();
        BuildUi();
        _serverStatus.Updated += (_, _) => SafelyBeginInvoke(RefreshServerStatusPanel);
        _ = _serverStatus.StartAsync();
        _newsService.PatchNotesUpdated += (_, _) => SafelyBeginInvoke(() =>
        {
            RefreshPatchNotesList();
            RefreshVersionChip();
        });
        _newsService.NewsUpdated += (_, _) => SafelyBeginInvoke(() =>
        {
            RefreshNewsList();
            RefreshHeaderTicker();
        });
        _newsService.StartPolling();

        // The form's Handle isn't created until Show() — when launched via the splash
        // ApplicationContext, that happens after construction. Background fetches that
        // complete before Show() can't BeginInvoke onto the UI thread, so their data
        // would be lost. On Shown, pull whatever the services already have.
        Shown += (_, _) =>
        {
            try { RefreshServerStatusPanel(); } catch { }
            try { RefreshPatchNotesList(); } catch { }
            try { RefreshVersionChip(); } catch { }
            try { RefreshNewsList(); } catch { }
            try { RefreshHeaderTicker(); } catch { }
        };
        _crewMissionStore.Changed += () => BeginInvoke((Action)RebuildCrewMissionList);
        _damageTailer.EventReceived += ev => _damageStore.Process(ev);
        _damageTailer.StatusChanged += message => BeginInvoke(() =>
        {
            if (_damageStatusLabel is not null)
            {
                _damageStatusLabel.Text = message;
            }
            UpdateDamageStatePill();
        });
        // Don't refresh per-event during combat — store.Changed fires on every damage hit,
        // which would rebuild rows faster than clicks can register. The 500ms tick timer
        // drives the UI instead. Only listen for fight count changes to refresh the
        // selector dropdown.
        _damageStore.Changed += () => BeginInvoke((Action)RefreshDamageFightSelector);
        FormClosed += (_, _) =>
        {
            // Wrap every shutdown step in try/catch so a single null/disposed access
            // can't surface a "send error report" dialog when the user closes the app.
            Safely(() => _crewMissionWatcher?.Dispose());
            Safely(() => _crewMissionTickTimer?.Stop());
            Safely(() => _crewMissionTickTimer?.Dispose());
            Safely(() => _damageTailer.Dispose());
            Safely(() => _damageTickTimer?.Stop());
            Safely(() => _damageTickTimer?.Dispose());
            Safely(() =>
            {
                if (_damageOverlay is not null && !_damageOverlay.IsDisposed)
                {
                    _damageOverlay.Close();
                    _damageOverlay.Dispose();
                }
            });
            Safely(() =>
            {
                if (_trayIcon is not null)
                {
                    _trayIcon.Visible = false;
                    _trayIcon.Dispose();
                }
            });
            Safely(() => _loreFooterTimer?.Stop());
            Safely(() => _loreFooterTimer?.Dispose());
            Safely(() => _crewBurstTimer?.Stop());
            Safely(() => _crewBurstTimer?.Dispose());
        };

        if (_settings.AutoStartSkillTracking)
        {
            BeginInvoke(() =>
            {
                _crewMissionWatcher ??= CreateCrewMissionWatcher();
                if (!_crewMissionWatcher.IsRunning)
                {
                    try
                    {
                        _crewMissionWatcher.Start();
                        if (_crewMissionToggle is not null)
                        {
                            _crewMissionToggle.Text = "Disable Skill Tracking";
                        }
                        UpdateCrewMissionStatePill();
                    }
                    catch
                    {
                        // Auto-start failure is non-fatal — user can toggle manually.
                    }
                }
            });
        }

        if (_settings.AutoShowDamageOverlay)
        {
            BeginInvoke(() =>
            {
                if (_damageOverlay is null)
                {
                    try { ToggleDamageOverlay(); } catch { /* non-fatal */ }
                }
            });
        }
    }

    private sealed record CardBinding(Panel Card, Button CompleteButton);

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    private static readonly IntPtr HwndTopMost = new(-1);
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpShowWindow = 0x0040;

    private void InitializeShell()
    {
        Text = _data.Title;
        MinimumSize = new Size(1040, 720);
        Size = new Size(1240, 820);
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        BackColor = _background;
        ForeColor = _text;
        Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
        AutoScaleMode = AutoScaleMode.Dpi;
        HandleCreated += (_, _) => EnableDarkMode();
    }

    private void EnableDarkMode()
    {
        var dark = 1;
        DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int)); // DWMWA_USE_IMMERSIVE_DARK_MODE
        DwmSetWindowAttribute(Handle, 19, ref dark, sizeof(int)); // older builds

        // BGR format: 0x00BBGGRR
        // Caption color — match the panel color (#101A24 → BGR 0x241A10)
        var captionColor = 0x00241A10;
        DwmSetWindowAttribute(Handle, 35, ref captionColor, sizeof(int)); // DWMWA_CAPTION_COLOR

        // Border color — gold accent (#F2B84B → BGR 0x4BB8F2)
        var borderColor = 0x004BB8F2;
        DwmSetWindowAttribute(Handle, 34, ref borderColor, sizeof(int)); // DWMWA_BORDER_COLOR

        // Caption text color — gold accent
        DwmSetWindowAttribute(Handle, 36, ref borderColor, sizeof(int)); // DWMWA_TEXT_COLOR

        // Win11+ Mica/Acrylic backdrop — DWMWA_SYSTEMBACKDROP_TYPE = 38, value 2 = Mica.
        // Silently no-ops on Win10. Adds the frosted-glass look behind controls.
        var mica = 2;
        DwmSetWindowAttribute(Handle, 38, ref mica, sizeof(int));
    }

    private void BuildUi()
    {
        Controls.Clear();
        _cards.Clear();
        _cardBindings.Clear();

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(18, 14, 18, 18),
            BackColor = _background
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildCategories(), 0, 1);
    }

    private Control BuildHeader()
    {
        // Flat themed background — no image. Uses the panel color so it blends with the
        // tab strip and content panels for a cleaner look.
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 110,
            BackColor = _panel
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(18, 0, 18, 0),
            BackColor = _background
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.Controls.Add(layout);

        var brandStack = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0),
            Padding = new Padding(0),
            Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Bottom,
            BackColor = _background
        };

        var titleIconPath = Path.Combine(AppContext.BaseDirectory, "data", "images", "AppData", "Icon-removebg-preview.png");
        if (File.Exists(titleIconPath))
        {
            var titleIcon = new PictureBox
            {
                Width = 56,
                Height = 56,
                SizeMode = PictureBoxSizeMode.Zoom,
                ImageLocation = titleIconPath,
                Margin = new Padding(0, 20, 0, 20),
                BackColor = Color.Transparent
            };
            titleIcon.Paint += (_, e) =>
            {
                using var glowPen = new Pen(Color.FromArgb(80, _accentAlt.R, _accentAlt.G, _accentAlt.B), 4);
                using var border = new Pen(_accentAlt, 1.5f);
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                e.Graphics.DrawEllipse(glowPen, 2, 2, titleIcon.Width - 5, titleIcon.Height - 5);
                e.Graphics.DrawEllipse(border, 2, 2, titleIcon.Width - 5, titleIcon.Height - 5);
            };
            brandStack.Controls.Add(titleIcon);
        }

        // Vertical accent divider
        var divider = new Panel
        {
            Width = 2,
            Height = 44,
            Margin = new Padding(14, 28, 16, 28),
            BackColor = Color.Transparent
        };
        divider.Paint += (_, e) =>
        {
            using var topColor = new SolidBrush(Color.FromArgb(0, _accentAlt));
            using var midColor = new SolidBrush(_accentAlt);
            using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                new Point(0, 0), new Point(0, divider.Height),
                Color.FromArgb(0, _accentAlt), _accentAlt);
            var blend = new System.Drawing.Drawing2D.ColorBlend(3);
            blend.Colors = [Color.FromArgb(0, _accentAlt), _accentAlt, Color.FromArgb(0, _accentAlt)];
            blend.Positions = [0f, 0.5f, 1f];
            brush.InterpolationColors = blend;
            e.Graphics.FillRectangle(brush, 0, 0, divider.Width, divider.Height);
        };
        brandStack.Controls.Add(divider);

        var titleStack = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 0),
            Padding = new Padding(0),
            BackColor = Color.Transparent,
            Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Bottom
        };
        titleStack.Controls.Add(new Label
        {
            AutoSize = true,
            Text = _data.Title,
            ForeColor = Color.White,
            Font = new Font(Font.FontFamily, 17F, FontStyle.Bold),
            Margin = new Padding(0, 24, 0, 0)
        });

        brandStack.Controls.Add(titleStack);

        layout.Controls.Add(brandStack, 0, 0);
        layout.Controls.Add(BuildHeaderPatchNoteCard(), 1, 0);
        layout.Controls.Add(BuildTopToolbar(), 2, 0);
        return header;
    }

    private Control BuildHeaderPatchNoteCard()
    {
        // Compact, broadcast-style center column:
        //   • Tag: LATEST NEWS (small caps, gold)
        //   • Ticker: smooth scrolling marquee of recent SWTOR news headlines
        //   • Divider: thin gold line
        //   • Quote: rotating SWTOR lore (italic, click to cycle)
        var host = new Panel
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            Margin = new Padding(28, 14, 28, 14),
            BackColor = _panel,
            Padding = new Padding(0)
        };

        var labelTag = new Label
        {
            AutoSize = false,
            Width = 100,
            Height = 14,
            Text = "LATEST NEWS",
            ForeColor = _accent,
            Font = new Font(Font.FontFamily, 8F, FontStyle.Bold),
            Location = new Point(0, 2),
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.Transparent
        };
        host.Controls.Add(labelTag);

        _newsTicker = new NewsTickerPanel
        {
            Location = new Point(0, 18),
            Width = 100,
            Height = 26,
            BackColor = _panel,
            ForeColor = Color.White,
            TextColor = Color.White,
            SeparatorColor = _accentAlt,
            Font = new Font(Font.FontFamily, 10.5F, FontStyle.Bold),
            PixelsPerSecond = 55,
            EdgeFadeWidth = 28
        };
        _newsTicker.ItemClicked += url =>
        {
            // Open inside the News tab — switch to it, find the matching headline,
            // and reuse the existing in-app article viewer.
            try
            {
                var newsIdx = _mainTabPages.FindIndex(p => p.Tab.Text.Contains("News", StringComparison.OrdinalIgnoreCase));
                if (newsIdx >= 0)
                {
                    SelectMainTab(newsIdx);
                }

                var entry = _newsService.News.FirstOrDefault(n => n.Url == url);
                if (entry is not null)
                {
                    OpenArticleViewer(entry.Title, entry.Url);
                }
            }
            catch { /* best-effort */ }
        };
        host.Controls.Add(_newsTicker);

        var divider = new Panel
        {
            Width = 100,
            Height = 1,
            Location = new Point(0, 50),
            BackColor = Color.FromArgb(60, _accentAlt)
        };
        host.Controls.Add(divider);

        _loreFooterLabel = new Label
        {
            AutoSize = false,
            Width = 100,
            Height = 18,
            Text = "",
            ForeColor = _muted,
            Font = new Font(Font.FontFamily, 8.75F, FontStyle.Italic),
            Location = new Point(0, 56),
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand,
            AutoEllipsis = true
        };
        host.Controls.Add(_loreFooterLabel);

        host.Resize += (_, _) => SyncHeaderPatchWidths(host);

        // Quote rotation
        _loreFooterIndex = _loreRandom.Next(SwtorLoreQuotes.Quotes.Length);
        UpdateLoreFooter();
        void Cycle(object? s, EventArgs e)
        {
            _loreFooterIndex = (_loreFooterIndex + 1) % SwtorLoreQuotes.Quotes.Length;
            UpdateLoreFooter();
        }
        _loreFooterLabel.Click += Cycle;
        _loreFooterTimer = new System.Windows.Forms.Timer { Interval = 60_000 };
        _loreFooterTimer.Tick += Cycle;
        _loreFooterTimer.Start();

        _headerPatchPanel = host;
        RefreshHeaderTicker();
        return host;
    }

    private void SyncHeaderPatchWidths(Panel host)
    {
        if (_newsTicker is not null)
        {
            _newsTicker.Width = host.ClientSize.Width;
        }
        if (_loreFooterLabel is not null)
        {
            _loreFooterLabel.Width = host.ClientSize.Width;
        }
        foreach (Control c in host.Controls.OfType<Control>())
        {
            if (c.Height == 1)
            {
                c.Width = host.ClientSize.Width;
            }
        }
    }

    private void RefreshHeaderTicker()
    {
        if (_newsTicker is null) return;
        var items = _newsService.News
            .Take(8)
            .Select(n => (n.Title, n.Url))
            .Where(t => !string.IsNullOrWhiteSpace(t.Title))
            .ToList();
        _newsTicker.SetItems(items);

        // If the fetch failed, show the error so the user knows the ticker isn't stuck.
        if (items.Count == 0 && !string.IsNullOrEmpty(_newsService.NewsError))
        {
            _newsTicker.PlaceholderText = $"News fetch failed: {_newsService.NewsError}";
        }
        else if (items.Count == 0)
        {
            _newsTicker.PlaceholderText = "Loading SWTOR news…";
        }
    }

    private void UpdateLoreFooter()
    {
        if (_loreFooterLabel is null)
        {
            return;
        }
        var (quote, source) = SwtorLoreQuotes.Quotes[_loreFooterIndex];
        _loreFooterLabel.Text = $"“{quote}”  —  {source}";
    }

    private void RefreshHeaderPatchNote()
    {
        if (_headerPatchTitleLabel is null || _headerPatchDateLabel is null)
        {
            return;
        }

        var entry = _newsService.PatchNotes.FirstOrDefault();
        if (entry is null)
        {
            _headerPatchTitleLabel.Text = _newsService.PatchNotesError is null
                ? "Loading patch notes…"
                : "Patch notes unavailable right now.";
            _headerPatchDateLabel.Text = "";
            _headerPatchUrl = null;
            return;
        }

        _headerPatchTitleLabel.Text = entry.Title;
        _headerPatchDateLabel.Text = string.IsNullOrWhiteSpace(entry.Date)
            ? "Click to open on swtor.com →"
            : $"{entry.Date}   ·   click to open on swtor.com →";
        _headerPatchUrl = entry.Url;
        if (_headerPatchPanel is not null)
        {
            SyncHeaderPatchWidths(_headerPatchPanel);
        }
    }

    private void OpenHeaderPatchUrl()
    {
        if (string.IsNullOrWhiteSpace(_headerPatchUrl))
        {
            return;
        }
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = _headerPatchUrl,
                UseShellExecute = true,
            });
        }
        catch { /* best-effort */ }
    }

    private Control BuildTopToolbar()
    {
        // Outer container: two rows — top row (version + reset), bottom row (clocks)
        var toolbar = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(12, 8, 0, 8),
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        toolbar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        toolbar.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // Top row: version chip
        var topRow = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 6),
            Padding = new Padding(0)
        };
        topRow.Controls.Add(BuildVersionChip());

        toolbar.Controls.Add(topRow, 0, 0);
        toolbar.Controls.Add(BuildResetTimerChip(), 0, 1);
        return toolbar;
    }

    private Control BuildQuestSubBar()
    {
        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 0, 0, 10),
            BackColor = Color.FromArgb(7, 13, 19)
        };

        bar.Controls.Add(MakeSwitch("Hide completed", _hideCompleted, value =>
        {
            _hideCompleted = value;
            RefreshProgressState();
        }));

        bar.Controls.Add(MakeSwitch("Quest tracker", _questTracker is { IsDisposed: false }, value =>
        {
            if (value)
            {
                _questTracker ??= new OcrMonitorForm(_data, _progress) { ShowInTaskbar = false };
                if (_questTracker.IsDisposed)
                {
                    _questTracker = new OcrMonitorForm(_data, _progress) { ShowInTaskbar = false };
                }

                _questTracker.StartWatching();
            }
            else if (_questTracker is { IsDisposed: false })
            {
                _questTracker.StopWatching();
                _questTracker.Hide();
            }

            RefreshProgressState();
        }));

        _questSubBar = bar;
        return bar;
    }

    private Control BuildResetTimerChip()
    {
        var panel = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = Color.Transparent
        };

        panel.Controls.Add(BuildClockBlock("Daily Reset", ref _dailyCountdownLabel));
        panel.Controls.Add(BuildClockBlock("Weekly Reset", ref _weeklyCountdownLabel));
        panel.Controls.Add(BuildClockBlock("Conquest Reset", ref _conquestCountdownLabel));

        UpdateResetTimer();
        return panel;
    }

    private Control BuildClockBlock(string title, ref Label? countdownLabel)
    {
        var block = new Panel
        {
            AutoSize = false,
            Width = 200,
            Height = 52,
            BackColor = Color.FromArgb(160, 6, 14, 24),
            Margin = new Padding(0, 0, 8, 0),
            Padding = new Padding(12, 6, 12, 6)
        };
        block.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var accentBar = new SolidBrush(_accentAlt);
            e.Graphics.FillRectangle(accentBar, 0, 0, block.Width, 2);
            using var border = new Pen(Color.FromArgb(55, _accentAlt.R, _accentAlt.G, _accentAlt.B), 1);
            e.Graphics.DrawRectangle(border, 0, 0, block.Width - 1, block.Height - 1);
        };

        var titleLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 18,
            Text = title.ToUpperInvariant(),
            ForeColor = Color.FromArgb(100, 140, 180),
            Font = new Font(Font.FontFamily, 7F, FontStyle.Bold),
            TextAlign = ContentAlignment.BottomLeft,
            BackColor = Color.Transparent,
            Padding = new Padding(0)
        };

        var clock = new Label
        {
            Dock = DockStyle.Fill,
            Text = "--Hr --Mins",
            ForeColor = Color.White,
            Font = new Font("Consolas", 13F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.Transparent
        };

        block.Controls.Add(clock);
        block.Controls.Add(titleLabel);
        countdownLabel = clock;
        return block;
    }

    private void UpdateResetTimer()
    {
        if (_dailyCountdownLabel is null || _weeklyCountdownLabel is null || _conquestCountdownLabel is null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        _dailyCountdownLabel.Text = FormatCountdown(NextDailyMissionReset(now) - now);
        _weeklyCountdownLabel.Text = FormatCountdown(NextWeeklyMissionReset(now) - now);
        _conquestCountdownLabel.Text = FormatCountdown(NextConquestReset(now) - now);
    }

    private static DateTimeOffset NextDailyMissionReset(DateTimeOffset now)
    {
        var reset = new DateTimeOffset(now.Year, now.Month, now.Day, 12, 0, 0, TimeSpan.Zero);
        return now < reset ? reset : reset.AddDays(1);
    }

    private static DateTimeOffset NextWeeklyMissionReset(DateTimeOffset now)
    {
        var reset = NextDailyMissionReset(now);
        while (reset.DayOfWeek != DayOfWeek.Tuesday)
        {
            reset = reset.AddDays(1);
        }

        return reset;
    }

    private static DateTimeOffset NextConquestReset(DateTimeOffset now)
    {
        var reset = new DateTimeOffset(now.Year, now.Month, now.Day, 18, 0, 0, TimeSpan.Zero);
        while (reset.DayOfWeek != DayOfWeek.Tuesday || now >= reset)
        {
            reset = reset.AddDays(1);
        }

        return reset;
    }

    private static string FormatCountdown(TimeSpan span)
    {
        if (span < TimeSpan.Zero)
        {
            span = TimeSpan.Zero;
        }

        var days = (int)span.TotalDays;
        var hours = span.Hours;
        var minutes = span.Minutes;

        if (days > 0)
        {
            return $"{days}D {hours}Hr {minutes}Mins";
        }

        if (hours > 0)
        {
            return $"{hours}Hr {minutes}Mins";
        }

        return $"{minutes}Mins";
    }

    private Control BuildVersionChip()
    {
        var chip = new Panel
        {
            AutoSize = false,
            Width = 624,
            Height = 26,
            Margin = new Padding(0, 0, 0, 0),
            BackColor = Color.FromArgb(140, 6, 14, 24)
        };
        chip.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var accentBar = new SolidBrush(_accentAlt);
            e.Graphics.FillRectangle(accentBar, 0, 0, 3, chip.Height);
            using var border = new Pen(Color.FromArgb(55, _accentAlt.R, _accentAlt.G, _accentAlt.B), 1);
            e.Graphics.DrawRectangle(border, 0, 0, chip.Width - 1, chip.Height - 1);
        };
        _versionChipLabel = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            Text = $"{_data.VersionLabel}  |  Verified {_data.LastVerified}",
            ForeColor = _accentAlt,
            BackColor = Color.Transparent,
            Font = new Font(Font.FontFamily, 9F, FontStyle.Bold),
            Padding = new Padding(8, 0, 6, 0),
            TextAlign = ContentAlignment.MiddleCenter
        };
        chip.Controls.Add(_versionChipLabel);
        // Initial refresh — once news service has data, this'll get updated again.
        RefreshVersionChip();
        return chip;
    }

    private void RefreshVersionChip()
    {
        if (_versionChipLabel is null) return;

        // Pull the latest patch note title and extract the "Game Update X.Y.Zx" version.
        // Falls back to the seed values from data/dailies.json if the news service
        // hasn't loaded yet or the title doesn't match the expected format.
        var latest = _newsService.PatchNotes.FirstOrDefault();
        var version = _data.VersionLabel;
        var verified = _data.LastVerified;

        if (latest is not null)
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                latest.Title,
                @"Game Update\s+\d+(?:\.\d+){0,2}[a-z]?",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success)
            {
                version = match.Value;
                // Successful fetch from swtor.com → mark as verified today.
                verified = DateTime.Now.ToString("yyyy-MM-dd");
            }
        }

        _versionChipLabel.Text = $"{version}  |  Verified {verified}";
    }

    private static Image? TryLoadImage(string path)
    {
        if (!File.Exists(path)) return null;
        try { return Image.FromFile(path); } catch { return null; }
    }

    private Control WrapWithBackground2(Control content)
    {
        return WrapWithBackground(content, "Background2.jpg");
    }

    private Control WrapWithBackground(Control content, string filename)
    {
        var bgPath = Path.Combine(AppContext.BaseDirectory, "data", "images", "Backgrounds", filename);
        Image? bgImage = TryLoadImage(bgPath);

        var host = new BackgroundPanel(bgImage, Color.FromArgb(170, 10, 16, 26))
        {
            Dock = DockStyle.Fill,
            BackColor = _background
        };

        content.Dock = DockStyle.Fill;
        host.Controls.Add(content);
        return host;
    }

    private readonly List<(Button Tab, Panel Page)> _mainTabPages = [];

    private Control BuildCategories()
    {
        var host = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = _background
        };
        host.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        host.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var tabStrip = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = _background,
            Padding = new Padding(0, 0, 0, 0),
            Margin = new Padding(0)
        };

        var contentArea = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = _background
        };

        var pages = new[]
        {
            ("News",          (Control)WrapWithBackground2(BuildNewsPage())),
            ("Quests",        (Control)WrapWithBackground2(BuildQuestsPanel())),
            ("Crew Skills",    WrapWithBackground(BuildCrewSkillPage(), "Background5.jpg")),
            ("Damage Meter",  WrapWithBackground(BuildDamageMeterPage(), "Background7.jpg")),
            (_datacrons.Title, WrapWithBackground2(BuildDatacronPage())),
            ("SWTOR & Chill",  WrapWithBackground(BuildChillPage(), "Background6.jpg")),
            (_redeemCodes.Title, WrapWithBackground2(BuildRedeemCodePage())),
            ("Server Status", BuildServerStatusPage()),
            ("Settings",      WrapWithBackground2(BuildSettingsPage()))
        };

        _mainTabPages.Clear();
        for (var i = 0; i < pages.Length; i++)
        {
            var (label, content) = pages[i];
            var idx = i;

            var page = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = _background,
                Visible = i == 0
            };
            content.Dock = DockStyle.Fill;
            page.Controls.Add(content);
            contentArea.Controls.Add(page);

            var btn = new Button
            {
                Text = label,
                FlatStyle = FlatStyle.Flat,
                BackColor = i == 0 ? Color.FromArgb(43, 57, 73) : Color.FromArgb(18, 28, 40),
                ForeColor = i == 0 ? _text : Color.FromArgb(202, 215, 231),
                Font = new Font(Font.FontFamily, 11F, FontStyle.Bold),
                Width = 180,
                Height = 42,
                Margin = new Padding(0, 0, 2, 0)
            };
            btn.FlatAppearance.BorderColor = i == 0 ? _accentAlt : Color.FromArgb(47, 63, 82);
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(36, 50, 66);
            btn.Click += (_, _) => SelectMainTab(idx);
            AttachTabHoverGlow(btn);
            tabStrip.Controls.Add(btn);
            _mainTabPages.Add((btn, page));
        }

        host.Controls.Add(tabStrip, 0, 0);
        host.Controls.Add(contentArea, 0, 1);
        return host;
    }

    private void AttachTabHoverGlow(Button btn)
    {
        // Soft gold radial glow that fades in/out on hover. Uses a per-button timer
        // to interpolate intensity smoothly.
        float intensity = 0f;
        bool hovering = false;
        var timer = new System.Windows.Forms.Timer { Interval = 16 };
        timer.Tick += (_, _) =>
        {
            var target = hovering ? 1f : 0f;
            var step = 0.10f;
            if (Math.Abs(intensity - target) < 0.01f)
            {
                intensity = target;
                timer.Stop();
            }
            else
            {
                intensity += intensity < target ? step : -step;
                intensity = Math.Clamp(intensity, 0f, 1f);
            }
            btn.Invalidate();
        };

        btn.MouseEnter += (_, _) => { hovering = true; if (!timer.Enabled) timer.Start(); };
        btn.MouseLeave += (_, _) => { hovering = false; if (!timer.Enabled) timer.Start(); };

        // Custom paint: render the glow before the default button text.
        btn.Paint += (s, e) =>
        {
            if (intensity <= 0.01f || s is not Button b) return;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // Radial gradient centered on the cursor-x for a subtle "follow" feel.
            // Cheap approximation: just centered on the button.
            var glowAlpha = (int)(60 * intensity);
            using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                new Point(0, 0), new Point(0, b.Height),
                Color.FromArgb(glowAlpha, _accentAlt),
                Color.FromArgb(0, _accentAlt));
            var blend = new System.Drawing.Drawing2D.ColorBlend(3)
            {
                Colors = [Color.FromArgb(0, _accentAlt), Color.FromArgb(glowAlpha, _accentAlt), Color.FromArgb(0, _accentAlt)],
                Positions = [0f, 0.5f, 1f]
            };
            brush.InterpolationColors = blend;
            e.Graphics.FillRectangle(brush, 1, 1, b.Width - 2, b.Height - 2);

            // Thin gold accent line at the bottom on hover.
            using var underlinePen = new Pen(Color.FromArgb((int)(180 * intensity), _accentAlt), 1);
            e.Graphics.DrawLine(underlinePen, 6, b.Height - 2, b.Width - 6, b.Height - 2);
        };

        btn.Disposed += (_, _) =>
        {
            try { timer.Stop(); timer.Dispose(); } catch { }
        };
    }

    private void SelectMainTab(int idx)
    {
        if (_mainTabPages.Count == 0 || idx < 0 || idx >= _mainTabPages.Count)
        {
            return;
        }

        var newPage = _mainTabPages[idx].Page;

        // Snapshot the currently-visible page BEFORE swapping visibility so we can
        // cross-fade from it onto the new page.
        Bitmap? snapshot = null;
        Panel? oldPage = null;
        foreach (var (_, page) in _mainTabPages)
        {
            if (page.Visible && page != newPage)
            {
                oldPage = page;
                break;
            }
        }

        if (oldPage is not null && oldPage.Width > 0 && oldPage.Height > 0)
        {
            try
            {
                snapshot = new Bitmap(oldPage.Width, oldPage.Height);
                oldPage.DrawToBitmap(snapshot, new Rectangle(0, 0, oldPage.Width, oldPage.Height));
            }
            catch
            {
                snapshot?.Dispose();
                snapshot = null;
            }
        }

        for (var i = 0; i < _mainTabPages.Count; i++)
        {
            var (btn, page) = _mainTabPages[i];
            var selected = i == idx;
            page.Visible = selected;
            btn.BackColor = selected ? Color.FromArgb(43, 57, 73) : Color.FromArgb(18, 28, 40);
            btn.ForeColor = selected ? _text : Color.FromArgb(202, 215, 231);
            btn.FlatAppearance.BorderColor = selected ? _accentAlt : Color.FromArgb(47, 63, 82);
        }

        if (snapshot is not null)
        {
            AnimateTabFade(newPage, snapshot);
        }
    }

    private void AnimateTabFade(Panel newPage, Bitmap snapshot)
    {
        // Add a custom-painted overlay child of the NEW page that draws the snapshot
        // of the OLD page at decreasing alpha. As alpha approaches 0 the new page's
        // real contents emerge underneath.
        var overlay = new TabFadeOverlay
        {
            Dock = DockStyle.Fill,
            Snapshot = snapshot,
            Alpha = 1f
        };
        newPage.Controls.Add(overlay);
        overlay.BringToFront();

        const int totalFrames = 10; // ~160ms at 16ms intervals
        var frame = 0;
        var timer = new System.Windows.Forms.Timer { Interval = 16 };
        timer.Tick += (_, _) =>
        {
            frame++;
            if (overlay.IsDisposed || frame >= totalFrames)
            {
                timer.Stop();
                timer.Dispose();
                if (!overlay.IsDisposed)
                {
                    newPage.Controls.Remove(overlay);
                    snapshot.Dispose();
                    overlay.Dispose();
                }
                return;
            }
            overlay.Alpha = 1f - (frame / (float)totalFrames);
            overlay.Invalidate();
        };
        timer.Start();
    }

    private sealed class TabFadeOverlay : Panel
    {
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Bitmap? Snapshot { get; set; }
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public float Alpha { get; set; } = 1f;

        public TabFadeOverlay()
        {
            DoubleBuffered = true;
            SetStyle(
                ControlStyles.UserPaint
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.SupportsTransparentBackColor,
                true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Snapshot is null)
            {
                return;
            }
            using var attrs = new System.Drawing.Imaging.ImageAttributes();
            var matrix = new System.Drawing.Imaging.ColorMatrix { Matrix33 = Math.Clamp(Alpha, 0f, 1f) };
            attrs.SetColorMatrix(matrix);
            e.Graphics.DrawImage(
                Snapshot,
                new Rectangle(0, 0, Width, Height),
                0, 0, Snapshot.Width, Snapshot.Height,
                GraphicsUnit.Pixel,
                attrs);
        }
    }

    private Control BuildQuestsPanel()
    {
        var host = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = _background
        };
        host.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // sub-bar
        host.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // tab buttons
        host.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // content

        host.Controls.Add(BuildQuestSubBar(), 0, 0);

        var tabRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
            BackColor = _background,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        tabRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        tabRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        // Custom tab button strip — no OS chrome at all
        var tabStrip = new FlowLayoutPanel
        {
            Dock = DockStyle.Left,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = _background,
            Padding = new Padding(0, 0, 0, 0),
            Margin = new Padding(0)
        };

        var reset = MakeResetButton();
        reset.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        reset.Margin = new Padding(0, 3, 20, 0);
        reset.Click += (_, _) => _progress.Reset();

        // Content area — swaps pages in/out
        var contentArea = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = _background
        };

        _questTabPages.Clear();
        var categories = BuildPrimaryCategories();
        for (var i = 0; i < categories.Count; i++)
        {
            var category = categories[i];
            var idx = i;

            var page = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = _background,
                Visible = i == 0
            };
            page.Controls.Add(BuildCategoryPage(category));
            contentArea.Controls.Add(page);

            var tabText = BuildTabText(category.Name, category.Items);
            var btn = new Button
            {
                Text = tabText,
                FlatStyle = FlatStyle.Flat,
                BackColor = i == 0 ? Color.FromArgb(43, 57, 73) : Color.FromArgb(18, 28, 40),
                ForeColor = i == 0 ? _text : Color.FromArgb(202, 215, 231),
                Font = new Font(Font.FontFamily, 9.5F, FontStyle.Bold),
                Width = 158,
                Height = 34,
                Margin = new Padding(0, 0, 2, 0),
                Tag = idx
            };
            btn.FlatAppearance.BorderColor = i == 0 ? _accentAlt : Color.FromArgb(47, 63, 82);
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(36, 50, 66);

            btn.Click += (_, _) => SelectQuestTab(idx);
            tabStrip.Controls.Add(btn);
            _questTabPages.Add((btn, page));
        }

        tabRow.Controls.Add(tabStrip, 0, 0);
        tabRow.Controls.Add(reset, 1, 0);

        host.Controls.Add(tabRow, 0, 1);
        host.Controls.Add(contentArea, 0, 2);
        return host;
    }

    private void SelectQuestTab(int idx)
    {
        _questSelectedTab = idx;
        for (var i = 0; i < _questTabPages.Count; i++)
        {
            var (btn, page) = _questTabPages[i];
            var selected = i == idx;
            page.Visible = selected;
            btn.BackColor = selected ? Color.FromArgb(43, 57, 73) : Color.FromArgb(18, 28, 40);
            btn.ForeColor = selected ? _text : Color.FromArgb(202, 215, 231);
            btn.FlatAppearance.BorderColor = selected ? _accentAlt : Color.FromArgb(47, 63, 82);
        }
    }

    private void RefreshProgressState()
    {
        SuspendLayout();
        try
        {
            foreach (var pair in _cardBindings)
            {
                var completed = _progress.IsCompleted(pair.Key);
                foreach (var binding in pair.Value)
                {
                    binding.Card.BackColor = completed ? Color.FromArgb(0, 55, 35) : _panel;
                    binding.CompleteButton.Text = completed ? "Undo" : "Done";
                    binding.Card.Visible = !_hideCompleted || !completed;
                }
            }

            UpdateTabCounts(this);
        }
        finally
        {
            ResumeLayout(true);
        }
    }

    private void UpdateTabCounts(Control root)
    {
        foreach (Control child in root.Controls)
        {
            if (child is TabControl tabs)
            {
                UpdateTabCounts(tabs);
            }

            UpdateTabCounts(child);
        }
    }

    private void UpdateTabCounts(TabControl tabs)
    {
        var categories = BuildPrimaryCategories();
        foreach (TabPage page in tabs.TabPages)
        {
            var category = categories.FirstOrDefault(item => page.Text.StartsWith(item.Name, StringComparison.OrdinalIgnoreCase));
            if (category is not null)
            {
                page.Text = BuildTabText(category.Name, category.Items);
            }
        }

        tabs.Invalidate();

        // Update custom quest tab buttons
        UpdateQuestTabCounts();
    }

    private void UpdateQuestTabCounts()
    {
        var categories = BuildPrimaryCategories();
        for (var i = 0; i < _questTabPages.Count && i < categories.Count; i++)
        {
            _questTabPages[i].Tab.Text = BuildTabText(categories[i].Name, categories[i].Items);
        }
    }

    private List<CategoryData> BuildPrimaryCategories()
    {
        var daily = FindCategory("Daily").Items;
        var weekly = FindCategory("Weekly").Items;
        var pvp = FindCategory("PvP").Items;
        var operations = FindCategory("Operations").Items;

        return
        [
            new CategoryData
            {
                Name = "Daily",
                Description = "The short daily loop: earn conquest, check the active Season objective, then do only the fastest useful missions.",
                Items =
                [
                    MakeGuideItem(
                        "Daily Conquest: 25,000 Personal Conquest Points",
                        "Legacy",
                        "Daily login priority",
                        "Every Galactic Season day starts here. Use any fast activity that also advances the current weekly list.",
                        ["Conquest progress", "Galactic Season daily points"],
                        ["Daily", "Season", "Conquest"]),
                    MakeGuideItem(
                        "Current Companion Objective",
                        "Galaxy",
                        "Season 10",
                        "When Season 10 asks for Altuur zok Adon or PH4-LNX kills, keep that companion out while doing normal daily missions.",
                        ["Season achievement progress", "Weekly objective progress"],
                        ["Daily", "Companion", "Season 10"]),
                    MakeGuideItem(
                        "Dynamic Encounter Check",
                        "Galaxy Map",
                        "Fast open-world activity",
                        "Run Dynamic Encounters only when they overlap with the current Galactic Season or conquest objectives.",
                        ["Conquest progress", "Season objective progress", "Event rewards where applicable"],
                        ["Daily", "Dynamic Encounters"]),
                    .. daily
                        .Where(item => item.Planet is "CZ-198" or "Black Hole")
                        .Take(8)
                ]
            },
            new CategoryData
            {
                Name = "Weekly",
                Description = "The weekly board should answer one question: which objectives are worth finishing before Tuesday reset?",
                Items =
                [
                    MakeGuideItem(
                        "Galactic Season Weeklies: Complete 7",
                        "Legacy",
                        "Weekly priority",
                        "Pick the easiest 7 of 11 objectives for the week. Favor objectives that overlap with conquest, flashpoints, daily areas, or Dynamic Encounters.",
                        ["Galactic Season points", "Reward track progress"],
                        ["Weekly", "Season"]),
                    MakeGuideItem(
                        "Weekly Conquest: 200,000 Personal Conquest Points",
                        "Legacy",
                        "Weekly priority",
                        "When the Season objective is active, this is one of the cleanest long-form goals because almost everything useful contributes to it.",
                        ["Conquest progress", "Season weekly progress"],
                        ["Weekly", "Conquest"]),
                    .. weekly
                ]
            },
            new CategoryData
            {
                Name = "Season",
                Description = "Season 10: Secrets of the Syndicate. Track limited-time progress and companion goals here instead of scattering them through every mission tab.",
                Items =
                [
                    MakeGuideItem(
                        "Season 10 Reward Track",
                        "Legacy",
                        "March 10 - July 27, 2026",
                        "Earn Season points from the daily objective, up to 7 weeklies, and login progress. This is the main limited-time reward path in 7.8.1.",
                        ["Season rewards", "Tokens", "Subscriber track rewards where applicable"],
                        ["Season 10", "Limited time"]),
                    MakeGuideItem(
                        "Altuur zok Adon Progress",
                        "Galaxy",
                        "Companion objective",
                        "Use Altuur when objectives or achievements call for him. His Season 10 achievements are separate from generic conquest progress.",
                        ["Companion achievement progress", "Season cosmetics"],
                        ["Season 10", "Altuur"]),
                    MakeGuideItem(
                        "PH4-LNX Progress",
                        "Galaxy",
                        "Companion objective",
                        "Use PH4-LNX when the weekly objective calls for PH4-LNX kills, role-specific companion kills, or Season 10 achievement progress.",
                        ["Companion achievement progress", "Season cosmetics"],
                        ["Season 10", "PH4-LNX"]),
                    MakeGuideItem(
                        "Current Week 8 Focus: April 28 - May 5",
                        "Legacy",
                        "Current official week",
                        "Prioritize PH4-LNX conquest, PH4-LNX heal-role defeats, Black Hole/Onderon daily sweep, Dantooine Dynamic Encounters, and the listed flashpoints if they fit your playstyle.",
                        ["Season points", "Conquest progress"],
                        ["Season 10", "Week 8"])
                ]
            },
            new CategoryData
            {
                Name = "Endgame",
                Description = "Group and gearing content lives here: flashpoints, PvP, operations, world bosses, and other lockout-style activities.",
                Items =
                [
                    .. weekly.Where(item => item.Planet is "Flashpoint" or "World Bosses" or "Starfighter"),
                    .. pvp,
                    .. operations
                        .Where(item =>
                            item.Name.Contains("Temple of Sacrifice", StringComparison.OrdinalIgnoreCase) ||
                            item.Name.Contains("R-4", StringComparison.OrdinalIgnoreCase) ||
                            item.Name.Contains("Ravagers", StringComparison.OrdinalIgnoreCase) ||
                            item.Name.Contains("Heart of Ruin", StringComparison.OrdinalIgnoreCase) ||
                            item.Name.Contains("Mountain Queen", StringComparison.OrdinalIgnoreCase))
                ]
            },
            new CategoryData
            {
                Name = "Story & Unlocks",
                Description = "Permanent story, unlock chains, and 7.8/7.8.1 content that matters once, occasionally, or when you are catching up.",
                Items =
                [
                    MakeGuideItem(
                        "7.8 Pursuit of Ruin Story",
                        "Story",
                        "Main story",
                        "Continue Galactic Threads and related Legacy of the Sith story requirements before treating repeatable endgame as the main loop.",
                        ["Story unlocks", "Quest progression"],
                        ["7.8", "Story"]),
                    MakeGuideItem(
                        "Dantooine Crash Site",
                        "Dantooine",
                        "7.8 Dynamic Encounters",
                        "Use this as the home for Wreckage on Dantooine progress, biome encounters, and Orbital Core-related unlocks.",
                        ["Dynamic Encounter rewards", "Priority Targets progress"],
                        ["7.8", "Dantooine"]),
                    MakeGuideItem(
                        "7.8.1 Master's Enigma Story",
                        "Story",
                        "Current story",
                        "Track the 7.8.1 story continuation here so it does not compete with daily and weekly repeatables.",
                        ["Story progression", "Unlock progress"],
                        ["7.8.1", "Story"]),
                    MakeGuideItem(
                        "Date Night Missions",
                        "Companions",
                        "Repeatable unlock",
                        "Keep Torian Cadera and Kira Carsen Date Night missions under permanent unlock content rather than the daily grind.",
                        ["Decorations", "Companion content"],
                        ["7.8.1", "Date Night"])
                ]
            }
        ];
    }

    private CategoryData FindCategory(string name)
    {
        return _data.Categories.FirstOrDefault(category => category.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            ?? new CategoryData { Name = name };
    }

    private static DailyRecommendation MakeGuideItem(
        string name,
        string planet,
        string priority,
        string why,
        List<string> rewards,
        List<string> tags)
    {
        return new DailyRecommendation
        {
            Name = name,
            Planet = planet,
            Priority = priority,
            Time = "Varies",
            Access = "Planner guidance",
            Location = "Use the in-game mission, conquest, or season panel",
            LevelRequirement = "Any eligible character",
            Why = why,
            Objectives = ["Check current in-game objective", "Complete only when it overlaps with your active goals"],
            Rewards = rewards,
            Tags = tags,
            MissionAliases = [name]
        };
    }

    private string BuildTabText(string name, List<DailyRecommendation> items)
    {
        if (items.Count == 0)
        {
            return name;
        }

        var completed = items.Count(item => _progress.IsCompleted(item.Name));
        return $"{name} {completed}/{items.Count}";
    }

    private void PaintTabControlBackground(object? sender, PaintEventArgs e)
    {
        if (sender is not TabControl tabs) return;
        using var brush = new SolidBrush(_background);
        e.Graphics.FillRectangle(brush, tabs.ClientRectangle);
    }

    private void DrawNavigationTab(object? sender, DrawItemEventArgs e)
    {
        if (sender is not TabControl tabs)
        {
            return;
        }

        var selected = e.Index == tabs.SelectedIndex;
        var bounds = e.Bounds;
        bounds.Inflate(-2, -2);

        using var hoverlessBackground = new SolidBrush(selected ? Color.FromArgb(43, 57, 73) : Color.FromArgb(18, 28, 40));
        using var selectedAccent = new SolidBrush(_accentAlt);
        using var border = new Pen(selected ? _accentAlt : Color.FromArgb(47, 63, 82), 1);
        e.Graphics.FillRectangle(hoverlessBackground, bounds);
        e.Graphics.DrawRectangle(border, bounds);
        if (selected)
        {
            e.Graphics.FillRectangle(selectedAccent, bounds.Left + 1, bounds.Bottom - 4, bounds.Width - 2, 3);
        }

        var text = tabs.TabPages[e.Index].Text;
        text = text.Replace("Story & Unlocks", "Story");
        TextRenderer.DrawText(
            e.Graphics,
            text,
            tabs.Font,
            bounds,
            selected ? _text : Color.FromArgb(202, 215, 231),
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private Control BuildChillPage()
    {
        var host = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.Transparent,
            Padding = new Padding(28, 22, 28, 28)
        };
        host.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        host.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        host.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        host.Controls.Add(new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            Height = 44,
            Text = "SWTOR & Chill",
            ForeColor = _accentAlt,
            Font = new Font(Font.FontFamily, 22F, FontStyle.Bold),
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = new Padding(0, 0, 0, 8)
        }, 0, 0);

        host.Controls.Add(new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            Height = 30,
            Text = "Open compact browser windows for search, music, and videos while you play.",
            ForeColor = _muted,
            Font = new Font(Font.FontFamily, 10.5F),
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = new Padding(0, 0, 0, 20)
        }, 0, 1);

        var grid = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 0,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));

        var links = new[]
        {
            ("Google", "Search quickly without leaving the game.", "https://www.google.com/"),
            ("YouTube", "Open videos, streams, and playlists.", "https://www.youtube.com/"),
            ("Spotify", "Open the Spotify web player.", "https://open.spotify.com/"),
            ("Twitch", "Watch SWTOR streams or other channels.", "https://www.twitch.tv/"),
            ("Netflix", "Open Netflix in a compact browser window.", "https://www.netflix.com/"),
            ("Disney+", "Open Disney+ in a compact browser window.", "https://www.disneyplus.com/"),
            ("SWTOR Forums", "Check official discussions and updates.", "https://forums.swtor.com/"),
            ("SWTOR.com", "Open the official SWTOR site.", "https://www.swtor.com/"),
            ("Swtorista", "Open guides and reference material.", "https://swtorista.com/")
        };

        for (var i = 0; i < links.Length; i++)
        {
            var (title, description, url) = links[i];
            grid.Controls.Add(BuildChillButton(title, description, url), i % 3, i / 3);
        }

        var scroller = new DarkScrollPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ThumbColor = Color.FromArgb(60, 80, 100),
            TrackColor = Color.FromArgb(7, 13, 19)
        };
        var gridHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent
        };
        gridHost.Controls.Add(grid);
        scroller.Controls.Add(gridHost);

        void CenterGrid()
        {
            gridHost.Height = grid.Height;
            grid.Left = Math.Max(0, (gridHost.ClientSize.Width - grid.Width) / 2);
            grid.Top = 0;
        }

        scroller.Resize += (_, _) =>
        {
            var available = Math.Max(260, scroller.ClientSize.Width - 18);
            var columns = available >= 810 ? 3 : available >= 540 ? 2 : 1;
            grid.ColumnCount = columns;
            grid.ColumnStyles.Clear();
            for (var i = 0; i < columns; i++)
            {
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
            }

            for (var i = 0; i < grid.Controls.Count; i++)
            {
                grid.SetColumn(grid.Controls[i], i % columns);
                grid.SetRow(grid.Controls[i], i / columns);
            }
            grid.PerformLayout();
            CenterGrid();
        };
        gridHost.Resize += (_, _) => CenterGrid();
        grid.Layout += (_, _) => CenterGrid();

        host.Controls.Add(scroller, 0, 2);
        return host;
    }

    private Control BuildChillButton(string title, string description, string url)
    {
        var card = new Panel
        {
            Width = 246,
            Height = 118,
            Margin = new Padding(0, 0, 14, 14),
            BackColor = _panel,
            Cursor = Cursors.Hand
        };

        var titleLabel = new Label
        {
            AutoSize = false,
            Location = new Point(16, 14),
            Size = new Size(214, 26),
            Text = title,
            ForeColor = _text,
            Font = new Font(Font.FontFamily, 12F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };

        var body = new Label
        {
            AutoSize = false,
            Location = new Point(16, 44),
            Size = new Size(214, 42),
            Text = description,
            ForeColor = _muted,
            Font = new Font(Font.FontFamily, 9F),
            Cursor = Cursors.Hand
        };

        var action = new Label
        {
            AutoSize = false,
            Location = new Point(16, 90),
            Size = new Size(214, 18),
            Text = "Open small window",
            ForeColor = _accentAlt,
            Font = new Font(Font.FontFamily, 8.5F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };

        void Open() => OpenChillWindow(url);
        card.Click += (_, _) => Open();
        titleLabel.Click += (_, _) => Open();
        body.Click += (_, _) => Open();
        action.Click += (_, _) => Open();

        card.Controls.Add(titleLabel);
        card.Controls.Add(body);
        card.Controls.Add(action);
        return card;
    }

    private async void OpenChillWindow(string url)
    {
        var window = new Form
        {
            Text = "SWTOR & Chill",
            Size = new Size(520, 340),
            MinimumSize = new Size(360, 220),
            StartPosition = FormStartPosition.Manual,
            BackColor = _background,
            ForeColor = _text,
            TopMost = true,
            ShowInTaskbar = true
        };
        CenterChillWindow(window);

        var webView = new Microsoft.Web.WebView2.WinForms.WebView2
        {
            Dock = DockStyle.Fill,
            DefaultBackgroundColor = Color.Black
        };
        window.Controls.Add(webView);
        window.Show(this);

        try
        {
            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SWTOR Holotracker",
                "ChillWebView");
            var environment = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(
                userDataFolder: userDataFolder);
            await webView.EnsureCoreWebView2Async(environment);
            webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            webView.CoreWebView2.ContainsFullScreenElementChanged += (_, _) =>
            {
                window.TopMost = true;
                webView.Dock = DockStyle.Fill;
                window.Size = new Size(Math.Max(window.Width, 420), Math.Max(window.Height, 260));
            };
            webView.Source = new Uri(url);
        }
        catch (Exception ex)
        {
            window.Close();
            MessageBox.Show($"Could not open {url}\n\n{ex.Message}", "SWTOR & Chill",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void CenterChillWindow(Form window)
    {
        var bounds = FindSwtorScreen()?.WorkingArea ?? Screen.FromControl(this).WorkingArea;
        window.Location = new Point(
            bounds.Left + (bounds.Width - window.Width) / 2,
            bounds.Top + (bounds.Height - window.Height) / 2);
    }

    private Screen? FindSwtorScreen()
    {
        IntPtr swtorWindow = IntPtr.Zero;
        EnumWindows((hWnd, _) =>
        {
            if (!IsWindowVisible(hWnd))
            {
                return true;
            }

            GetWindowThreadProcessId(hWnd, out var processId);
            if (IsProcessName(processId, "swtor"))
            {
                swtorWindow = hWnd;
                return false;
            }

            return true;
        }, IntPtr.Zero);

        return swtorWindow != IntPtr.Zero ? Screen.FromHandle(swtorWindow) : null;
    }

    private static void OpenChillWindowInDefaultBrowser(string url)
    {
        try
        {
            var browser = FindDefaultBrowser();
            if (browser is not null)
            {
                var existingWindows = GetVisibleProcessWindows(browser.ProcessName);
                var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = browser.ExecutablePath,
                    Arguments = BuildBrowserArguments(browser.ProcessName, url),
                    UseShellExecute = false
                });
                MakeBrowserWindowTopMost(browser.ProcessName, existingWindows, process);
                return;
            }

            var fallbackProcess = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
            if (fallbackProcess is not null)
            {
                MakeProcessWindowTopMost(fallbackProcess);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open {url}\n\n{ex.Message}", "SWTOR & Chill",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private sealed record BrowserLaunchInfo(string ExecutablePath, string ProcessName);

    private static string BuildBrowserArguments(string processName, string url)
    {
        if (processName.Equals("msedge", StringComparison.OrdinalIgnoreCase) ||
            processName.Equals("chrome", StringComparison.OrdinalIgnoreCase) ||
            processName.Equals("brave", StringComparison.OrdinalIgnoreCase) ||
            processName.Equals("vivaldi", StringComparison.OrdinalIgnoreCase))
        {
            return $"--app=\"{url}\" --new-window --window-size=420,260 --window-position=60,60";
        }

        if (processName.Equals("firefox", StringComparison.OrdinalIgnoreCase))
        {
            return $"-new-window \"{url}\"";
        }

        return $"\"{url}\"";
    }

    private static void MakeBrowserWindowTopMost(
        string processName,
        HashSet<IntPtr> existingWindows,
        System.Diagnostics.Process? launchedProcess)
    {
        Task.Run(() =>
        {
            var emptyTicks = 0;
            for (var i = 0; i < 57600; i++)
            {
                var applied = false;
                try
                {
                    EnumWindows((hWnd, _) =>
                    {
                        if (!IsWindowVisible(hWnd) || existingWindows.Contains(hWnd))
                        {
                            return true;
                        }

                        GetWindowThreadProcessId(hWnd, out var windowProcessId);
                        if (!IsProcessName(windowProcessId, processName))
                        {
                            return true;
                        }

                        SetWindowPos(
                            hWnd,
                            HwndTopMost,
                            60,
                            60,
                            420,
                            260,
                            SwpShowWindow);
                        applied = true;
                        return true;
                    }, IntPtr.Zero);

                    if (applied)
                    {
                        emptyTicks = 0;
                    }
                    else
                    {
                        emptyTicks++;
                    }

                    if (launchedProcess is { HasExited: true } && emptyTicks > 20)
                    {
                        return;
                    }

                    if (emptyTicks > 80)
                    {
                        return;
                    }
                }
                catch
                {
                    return;
                }

                Thread.Sleep(applied ? 350 : 150);
            }
        });
    }

    private static void MakeProcessWindowTopMost(System.Diagnostics.Process process)
    {
        Task.Run(() =>
        {
            for (var i = 0; i < 57600; i++)
            {
                try
                {
                    process.Refresh();
                    if (process.HasExited)
                    {
                        return;
                    }

                    if (process.MainWindowHandle != IntPtr.Zero && IsWindow(process.MainWindowHandle))
                    {
                        SetWindowPos(
                            process.MainWindowHandle,
                            HwndTopMost,
                            60,
                            60,
                            420,
                            260,
                            SwpShowWindow);
                    }
                }
                catch
                {
                    return;
                }

                Thread.Sleep(350);
            }
        });
    }

    private static HashSet<IntPtr> GetVisibleProcessWindows(string processName)
    {
        var windows = new HashSet<IntPtr>();
        EnumWindows((hWnd, _) =>
        {
            if (!IsWindowVisible(hWnd))
            {
                return true;
            }

            GetWindowThreadProcessId(hWnd, out var windowProcessId);
            if (IsProcessName(windowProcessId, processName))
            {
                windows.Add(hWnd);
            }

            return true;
        }, IntPtr.Zero);
        return windows;
    }

    private static bool IsProcessName(uint processId, string processName)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById((int)processId);
            return process.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static BrowserLaunchInfo? FindDefaultBrowser()
    {
        var progId = Microsoft.Win32.Registry.CurrentUser
            .OpenSubKey(@"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\https\UserChoice")
            ?.GetValue("ProgId")
            ?.ToString();
        if (string.IsNullOrWhiteSpace(progId))
        {
            return null;
        }

        var command = Microsoft.Win32.Registry.ClassesRoot
            .OpenSubKey($@"{progId}\shell\open\command")
            ?.GetValue(null)
            ?.ToString();
        var executable = ExtractExecutablePath(command);
        if (string.IsNullOrWhiteSpace(executable) || !File.Exists(executable))
        {
            return null;
        }

        return new BrowserLaunchInfo(executable, Path.GetFileNameWithoutExtension(executable));
    }

    private static string? ExtractExecutablePath(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return null;
        }

        command = command.Trim();
        if (command.StartsWith('"'))
        {
            var endQuote = command.IndexOf('"', 1);
            return endQuote > 1 ? command[1..endQuote] : null;
        }

        var exeIndex = command.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        return exeIndex >= 0 ? command[..(exeIndex + 4)].Trim() : null;
    }

    private Control BuildNewsPage()
    {
        // Outer: rows = tab strip + split content
        var host = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent
        };
        host.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        host.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // Tab strip
        var tabStrip = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };

        // Split: list (left) + article viewer (right)
        var split = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent
        };
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 600));
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // Article viewer panel (right)
        _articleViewerPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = _panel,
            Padding = new Padding(20)
        };
        _articleViewerPanel.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Select a patch note or article to read it here.",
            ForeColor = _muted,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font(Font.FontFamily, 11F)
        });

        // List panels (left)
        var listArea = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };

        _patchNotesListPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Visible = true };
        _patchNotesListPanel.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Loading patch notes...",
            ForeColor = _muted,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font(Font.FontFamily, 11F)
        });
        listArea.Controls.Add(_patchNotesListPanel);

        _newsListPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Visible = false };
        _newsListPanel.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Loading news...",
            ForeColor = _muted,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font(Font.FontFamily, 11F)
        });
        listArea.Controls.Add(_newsListPanel);

        split.Controls.Add(listArea, 0, 0);
        split.Controls.Add(_articleViewerPanel, 1, 0);

        var subPages = new[] { ("Patch Notes", _patchNotesListPanel), ("News", _newsListPanel) };
        var subButtons = new List<Button>();
        for (var i = 0; i < subPages.Length; i++)
        {
            var (label, page) = subPages[i];
            var idx = i;
            var btn = new Button
            {
                Text = label,
                FlatStyle = FlatStyle.Flat,
                BackColor = i == 0 ? Color.FromArgb(43, 57, 73) : Color.FromArgb(18, 28, 40),
                ForeColor = i == 0 ? _text : Color.FromArgb(202, 215, 231),
                Font = new Font(Font.FontFamily, 9.5F, FontStyle.Bold),
                Width = 158,
                Height = 34,
                Margin = new Padding(0, 0, 2, 0)
            };
            btn.FlatAppearance.BorderColor = i == 0 ? _accentAlt : Color.FromArgb(47, 63, 82);
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(36, 50, 66);
            btn.Click += (_, _) =>
            {
                foreach (var (_, p) in subPages) p.Visible = false;
                page.Visible = true;
                for (var j = 0; j < subButtons.Count; j++)
                {
                    var sel = j == idx;
                    subButtons[j].BackColor = sel ? Color.FromArgb(43, 57, 73) : Color.FromArgb(18, 28, 40);
                    subButtons[j].ForeColor = sel ? _text : Color.FromArgb(202, 215, 231);
                    subButtons[j].FlatAppearance.BorderColor = sel ? _accentAlt : Color.FromArgb(47, 63, 82);
                }
            };
            subButtons.Add(btn);
            tabStrip.Controls.Add(btn);
        }

        host.Controls.Add(tabStrip, 0, 0);
        host.Controls.Add(split, 0, 1);
        return host;
    }

    private void RefreshPatchNotesList()
    {
        if (_patchNotesListPanel is null) return;
        _patchNotesListPanel.Controls.Clear();

        if (_newsService.PatchNotesError is not null)
        {
            _patchNotesListPanel.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = $"Could not load patch notes: {_newsService.PatchNotesError}",
                ForeColor = _muted,
                TextAlign = ContentAlignment.MiddleCenter
            });
            return;
        }

        var scroller = new DarkScrollPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ThumbColor = Color.FromArgb(60, 80, 100),
            TrackColor = Color.FromArgb(7, 13, 19)
        };
        var stack = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.Transparent,
            Padding = new Padding(24, 16, 24, 16)
        };
        scroller.Controls.Add(stack);
        void ResizePatchCards()
        {
            var w = scroller.ClientSize.Width - 8;
            stack.Width = w;
            foreach (Control c in stack.Controls) c.Width = w;
        }
        scroller.Resize += (_, _) => ResizePatchCards();

        foreach (var note in _newsService.PatchNotes)
        {
            stack.Controls.Add(BuildNewsCard(note.Title, note.Date, "", note.Url, isPatchNote: true));
        }
        ResizePatchCards();

        _patchNotesListPanel.Controls.Add(scroller);
    }

    private void RefreshNewsList()
    {
        if (_newsListPanel is null) return;
        _newsListPanel.Controls.Clear();

        if (_newsService.NewsError is not null)
        {
            _newsListPanel.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = $"Could not load news: {_newsService.NewsError}",
                ForeColor = _muted,
                TextAlign = ContentAlignment.MiddleCenter
            });
            return;
        }

        var scroller = new DarkScrollPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ThumbColor = Color.FromArgb(60, 80, 100),
            TrackColor = Color.FromArgb(7, 13, 19)
        };
        var stack = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.Transparent,
            Padding = new Padding(24, 16, 24, 16)
        };
        scroller.Controls.Add(stack);
        void ResizeNewsCards()
        {
            var w = scroller.ClientSize.Width - 8;
            stack.Width = w;
            foreach (Control c in stack.Controls) c.Width = w;
        }
        scroller.Resize += (_, _) => ResizeNewsCards();

        foreach (var article in _newsService.News)
        {
            stack.Controls.Add(BuildNewsCard(article.Title, article.Date, article.Category, article.Url, isPatchNote: false));
        }
        ResizeNewsCards();

        _newsListPanel.Controls.Add(scroller);
    }

    private Control BuildNewsCard(string title, string date, string category, string url, bool isPatchNote)
    {
        var card = new Panel
        {
            Height = 64,
            Margin = new Padding(0, 0, 0, 6),
            BackColor = _panel,
            Cursor = Cursors.Hand
        };

        var titleLabel = new Label
        {
            AutoSize = false,
            Height = 22,
            Location = new Point(14, 10),
            Text = title,
            ForeColor = _text,
            Font = new Font(Font.FontFamily, 10F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };

        var meta = category.Length > 0 ? $"{date}  ·  {category}" : date;
        var metaLabel = new Label
        {
            AutoSize = false,
            Height = 18,
            Location = new Point(14, 34),
            Text = meta,
            ForeColor = _accent,
            Font = new Font(Font.FontFamily, 8.5F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };

        card.Controls.Add(titleLabel);
        card.Controls.Add(metaLabel);

        // Resize labels to fill card width
        card.SizeChanged += (_, _) =>
        {
            titleLabel.Width = card.Width - 28;
            metaLabel.Width = card.Width - 28;
        };

        card.Click += (_, _) => OpenArticleViewer(title, url);
        titleLabel.Click += (_, _) => OpenArticleViewer(title, url);
        metaLabel.Click += (_, _) => OpenArticleViewer(title, url);

        return card;
    }

    private async void OpenArticleViewer(string title, string url)
    {
        if (_articleViewerPanel is null) return;

        _articleViewerPanel.Controls.Clear();

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            BackColor = _panel
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _articleViewerPanel.Controls.Add(layout);

        layout.Controls.Add(new Label
        {
            AutoSize = true,
            Text = title,
            ForeColor = _accentAlt,
            Font = new Font(Font.FontFamily, 14F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 12)
        }, 0, 0);

        var loadingLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Loading...",
            ForeColor = _muted,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font(Font.FontFamily, 11F)
        };
        layout.Controls.Add(loadingLabel, 0, 1);

        try
        {
            var text = await _newsService.FetchArticleTextAsync(url);
            layout.Controls.Remove(loadingLabel);
            layout.Controls.Add(new RichTextBox
            {
                Dock = DockStyle.Fill,
                Text = text,
                BackColor = _panel,
                ForeColor = _text,
                Font = new Font("Segoe UI", 10F),
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                WordWrap = true
            }, 0, 1);
        }
        catch (Exception ex)
        {
            loadingLabel.Text = $"Failed to load: {ex.Message}";
        }
    }

    private Control BuildServerStatusPage()
    {
        var bgPath = Path.Combine(AppContext.BaseDirectory, "data", "images", "Backgrounds", "Background4.png");
        Image? bgImage = TryLoadImage(bgPath);

        var host = new BackgroundPanel(bgImage, Color.FromArgb(150, 10, 16, 26))
        {
            Dock = DockStyle.Fill,
            BackColor = _background
        };

        _serverLastCheckedLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 36,
            Text = "Checking server status...",
            ForeColor = _muted,
            Font = new Font(Font.FontFamily, 9F, FontStyle.Regular),
            Padding = new Padding(12, 10, 0, 0),
            BackColor = Color.Transparent
        };
        host.Controls.Add(_serverLastCheckedLabel);

        // Build the static card skeleton using known servers
        var knownServers = new[]
        {
            new ServerInfo { Name = "Star Forge",     Region = "North America" },
            new ServerInfo { Name = "Satele Shan",    Region = "North America" },
            new ServerInfo { Name = "Tulak Hord",     Region = "Europe" },
            new ServerInfo { Name = "Darth Malgus",   Region = "Europe" },
            new ServerInfo { Name = "The Leviathan",  Region = "Europe" },
            new ServerInfo { Name = "Shae Vizla",     Region = "Asia Pacific" }
        };

        var centerWrapper = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 3,
            RowCount = 3
        };
        centerWrapper.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        centerWrapper.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        centerWrapper.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        centerWrapper.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        centerWrapper.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        centerWrapper.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        host.Controls.Add(centerWrapper);

        var stack = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.Transparent,
            Anchor = AnchorStyles.None
        };

        centerWrapper.Controls.Add(stack, 1, 1);

        _serverCards.Clear();
        string? lastRegion = null;
        foreach (var server in knownServers)
        {
            if (server.Region != lastRegion)
            {
                stack.Controls.Add(new Label
                {
                    AutoSize = true,
                    ForeColor = _accentAlt,
                    Text = server.Region,
                    Font = new Font(Font.FontFamily, 11F, FontStyle.Bold),
                    Margin = new Padding(0, lastRegion is null ? 0 : 12, 0, 8),
                    BackColor = Color.Transparent
                });
                lastRegion = server.Region;
            }

            stack.Controls.Add(BuildServerCard(server));
        }

        return host;
    }

    private void RefreshServerStatusPanel()
    {
        if (_serverLastCheckedLabel is null) return;

        _serverLastCheckedLabel.Text = _serverStatus.Error is not null && _serverStatus.Servers.Count == 0
            ? $"Could not reach swtor.com — {_serverStatus.Error}"
            : _serverStatus.LastChecked == DateTime.MinValue
                ? "Checking server status..."
                : $"Live server status  ·  Last checked: {_serverStatus.LastChecked:HH:mm:ss} UTC  ·  Refreshes every 60s";

        // Update each card's status in place — no rebuild, no flicker
        foreach (var (info, dot, statusText, _) in _serverCards)
        {
            var live = _serverStatus.Servers.FirstOrDefault(s =>
                s.Name.Equals(info.Name, StringComparison.OrdinalIgnoreCase));
            if (live is null) continue;

            var color = live.Online ? Color.FromArgb(72, 199, 116) : Color.FromArgb(255, 90, 90);
            dot.BackColor = color;
            statusText.ForeColor = color;
            statusText.Text = live.Online ? "ONLINE" : "OFFLINE";
        }
    }

    private Control BuildServerCard(ServerInfo server)
    {
        var mutedColor = Color.FromArgb(100, 180, 180, 180);

        var card = new Panel
        {
            Width = 480,
            Height = 72,
            Margin = new Padding(0, 0, 0, 10),
            BackColor = Color.FromArgb(180, 10, 16, 26)
        };

        var center = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(0, 10, 0, 0),
            BackColor = Color.Transparent
        };

        var nameLabel = new Label
        {
            AutoSize = true,
            Text = server.Name,
            ForeColor = _text,
            Font = new Font(Font.FontFamily, 13F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 4),
            BackColor = Color.Transparent
        };

        var statusRow = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent
        };

        var dot = new Label
        {
            AutoSize = false,
            Width = 12,
            Height = 12,
            Margin = new Padding(0, 3, 6, 0),
            BackColor = mutedColor
        };
        var statusText = new Label
        {
            AutoSize = true,
            Text = "...",
            ForeColor = mutedColor,
            Font = new Font(Font.FontFamily, 9.5F, FontStyle.Bold),
            BackColor = Color.Transparent
        };

        statusRow.Controls.Add(dot);
        statusRow.Controls.Add(statusText);
        center.Controls.Add(nameLabel);
        center.Controls.Add(statusRow);
        card.Controls.Add(center);

        center.ClientSizeChanged += (_, _) =>
        {
            foreach (Control c in center.Controls)
                c.Left = (center.ClientSize.Width - c.Width) / 2;
        };

        _serverCards.Add((server, dot, statusText, nameLabel));
        return card;
    }

    private Control BuildRedeemCodePage()
    {
        var scroller = new DarkScrollPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(7, 13, 19),
            ThumbColor = Color.FromArgb(60, 80, 100),
            TrackColor = Color.FromArgb(7, 13, 19)
        };

        var stack = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(4)
        };
        scroller.Controls.Add(stack);
        scroller.Resize += (_, _) => ResizeCards(scroller);

        stack.Controls.Add(new Label
        {
            AutoSize = true,
            MaximumSize = new Size(1060, 0),
            Margin = new Padding(0, 0, 0, 12),
            ForeColor = _muted,
            Text = $"{_redeemCodes.Description} Verified: {_redeemCodes.LastVerified}"
        });

        foreach (var code in _redeemCodes.Codes)
        {
            stack.Controls.Add(BuildRedeemCodeCard(code));
        }

        return scroller;
    }

    private Control BuildCrewSkillPage()
    {
        var scroller = new DarkScrollPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 22, 24, 24),
            BackColor = Color.Transparent
        };

        // Center the entire content stack horizontally inside the scroller.
        var center = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 1,
            BackColor = Color.Transparent
        };
        center.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        center.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        scroller.Controls.Add(center);

        var stack = new FlowLayoutPanel
        {
            Anchor = AnchorStyles.None,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.Transparent
        };
        center.Controls.Add(stack, 0, 0);

        stack.Controls.Add(BuildCrewMissionHeader());
        stack.Controls.Add(BuildCrewMissionControlBar());

        var listPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0, 18, 0, 0),
            BackColor = Color.Transparent
        };
        _crewMissionListPanel = listPanel;
        stack.Controls.Add(listPanel);

        _crewMissionEmptyLabel = new Label
        {
            AutoSize = true,
            ForeColor = _muted,
            Font = new Font(Font.FontFamily, 10.5F, FontStyle.Italic),
            Text = "No active crew missions. Open the Crew Skills panel in SWTOR and send a companion — the timer will appear here automatically.",
            MaximumSize = new Size(820, 0),
            Margin = new Padding(0, 8, 0, 0)
        };
        listPanel.Controls.Add(_crewMissionEmptyLabel);

        if (_crewMissionTickTimer is null)
        {
            _crewMissionTickTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _crewMissionTickTimer.Tick += (_, _) => TickCrewMissionList();
            _crewMissionTickTimer.Start();
        }

        UpdateCrewMissionStatePill();
        UpdateCrewMissionCountLabel();
        return scroller;
    }

    private Control BuildCrewMissionHeader()
    {
        var header = new Panel
        {
            AutoSize = true,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 10)
        };

        var title = new Label
        {
            AutoSize = true,
            ForeColor = _accentAlt,
            Text = "CREW SKILL MISSIONS",
            Font = new Font(Font.FontFamily, 19F, FontStyle.Bold),
            Location = new Point(0, 0),
            BackColor = Color.Transparent
        };

        var subtitle = new Label
        {
            AutoSize = true,
            ForeColor = _muted,
            Text = "Live tracker for companion dispatches. Send a mission in SWTOR — Holotracker reads the panel and starts a timer automatically.",
            Font = new Font(Font.FontFamily, 9.75F, FontStyle.Regular),
            MaximumSize = new Size(820, 0),
            Location = new Point(0, 38),
            BackColor = Color.Transparent
        };

        var warning = new Label
        {
            AutoSize = true,
            ForeColor = Color.FromArgb(220, 168, 90),
            Text = "⚠  SWTOR doesn't expose mission data directly — Holotracker reads the panel via OCR. Captures usually succeed, but a few may slip through if the UI animates, scrolls, or you click very fast. If a timer is missing or wrong, just remove it with the ✕ and re-send.",
            Font = new Font(Font.FontFamily, 9F, FontStyle.Italic),
            MaximumSize = new Size(820, 0),
            Location = new Point(0, 80),
            BackColor = Color.Transparent
        };

        header.Controls.Add(title);
        header.Controls.Add(subtitle);
        header.Controls.Add(warning);
        header.Height = 140;
        header.Width = 820;
        return header;
    }

    private Control BuildCrewMissionControlBar()
    {
        var bar = new Panel
        {
            Width = 820,
            Height = 82,
            BackColor = _panel,
            Margin = new Padding(0, 0, 0, 6)
        };

        var statePill = new Label
        {
            AutoSize = true,
            Font = new Font(Font.FontFamily, 9.5F, FontStyle.Bold),
            Padding = new Padding(10, 5, 10, 5),
            Location = new Point(16, 18),
            BackColor = Color.FromArgb(28, 38, 52),
            ForeColor = _muted,
            Text = "○  PAUSED"
        };
        _crewMissionStatePill = statePill;

        var count = new Label
        {
            AutoSize = true,
            Font = new Font(Font.FontFamily, 10F, FontStyle.Regular),
            ForeColor = _muted,
            Location = new Point(160, 22),
            BackColor = Color.Transparent,
            Text = "No active missions"
        };
        _crewMissionCountLabel = count;

        var toggle = new Button
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Text = "Enable Skill Tracking",
            BackColor = Color.FromArgb(32, 41, 56),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Padding = new Padding(16, 8, 16, 8),
            Font = new Font(Font.FontFamily, 9.75F, FontStyle.Bold),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(620, 14)
        };
        toggle.FlatAppearance.BorderColor = _accentAlt;
        toggle.FlatAppearance.BorderSize = 1;
        toggle.Click += (_, _) => ToggleCrewMissionWatcher();
        _crewMissionToggle = toggle;

        var status = new Label
        {
            AutoSize = false,
            Width = 820 - 32,
            Height = 34,
            ForeColor = _muted,
            Font = new Font(Font.FontFamily, 8.5F, FontStyle.Italic),
            Location = new Point(16, 44),
            BackColor = Color.Transparent,
            Text = "Idle. Crew timers require Tesseract OCR and Holotracker must run at the same permission level as SWTOR."
        };
        _crewMissionStatus = status;

        bar.Controls.Add(statePill);
        bar.Controls.Add(count);
        bar.Controls.Add(toggle);
        bar.Controls.Add(status);
        return bar;
    }

    private void RebuildCrewMissionList()
    {
        if (_crewMissionListPanel is null)
        {
            return;
        }

        var snapshot = _crewMissionStore.Snapshot();
        var liveKeys = new HashSet<string>(snapshot.Select(timer => timer.Companion), StringComparer.OrdinalIgnoreCase);

        foreach (var stale in _crewMissionRows.Keys.Where(key => !liveKeys.Contains(key)).ToList())
        {
            var controls = _crewMissionRows[stale];
            _crewMissionListPanel.Controls.Remove(controls.Row);
            controls.Row.Dispose();
            _crewMissionRows.Remove(stale);
            _crewMissionNotified.Remove(stale);
        }

        for (var i = 0; i < snapshot.Count; i++)
        {
            var timer = snapshot[i];
            if (!_crewMissionRows.TryGetValue(timer.Companion, out var controls))
            {
                controls = BuildCrewMissionRow(timer);
                _crewMissionRows[timer.Companion] = controls;
                _crewMissionListPanel.Controls.Add(controls.Row);
            }

            _crewMissionListPanel.Controls.SetChildIndex(controls.Row, i);
            controls.Companion.Text = timer.Companion;
            controls.Mission.Text = timer.MissionName;
            controls.Details.Text = BuildDetailsText(timer);
            controls.Row.Tag = timer;
            UpdateRowCountdown(controls, timer);
            controls.Row.Invalidate();
        }

        if (_crewMissionEmptyLabel is not null)
        {
            _crewMissionEmptyLabel.Visible = snapshot.Count == 0;
            if (snapshot.Count == 0 && !_crewMissionListPanel.Controls.Contains(_crewMissionEmptyLabel))
            {
                _crewMissionListPanel.Controls.Add(_crewMissionEmptyLabel);
            }
        }

        UpdateCrewMissionCountLabel();
    }

    private CrewMissionRowControls BuildCrewMissionRow(CrewMissionTimer timer)
    {
        const int cardWidth = 820;
        const int cardHeight = 116;
        const int stripeWidth = 6;
        const int contentLeft = stripeWidth + 18;

        var row = new Panel
        {
            Width = cardWidth,
            Height = cardHeight,
            BackColor = _panel,
            Margin = new Padding(0, 0, 0, 10),
            Tag = timer
        };
        row.Paint += (_, e) => PaintCrewMissionCard(e, row);

        var companion = new Label
        {
            AutoSize = true,
            ForeColor = _accentAlt,
            Font = new Font(Font.FontFamily, 13F, FontStyle.Bold),
            Location = new Point(contentLeft, 14),
            BackColor = Color.Transparent,
            Text = timer.Companion
        };

        var mission = new Label
        {
            AutoSize = true,
            ForeColor = _text,
            Font = new Font(Font.FontFamily, 10.5F, FontStyle.Bold),
            Location = new Point(contentLeft, 40),
            BackColor = Color.Transparent,
            Text = timer.MissionName,
            MaximumSize = new Size(560, 0)
        };

        var details = new Label
        {
            AutoSize = true,
            ForeColor = _muted,
            Font = new Font(Font.FontFamily, 9F, FontStyle.Regular),
            Location = new Point(contentLeft, 64),
            BackColor = Color.Transparent,
            Text = BuildDetailsText(timer),
            MaximumSize = new Size(560, 0)
        };

        var countdown = new Label
        {
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = _accentAlt,
            Font = new Font("Consolas", 18F, FontStyle.Bold),
            Width = 160,
            Height = 32,
            Location = new Point(cardWidth - 220, 18),
            BackColor = Color.Transparent,
            Text = "--:--"
        };

        var remove = new Button
        {
            Text = "✕",
            Width = 30,
            Height = 28,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(32, 41, 56),
            ForeColor = Color.FromArgb(220, 220, 220),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(cardWidth - 50, 18),
            TabStop = false
        };
        remove.FlatAppearance.BorderColor = Color.FromArgb(60, 75, 95);
        remove.FlatAppearance.BorderSize = 1;
        var companionKey = timer.Companion;
        remove.Click += (_, _) => _crewMissionStore.Remove(companionKey);

        row.Controls.Add(companion);
        row.Controls.Add(mission);
        row.Controls.Add(details);
        row.Controls.Add(countdown);
        row.Controls.Add(remove);

        return new CrewMissionRowControls(row, companion, mission, details, countdown);
    }

    private void PaintCrewMissionCard(PaintEventArgs e, Panel row)
    {
        if (row.Tag is not CrewMissionTimer timer)
        {
            return;
        }

        var graphics = e.Graphics;
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var stripeColor = timer.IsDone ? Color.FromArgb(120, 230, 140) : _accentAlt;
        using (var stripe = new SolidBrush(stripeColor))
        {
            graphics.FillRectangle(stripe, 0, 0, 6, row.Height);
        }

        // Horizontal progress bar (subtle, kept for "fill from left" feel).
        const int barLeft = 24;
        const int barRight = 60;
        const int barHeight = 5;
        var barTop = row.Height - 20;
        var barWidth = row.Width - barLeft - barRight;

        using (var trackBrush = new SolidBrush(Color.FromArgb(28, 38, 52)))
        {
            graphics.FillRectangle(trackBrush, barLeft, barTop, barWidth, barHeight);
        }

        var fillWidth = (int)(barWidth * timer.Progress);
        if (fillWidth > 0)
        {
            using var fillBrush = new SolidBrush(stripeColor);
            graphics.FillRectangle(fillBrush, barLeft, barTop, fillWidth, barHeight);
        }

        // Animated countdown ring — sits left of the countdown text.
        const int ringSize = 60;
        const int ringX = 820 - 280;
        const int ringY = 28;
        var ringRect = new Rectangle(ringX, ringY, ringSize, ringSize);

        using (var trackPen = new Pen(Color.FromArgb(28, 38, 52), 4))
        {
            graphics.DrawArc(trackPen, ringRect, 0, 360);
        }

        var sweep = (float)(timer.Progress * 360);
        if (sweep > 0.5f)
        {
            using var progressPen = new Pen(stripeColor, 4)
            {
                StartCap = System.Drawing.Drawing2D.LineCap.Round,
                EndCap = System.Drawing.Drawing2D.LineCap.Round
            };
            graphics.DrawArc(progressPen, ringRect, -90, sweep);
        }

        var percent = timer.IsDone ? "100%" : $"{(int)Math.Floor(timer.Progress * 100)}%";
        using (var percentFont = new Font(Font.FontFamily, 9F, FontStyle.Bold))
        using (var percentBrush = new SolidBrush(timer.IsDone ? Color.FromArgb(120, 230, 140) : _muted))
        using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
        {
            graphics.DrawString(percent, percentFont, percentBrush, ringRect, format);
        }

        // Render any active completion-burst particles for this row.
        if (_crewBursts.TryGetValue(timer.Companion, out var burst))
        {
            burst.Render(graphics);
        }
    }

    private string BuildDetailsText(CrewMissionTimer timer)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(timer.Yield))
        {
            parts.Add(timer.Yield);
        }
        if (!string.IsNullOrWhiteSpace(timer.Influence))
        {
            parts.Add(timer.Influence);
        }
        return parts.Count > 0 ? string.Join("   ·   ", parts) : "Reward details unavailable.";
    }

    private void TickCrewMissionList()
    {
        if (_crewMissionRows.Count == 0)
        {
            return;
        }

        var snapshot = _crewMissionStore.Snapshot();
        foreach (var timer in snapshot)
        {
            if (_crewMissionRows.TryGetValue(timer.Companion, out var controls))
            {
                controls.Row.Tag = timer;
                UpdateRowCountdown(controls, timer);
                controls.Row.Invalidate();

                if (timer.IsDone && _crewMissionNotified.Add(timer.Companion))
                {
                    NotifyMissionComplete(timer);
                    SpawnCompletionBurst(controls.Row, timer.Companion);
                    if (_settings.AutoRemoveCompletedTimers)
                    {
                        _crewMissionStore.Remove(timer.Companion);
                    }
                }
            }
        }

        _crewMissionStore.PruneCompleted(TimeSpan.FromMinutes(15));
    }

    private void SpawnCompletionBurst(Panel row, string companion)
    {
        // Center the burst on the countdown ring's location.
        var center = new PointF(row.Width - 220, 58);
        _crewBursts[companion] = new CompletionBurst(center);

        if (_crewBurstTimer is null)
        {
            _crewBurstTimer = new System.Windows.Forms.Timer { Interval = 30 };
            _crewBurstTimer.Tick += (_, _) =>
            {
                var anyAlive = false;
                foreach (var (key, burst) in _crewBursts.ToList())
                {
                    burst.Update();
                    if (burst.IsAlive)
                    {
                        anyAlive = true;
                        if (_crewMissionRows.TryGetValue(key, out var ctrl))
                        {
                            ctrl.Row.Invalidate();
                        }
                    }
                    else
                    {
                        _crewBursts.Remove(key);
                        if (_crewMissionRows.TryGetValue(key, out var ctrl))
                        {
                            ctrl.Row.Invalidate();
                        }
                    }
                }
                if (!anyAlive)
                {
                    _crewBurstTimer?.Stop();
                }
            };
        }
        if (!_crewBurstTimer.Enabled)
        {
            _crewBurstTimer.Start();
        }
    }

    private void UpdateRowCountdown(CrewMissionRowControls controls, CrewMissionTimer timer)
    {
        var remaining = timer.Remaining;
        if (remaining <= TimeSpan.Zero)
        {
            controls.Countdown.Text = "READY";
            controls.Countdown.ForeColor = Color.FromArgb(120, 230, 140);
            controls.Row.BackColor = Color.FromArgb(18, 38, 28);
        }
        else
        {
            controls.Countdown.Text = remaining.TotalHours >= 1
                ? $"{(int)remaining.TotalHours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}"
                : $"{remaining.Minutes:D2}:{remaining.Seconds:D2}";
            controls.Countdown.ForeColor = _accentAlt;
            controls.Row.BackColor = _panel;
        }
    }

    private void UpdateCrewMissionStatePill()
    {
        if (_crewMissionStatePill is null)
        {
            return;
        }

        var watching = _crewMissionWatcher?.IsRunning ?? false;
        if (watching)
        {
            _crewMissionStatePill.Text = "●  WATCHING";
            _crewMissionStatePill.ForeColor = Color.FromArgb(120, 230, 140);
            _crewMissionStatePill.BackColor = Color.FromArgb(20, 50, 32);
        }
        else
        {
            _crewMissionStatePill.Text = "○  PAUSED";
            _crewMissionStatePill.ForeColor = _muted;
            _crewMissionStatePill.BackColor = Color.FromArgb(28, 38, 52);
        }
    }

    private void UpdateCrewMissionCountLabel()
    {
        if (_crewMissionCountLabel is null)
        {
            return;
        }

        var count = _crewMissionStore.Snapshot().Count;
        _crewMissionCountLabel.Text = count switch
        {
            0 => "No active missions",
            1 => "1 active mission",
            _ => $"{count} active missions"
        };
    }

    private void NotifyMissionComplete(CrewMissionTimer timer)
    {
        if (_settings.ShowNotificationOnComplete)
        {
            try
            {
                var icon = GetOrCreateTrayIcon();
                icon.BalloonTipTitle = "Crew Mission Ready";
                icon.BalloonTipText = $"{timer.Companion} returned from {timer.MissionName}";
                icon.BalloonTipIcon = ToolTipIcon.Info;
                icon.ShowBalloonTip(8000);
            }
            catch
            {
                // Notifications are best-effort.
            }
        }

        if (_settings.PlaySoundOnComplete)
        {
            try
            {
                System.Media.SystemSounds.Asterisk.Play();
            }
            catch
            {
                // Sound is best-effort.
            }
        }
    }

    private Control BuildDamageMeterPage()
    {
        var host = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent
        };
        host.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        host.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var tabStrip = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent
        };

        var contentArea = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent
        };

        _damageSubTabPages.Clear();
        AddDamageSubTab(tabStrip, contentArea, "Meter", BuildDamageMeterLivePage(), 0);
        AddDamageSubTab(tabStrip, contentArea, "Analytics", BuildDamageAnalyticsPage(), 1);

        host.Controls.Add(tabStrip, 0, 0);
        host.Controls.Add(contentArea, 0, 1);

        if (_damageTickTimer is null)
        {
            _damageTickTimer = new System.Windows.Forms.Timer { Interval = 500 };
            _damageTickTimer.Tick += (_, _) => RefreshDamageMeter();
            _damageTickTimer.Start();
        }

        // Auto-start tailing — combat log watching is read-only and harmless if no log exists.
        if (!_damageTailer.IsRunning)
        {
            _damageTailer.Start();
        }

        UpdateDamageStatePill();
        SelectDamageSubTab(_damageSelectedSubTab);
        RefreshDamageMeter();
        return host;
    }

    private Control BuildDamageMeterLivePage()
    {
        var (scroller, stack) = BuildDamageScrollerStack();
        stack.Controls.Add(BuildDamageMeterHeader());
        stack.Controls.Add(BuildDamageMeterControlBar());
        stack.Controls.Add(BuildDamageMeterStatsBar());
        stack.Controls.Add(BuildDamageMeterListPanel());
        return scroller;
    }

    private Control BuildDamageAnalyticsPage()
    {
        var (scroller, stack) = BuildDamageScrollerStack();
        stack.Controls.Add(BuildDamageAnalyticsSummaryPanel());
        stack.Controls.Add(BuildDamageHistoryGraphPanel());
        stack.Controls.Add(BuildTopAbilitiesAllTimePanel());
        return scroller;
    }

    private (Control Scroller, FlowLayoutPanel Stack) BuildDamageScrollerStack()
    {
        var scroller = new DarkScrollPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 22, 24, 24),
            BackColor = Color.Transparent
        };

        var center = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            BackColor = Color.Transparent
        };
        center.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        center.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        scroller.Controls.Add(center);

        var stack = new FlowLayoutPanel
        {
            Anchor = AnchorStyles.None,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.Transparent
        };
        center.Controls.Add(stack, 0, 0);
        return (scroller, stack);
    }

    private void AddDamageSubTab(FlowLayoutPanel tabStrip, Panel contentArea, string text, Control content, int index)
    {
        var page = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Visible = index == _damageSelectedSubTab
        };
        content.Dock = DockStyle.Fill;
        page.Controls.Add(content);
        contentArea.Controls.Add(page);

        var button = new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            BackColor = index == _damageSelectedSubTab ? Color.FromArgb(43, 57, 73) : Color.FromArgb(18, 28, 40),
            ForeColor = index == _damageSelectedSubTab ? _text : Color.FromArgb(202, 215, 231),
            Font = new Font(Font.FontFamily, 9.5F, FontStyle.Bold),
            Width = 158,
            Height = 34,
            Margin = new Padding(0, 0, 2, 0),
            Tag = index
        };
        button.FlatAppearance.BorderColor = index == _damageSelectedSubTab ? _accentAlt : Color.FromArgb(47, 63, 82);
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(36, 50, 66);
        button.Click += (_, _) => SelectDamageSubTab(index);
        tabStrip.Controls.Add(button);
        _damageSubTabPages.Add((button, page));
    }

    private Control BuildDamageMeterHeader()
    {
        var header = new Panel
        {
            Width = DamagePanelWidth,
            Height = 128,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 12)
        };

        header.Controls.Add(new Label
        {
            AutoSize = true,
            ForeColor = Color.FromArgb(150, 195, 224),
            Text = "COMBAT ANALYTICS",
            Font = new Font(Font.FontFamily, 8.5F, FontStyle.Bold),
            Location = new Point(2, 0),
            BackColor = Color.Transparent
        });

        header.Controls.Add(new Label
        {
            AutoSize = true,
            ForeColor = _accentAlt,
            Text = "DAMAGE METER",
            Font = new Font(Font.FontFamily, 22F, FontStyle.Bold),
            Location = new Point(0, 18),
            BackColor = Color.Transparent
        });

        header.Controls.Add(new Label
        {
            AutoSize = true,
            ForeColor = _muted,
            Text = "Live combat log meter for the active fight. Reads SWTOR's combat log and breaks down damage by player and ability — Details!-style.",
            Font = new Font(Font.FontFamily, 9.75F, FontStyle.Regular),
            MaximumSize = new Size(DamagePanelWidth, 0),
            Location = new Point(2, 58),
            BackColor = Color.Transparent
        });

        header.Controls.Add(new Label
        {
            AutoSize = true,
            ForeColor = Color.FromArgb(220, 168, 90),
            Text = "⚠  Requires combat logging enabled in SWTOR (Preferences → User Interface → Enable Combat Logging).",
            Font = new Font(Font.FontFamily, 9F, FontStyle.Italic),
            MaximumSize = new Size(DamagePanelWidth, 0),
            Location = new Point(2, 92),
            BackColor = Color.Transparent
        });

        return header;
    }

    private Control BuildDamageMeterControlBar()
    {
        var bar = new Panel
        {
            Width = DamagePanelWidth,
            Height = 104,
            BackColor = Color.FromArgb(235, 8, 20, 32),
            Margin = new Padding(0, 0, 0, 8)
        };
        bar.Paint += PaintDamageSurface;

        _damageStatePill = new Label
        {
            AutoSize = true,
            Font = new Font(Font.FontFamily, 9.5F, FontStyle.Bold),
            Padding = new Padding(12, 6, 12, 6),
            Location = new Point(20, 18),
            BackColor = Color.FromArgb(28, 38, 52),
            ForeColor = _muted,
            Text = "○  STOPPED"
        };
        bar.Controls.Add(_damageStatePill);

        _damageStatusLabel = new Label
        {
            AutoSize = false,
            Width = 350,
            Height = 18,
            ForeColor = _muted,
            Font = new Font(Font.FontFamily, 8.75F, FontStyle.Italic),
            Location = new Point(158, 24),
            BackColor = Color.Transparent,
            Text = "Idle."
        };
        bar.Controls.Add(_damageStatusLabel);

        var resetButton = new Button
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Text = "Reset",
            BackColor = Color.FromArgb(32, 41, 56),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Padding = new Padding(14, 6, 14, 6),
            Font = new Font(Font.FontFamily, 9.5F, FontStyle.Bold),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(DamagePanelWidth - 98, 16)
        };
        resetButton.FlatAppearance.BorderColor = _accentAlt;
        resetButton.Click += (_, _) =>
        {
            _damageStore.Reset();
            _damageDrilledPlayer = null;
            _damageExpandedAbilities.Clear();
            _damageSelectedHistoryIndex = -1;
        };
        bar.Controls.Add(resetButton);

        var overlayButton = new Button
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Text = "Show Overlay",
            BackColor = Color.FromArgb(28, 38, 52),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Padding = new Padding(14, 6, 14, 6),
            Font = new Font(Font.FontFamily, 9.5F, FontStyle.Bold),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(DamagePanelWidth - 220, 16)
        };
        overlayButton.FlatAppearance.BorderColor = _accentAlt;
        overlayButton.FlatAppearance.BorderSize = 1;
        overlayButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, 56, 78);
        overlayButton.Click += (_, _) => ToggleDamageOverlay();
        _damageOverlayButton = overlayButton;
        bar.Controls.Add(overlayButton);
        overlayButton.BringToFront();

        // Fight selector — Details!-style segment dropdown.
        bar.Controls.Add(new Label
        {
            AutoSize = true,
            Font = new Font(Font.FontFamily, 8.5F, FontStyle.Bold),
            ForeColor = _muted,
            Text = "FIGHT",
            Location = new Point(20, 67),
            BackColor = Color.Transparent
        });
        _damageFightSelector = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 360,
            Height = 24,
            Location = new Point(76, 64),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(28, 38, 52),
            ForeColor = Color.White,
            Font = new Font(Font.FontFamily, 9.5F, FontStyle.Regular)
        };
        _damageFightSelector.SelectedIndexChanged += (_, _) =>
        {
            if (_damageSuppressSelectorEvent) return;
            _damageSelectedHistoryIndex = _damageFightSelector.SelectedIndex - 1; // 0 = "Current Fight"
            _damageDrilledPlayer = null;
            _damageExpandedAbilities.Clear();
            RefreshDamageMeter();
        };
        bar.Controls.Add(_damageFightSelector);

        var clearHistoryButton = new Button
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Text = "Clear History",
            BackColor = Color.FromArgb(32, 41, 56),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Padding = new Padding(10, 4, 10, 4),
            Font = new Font(Font.FontFamily, 8.75F, FontStyle.Bold),
            // Adjacent to the dropdown (76 + 360 + 6) instead of right-aligned.
            Anchor = AnchorStyles.Top | AnchorStyles.Left,
            Location = new Point(76 + 360 + 6, 63)
        };
        clearHistoryButton.FlatAppearance.BorderColor = Color.FromArgb(60, 75, 95);
        clearHistoryButton.Click += (_, _) =>
        {
            _damageStore.Reset();
            _damageDrilledPlayer = null;
            _damageExpandedAbilities.Clear();
            _damageSelectedHistoryIndex = -1;
        };
        bar.Controls.Add(clearHistoryButton);

        RefreshDamageFightSelector();
        return bar;
    }

    private void RefreshDamageFightSelector()
    {
        if (_damageFightSelector is null)
        {
            return;
        }

        var history = _damageStore.History;
        var current = _damageStore.CurrentFight;

        var labels = new List<string> { "Current Fight" };
        // History order: oldest → newest. Show newest first in the dropdown.
        for (var i = history.Count - 1; i >= 0; i--)
        {
            labels.Add(BuildFightLabel(history[i], history.Count - i));
        }

        // Don't fire the selection-changed event while we rebuild items.
        _damageSuppressSelectorEvent = true;
        try
        {
            _damageFightSelector.BeginUpdate();
            _damageFightSelector.Items.Clear();
            foreach (var label in labels)
            {
                _damageFightSelector.Items.Add(label);
            }

            var idx = Math.Clamp(_damageSelectedHistoryIndex + 1, 0, _damageFightSelector.Items.Count - 1);
            if (_damageFightSelector.Items.Count > 0)
            {
                _damageFightSelector.SelectedIndex = idx;
            }
        }
        finally
        {
            _damageFightSelector.EndUpdate();
            _damageSuppressSelectorEvent = false;
        }

        _ = current; // suppress unused
    }

    private static string BuildFightLabel(FightSegment fight, int humanIndex)
    {
        var topPlayer = fight.Participants.Values
            .OrderByDescending(p => p.TotalDamage)
            .FirstOrDefault();
        var top = topPlayer is null ? "" : $" — {topPlayer.Name}";
        var dur = fight.Duration;
        var durText = dur.TotalHours >= 1
            ? $"{(int)dur.TotalHours}h {dur.Minutes:D2}m"
            : $"{dur.Minutes:D1}m {dur.Seconds:D2}s";
        return $"#{humanIndex} {fight.StartedAtUtc.ToLocalTime():HH:mm:ss} ({durText}){top}";
    }

    private Control BuildDamageMeterStatsBar()
    {
        var bar = new Panel
        {
            Width = DamagePanelWidth,
            Height = 94,
            BackColor = Color.FromArgb(235, 8, 20, 32),
            Margin = new Padding(0, 0, 0, 8)
        };
        bar.Paint += PaintDamageSurface;

        _damageBackButton = new Button
        {
            Text = "← All players",
            Width = 120,
            Height = 28,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(32, 41, 56),
            ForeColor = Color.White,
            Font = new Font(Font.FontFamily, 8.75F, FontStyle.Bold),
            Location = new Point(18, 16),
            Visible = false
        };
        _damageBackButton.FlatAppearance.BorderColor = _accentAlt;
        _damageBackButton.Click += (_, _) =>
        {
            _damageDrilledPlayer = null;
            _damageExpandedAbilities.Clear();
            RefreshDamageMeter();
        };
        bar.Controls.Add(_damageBackButton);

        _damageHeaderTotal = BuildStatColumn(bar, "TOTAL DAMAGE", "0", 26, 28);
        _damageHeaderDps = BuildStatColumn(bar, "DPS", "0", 346, 28);
        _damageHeaderDuration = BuildStatColumn(bar, "DURATION", "00:00", 646, 28);

        return bar;
    }

    private Label BuildStatColumn(Control parent, string title, string initial, int x, int y)
    {
        parent.Controls.Add(new Label
        {
            AutoSize = true,
            Text = title,
            ForeColor = _muted,
            Font = new Font(Font.FontFamily, 8.5F, FontStyle.Bold),
            Location = new Point(x, y),
            BackColor = Color.Transparent
        });

        var value = new Label
        {
            AutoSize = true,
            Text = initial,
            ForeColor = _accentAlt,
            Font = new Font("Consolas", 16F, FontStyle.Bold),
            Location = new Point(x, y + 16),
            BackColor = Color.Transparent
        };
        parent.Controls.Add(value);
        return value;
    }

    private void PaintDamageSurface(object? sender, PaintEventArgs e)
    {
        if (sender is not Panel panel)
        {
            return;
        }

        var graphics = e.Graphics;
        using var topLine = new SolidBrush(Color.FromArgb(185, _accentAlt.R, _accentAlt.G, _accentAlt.B));
        graphics.FillRectangle(topLine, 0, 0, panel.Width, 2);

        using var leftLine = new SolidBrush(Color.FromArgb(95, 62, 145, 190));
        graphics.FillRectangle(leftLine, 0, 2, 2, panel.Height - 2);

        using var border = new Pen(Color.FromArgb(95, 60, 86, 112), 1);
        graphics.DrawRectangle(border, 0, 0, panel.Width - 1, panel.Height - 1);
    }

    private Control BuildDamageMeterListPanel()
    {
        var panel = new FlowLayoutPanel
        {
            Width = DamagePanelWidth,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowOnly,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 0),
            BackColor = Color.Transparent
        };
        _damageBarsPanel = panel;
        return panel;
    }

    private void SelectDamageSubTab(int index)
    {
        _damageSelectedSubTab = Math.Clamp(index, 0, 1);

        for (var i = 0; i < _damageSubTabPages.Count; i++)
        {
            var (btn, page) = _damageSubTabPages[i];
            var selected = i == _damageSelectedSubTab;
            page.Visible = selected;
            btn.BackColor = selected ? Color.FromArgb(43, 57, 73) : Color.FromArgb(18, 28, 40);
            btn.ForeColor = selected ? _text : Color.FromArgb(202, 215, 231);
            btn.FlatAppearance.BorderColor = selected ? _accentAlt : Color.FromArgb(47, 63, 82);
        }

    }

    private Control BuildDamageHistoryGraphPanel()
    {
        var section = new FlowLayoutPanel
        {
            Width = DamagePanelWidth,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowOnly,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0, 16, 0, 0),
            BackColor = Color.Transparent
        };
        section.Controls.Add(new Label
        {
            AutoSize = true,
            ForeColor = _accentAlt,
            Font = new Font(Font.FontFamily, 11F, FontStyle.Bold),
            Text = "DPS HISTORY",
            Margin = new Padding(0, 0, 0, 4)
        });
        section.Controls.Add(new Label
        {
            AutoSize = true,
            ForeColor = _muted,
            Font = new Font(Font.FontFamily, 9F, FontStyle.Italic),
            Text = "Per-fight DPS trend over time. Most recent fights are on the right.",
            MaximumSize = new Size(DamagePanelWidth, 0),
            Margin = new Padding(0, 0, 0, 8)
        });

        var graph = new DoubleBufferedPanel
        {
            Width = DamagePanelWidth,
            Height = 220,
            BackColor = _panel,
            Margin = new Padding(0, 0, 0, 0)
        };
        graph.Paint += PaintDamageHistory;
        _damageHistoryGraph = graph;
        section.Controls.Add(graph);
        return section;
    }

    private void PaintDamageHistory(object? sender, PaintEventArgs e)
    {
        if (_damageHistoryGraph is null) return;
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        var samples = _damageStore.History
            .Select(fight => (Fight: fight, Dps: GetPersonalDps(fight)))
            .Where(sample => sample.Dps > 0)
            .ToList();
        if (_damageStore.CurrentFight is { } active && GetPersonalDamage(active) > 0)
        {
            samples.Add((active, GetPersonalDps(active)));
        }

        const int padL = 58, padR = 22, padT = 18, padB = 34;
        var w = _damageHistoryGraph.Width;
        var h = _damageHistoryGraph.Height;
        var plotRect = new Rectangle(padL, padT, w - padL - padR, h - padT - padB);

        // Frame
        using (var border = new Pen(Color.FromArgb(45, 60, 80), 1))
        {
            g.DrawRectangle(border, 0, 0, w - 1, h - 1);
        }

        if (samples.Count == 0)
        {
            using var brush = new SolidBrush(_muted);
            using var font = new Font(Font.FontFamily, 9F, FontStyle.Italic);
            var text = "No personal damage recorded yet.";
            var size = TextRenderer.MeasureText(text, font);
            g.DrawString(text, font, brush, (w - size.Width) / 2, (h - size.Height) / 2);
            return;
        }

        var maxDps = Math.Max(1L, samples.Max(sample => sample.Dps));

        // Y-axis labels and grid.
        using var axisFont = new Font(Font.FontFamily, 7.5F, FontStyle.Regular);
        using var axisBrush = new SolidBrush(_muted);
        using var gridPen = new Pen(Color.FromArgb(28, 38, 52), 1);
        using var axisPen = new Pen(Color.FromArgb(72, 92, 116), 1);
        for (var t = 0; t <= 4; t++)
        {
            var y = plotRect.Top + (plotRect.Height * (4 - t) / 4);
            var value = (long)((maxDps * t) / 4.0);
            var label = FormatNumber(value);
            g.DrawString(label, axisFont, axisBrush, 8, y - 7);
            g.DrawLine(gridPen, plotRect.Left, y, plotRect.Right, y);
        }
        g.DrawLine(axisPen, plotRect.Left, plotRect.Top, plotRect.Left, plotRect.Bottom);
        g.DrawLine(axisPen, plotRect.Left, plotRect.Bottom, plotRect.Right, plotRect.Bottom);

        var points = new List<PointF>(samples.Count);
        for (var i = 0; i < samples.Count; i++)
        {
            var ratio = samples[i].Dps / (double)maxDps;
            var x = samples.Count == 1
                ? plotRect.Left + plotRect.Width / 2f
                : plotRect.Left + (plotRect.Width * i / (float)(samples.Count - 1));
            var y = plotRect.Bottom - (float)(plotRect.Height * Math.Clamp(ratio, 0, 1));
            points.Add(new PointF(x, y));
        }

        if (points.Count >= 2)
        {
            using var fillPath = new System.Drawing.Drawing2D.GraphicsPath();
            fillPath.AddLine(points[0].X, plotRect.Bottom, points[0].X, points[0].Y);
            fillPath.AddLines(points.ToArray());
            fillPath.AddLine(points[^1].X, points[^1].Y, points[^1].X, plotRect.Bottom);
            fillPath.CloseFigure();
            using var fill = new SolidBrush(Color.FromArgb(34, _accentAlt.R, _accentAlt.G, _accentAlt.B));
            g.FillPath(fill, fillPath);
        }

        using var linePen = new Pen(_accentAlt, 2.2F)
        {
            StartCap = System.Drawing.Drawing2D.LineCap.Round,
            EndCap = System.Drawing.Drawing2D.LineCap.Round,
            LineJoin = System.Drawing.Drawing2D.LineJoin.Round
        };
        if (points.Count >= 2)
        {
            g.DrawLines(linePen, points.ToArray());
        }

        for (var i = 0; i < points.Count; i++)
        {
            var point = points[i];
            using var glow = new SolidBrush(Color.FromArgb(50, _accentAlt.R, _accentAlt.G, _accentAlt.B));
            using var dot = new SolidBrush(i == points.Count - 1 ? Color.White : _accentAlt);
            g.FillEllipse(glow, point.X - 6, point.Y - 6, 12, 12);
            g.FillEllipse(dot, point.X - 3, point.Y - 3, 6, 6);
        }

        // X-axis labels — first / mid / last fight start times.
        if (samples.Count >= 1)
        {
            var first = samples[0].Fight.StartedAtUtc.ToLocalTime().ToString("HH:mm");
            var last = samples[^1].Fight.StartedAtUtc.ToLocalTime().ToString("HH:mm");
            g.DrawString(first, axisFont, axisBrush, plotRect.Left, plotRect.Bottom + 4);
            var lastSize = TextRenderer.MeasureText(last, axisFont);
            g.DrawString(last, axisFont, axisBrush, plotRect.Right - lastSize.Width, plotRect.Bottom + 4);
        }

        var latest = $"{FormatNumber(samples[^1].Dps)} dps";
        using var latestFont = new Font("Consolas", 9F, FontStyle.Bold);
        var latestSize = TextRenderer.MeasureText(latest, latestFont);
        using var latestBrush = new SolidBrush(_accentAlt);
        g.DrawString(latest, latestFont, latestBrush, plotRect.Right - latestSize.Width, plotRect.Top + 3);
    }

    private Control BuildDamageAnalyticsSummaryPanel()
    {
        var section = new FlowLayoutPanel
        {
            Width = DamagePanelWidth,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowOnly,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0, 16, 0, 0),
            BackColor = Color.Transparent
        };
        section.Controls.Add(new Label
        {
            AutoSize = true,
            ForeColor = _accentAlt,
            Font = new Font(Font.FontFamily, 11F, FontStyle.Bold),
            Text = "DAMAGE ANALYTICS",
            Margin = new Padding(0, 0, 0, 4)
        });
        section.Controls.Add(new Label
        {
            AutoSize = true,
            ForeColor = _muted,
            Font = new Font(Font.FontFamily, 9F, FontStyle.Italic),
            Text = "All-time fight totals, averages, peaks, and ability efficiency.",
            MaximumSize = new Size(DamagePanelWidth, 0),
            Margin = new Padding(0, 0, 0, 8)
        });

        var grid = new FlowLayoutPanel
        {
            Width = DamagePanelWidth,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowOnly,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        _damageAnalyticsSummaryPanel = grid;
        section.Controls.Add(grid);
        return section;
    }

    private void RefreshDamageAnalyticsSummary()
    {
        if (_damageAnalyticsSummaryPanel is null) return;

        var fights = GetAllDamageFights().Where(f => GetPersonalDamage(f) > 0).ToList();
        var totalDamage = fights.Sum(GetPersonalDamage);
        var totalSeconds = fights.Sum(f => Math.Max(1, f.Duration.TotalSeconds));
        var avgDps = totalSeconds > 0 ? (long)(totalDamage / totalSeconds) : 0;
        var bestFight = fights.OrderByDescending(GetPersonalDps).FirstOrDefault();
        var bestDps = bestFight is null ? 0 : GetPersonalDps(bestFight);
        var bestTime = bestFight is null ? "-" : bestFight.StartedAtUtc.ToLocalTime().ToString("HH:mm:ss");

        var abilityStats = fights
            .SelectMany(GetPersonalParticipants)
            .SelectMany(p => p.DamageAbilities.Values)
            .ToList();
        var totalHits = abilityStats.Sum(a => a.HitCount);
        var totalCrits = abilityStats.Sum(a => a.CritCount);
        var critRate = totalHits > 0 ? (double)totalCrits / totalHits : 0;
        var topAbility = abilityStats
            .GroupBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => new { Name = g.Key, Damage = g.Sum(a => a.TotalAmount), Hits = g.Sum(a => a.HitCount) })
            .OrderByDescending(a => a.Damage)
            .FirstOrDefault();

        foreach (Control control in _damageAnalyticsSummaryPanel.Controls)
        {
            control.Dispose();
        }
        _damageAnalyticsSummaryPanel.Controls.Clear();
        _damageAnalyticsSummaryPanel.Controls.Add(BuildDamageAnalyticsCard("Fights", fights.Count.ToString("N0"), "Recorded segments"));
        _damageAnalyticsSummaryPanel.Controls.Add(BuildDamageAnalyticsCard("Total Damage", FormatNumber(totalDamage), "Across history"));
        _damageAnalyticsSummaryPanel.Controls.Add(BuildDamageAnalyticsCard("Average DPS", FormatNumber(avgDps), "Weighted by fight duration"));
        _damageAnalyticsSummaryPanel.Controls.Add(BuildDamageAnalyticsCard("Peak DPS", FormatNumber(bestDps), bestTime));
        _damageAnalyticsSummaryPanel.Controls.Add(BuildDamageAnalyticsCard("Crit Rate", $"{critRate:P0}", $"{totalCrits:N0} / {totalHits:N0} hits"));
        _damageAnalyticsSummaryPanel.Controls.Add(BuildDamageAnalyticsCard("Top Ability", topAbility?.Name ?? "-", topAbility is null ? "No ability data" : $"{FormatNumber(topAbility.Damage)} damage"));
    }

    private Control BuildDamageAnalyticsCard(string title, string value, string detail)
    {
        var card = new Panel
        {
            Width = (DamagePanelWidth - 24) / 3,
            Height = 74,
            BackColor = Color.FromArgb(235, 8, 20, 32),
            Margin = new Padding(0, 0, 8, 8)
        };
        card.Paint += PaintDamageSurface;
        card.Controls.Add(new Label
        {
            AutoSize = true,
            Text = title.ToUpperInvariant(),
            ForeColor = _muted,
            Font = new Font(Font.FontFamily, 8F, FontStyle.Bold),
            Location = new Point(14, 12),
            BackColor = Color.Transparent
        });
        card.Controls.Add(new Label
        {
            AutoSize = false,
            Text = value,
            AutoEllipsis = true,
            ForeColor = _accentAlt,
            Font = new Font("Consolas", 13F, FontStyle.Bold),
            Location = new Point(14, 28),
            Size = new Size(card.Width - 28, 22),
            BackColor = Color.Transparent
        });
        card.Controls.Add(new Label
        {
            AutoSize = false,
            Text = detail,
            AutoEllipsis = true,
            ForeColor = _muted,
            Font = new Font(Font.FontFamily, 8.25F, FontStyle.Regular),
            Location = new Point(14, 52),
            Size = new Size(card.Width - 28, 16),
            BackColor = Color.Transparent
        });
        return card;
    }

    private Control BuildTopAbilitiesAllTimePanel()
    {
        var section = new FlowLayoutPanel
        {
            Width = DamagePanelWidth,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowOnly,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0, 18, 0, 0),
            BackColor = Color.Transparent
        };
        section.Controls.Add(new Label
        {
            AutoSize = true,
            ForeColor = _accentAlt,
            Font = new Font(Font.FontFamily, 11F, FontStyle.Bold),
            Text = "TOP 10 ABILITIES — ALL TIME",
            Margin = new Padding(0, 0, 0, 4)
        });
        section.Controls.Add(new Label
        {
            AutoSize = true,
            ForeColor = _muted,
            Font = new Font(Font.FontFamily, 9F, FontStyle.Italic),
            Text = "Aggregated across every fight in history.",
            MaximumSize = new Size(DamagePanelWidth, 0),
            Margin = new Padding(0, 0, 0, 8)
        });

        var list = new FlowLayoutPanel
        {
            Width = DamagePanelWidth,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowOnly,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.Transparent
        };
        _damageTopAbilitiesPanel = list;
        section.Controls.Add(list);
        return section;
    }

    private void RefreshDamageHistoryGraph()
    {
        _damageHistoryGraph?.Invalidate();
    }

    private IEnumerable<FightSegment> GetAllDamageFights()
    {
        foreach (var fight in _damageStore.History)
        {
            yield return fight;
        }
        if (_damageStore.CurrentFight is { } current)
        {
            yield return current;
        }
    }

    private IReadOnlyList<ParticipantStats> GetPersonalParticipants(FightSegment fight)
    {
        var localName = _damageStore.LocalPlayerName;
        if (!string.IsNullOrWhiteSpace(localName)
            && fight.Participants.TryGetValue(localName, out var local))
        {
            return [local];
        }

        var markedLocal = fight.Participants.Values
            .Where(p => p.IsLocalPlayer)
            .ToList();
        if (markedLocal.Count > 0)
        {
            return markedLocal;
        }

        // Fallback for older fights before local-player detection was learned.
        return fight.Participants.Values
            .Where(p => p.IsPlayer)
            .ToList();
    }

    private long GetPersonalDamage(FightSegment fight)
    {
        return GetPersonalParticipants(fight).Sum(p => p.TotalDamage);
    }

    private long GetPersonalDps(FightSegment fight)
    {
        var seconds = fight.Duration.TotalSeconds;
        var damage = GetPersonalDamage(fight);
        return seconds >= 1 ? (long)(damage / seconds) : damage;
    }

    private void RefreshTopAbilitiesAllTime()
    {
        if (_damageTopAbilitiesPanel is null) return;

        // Aggregate ability totals across CurrentFight + all archived fights.
        var totals = new Dictionary<string, (long Damage, int Hits)>(StringComparer.OrdinalIgnoreCase);
        foreach (var fight in GetAllDamageFights())
        {
            foreach (var participant in GetPersonalParticipants(fight))
            {
                foreach (var (name, stats) in participant.DamageAbilities)
                {
                    if (!totals.TryGetValue(name, out var existing))
                    {
                        existing = (0, 0);
                    }
                    totals[name] = (existing.Damage + stats.TotalAmount, existing.Hits + stats.HitCount);
                }
            }
        }

        var top = totals
            .OrderByDescending(kv => kv.Value.Damage)
            .Take(10)
            .ToList();

        _damageTopAbilitiesPanel.Controls.Clear();

        if (top.Count == 0)
        {
            _damageTopAbilitiesPanel.Controls.Add(new Label
            {
                AutoSize = true,
                ForeColor = _muted,
                Font = new Font(Font.FontFamily, 9F, FontStyle.Italic),
                Text = "No ability data yet — finish a fight in SWTOR.",
                Margin = new Padding(0, 4, 0, 0)
            });
            return;
        }

        var max = top[0].Value.Damage;
        for (var i = 0; i < top.Count; i++)
        {
            var (name, info) = top[i];
            var fraction = max > 0 ? (double)info.Damage / max : 0;

            var row = new Panel
            {
                Width = DamagePanelWidth,
                Height = 28,
                BackColor = _panel,
                Margin = new Padding(0, 0, 0, 3),
                Tag = fraction
            };
            row.Paint += (s, e) => PaintTopAbilityRow(e, row);

            var icon = _abilityIcons.GetIcon(name);
            if (icon is not null)
            {
                row.Controls.Add(new PictureBox
                {
                    Image = icon,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Width = 22,
                    Height = 22,
                    Location = new Point(8, 3),
                    BackColor = Color.FromArgb(20, 28, 38)
                });
            }

            row.Controls.Add(new Label
            {
                AutoSize = true,
                Font = new Font(Font.FontFamily, 9.5F, FontStyle.Bold),
                ForeColor = _text,
                Text = $"#{i + 1}  {name}",
                Location = new Point(40, 5),
                BackColor = Color.Transparent
            });
            row.Controls.Add(new Label
            {
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Consolas", 9.5F, FontStyle.Bold),
                ForeColor = _accentAlt,
                Text = FormatNumber(info.Damage),
                Width = 120,
                Height = 20,
                Location = new Point(DamagePanelWidth - 200, 4),
                BackColor = Color.Transparent
            });
            row.Controls.Add(new Label
            {
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleRight,
                Font = new Font(Font.FontFamily, 8.5F, FontStyle.Regular),
                ForeColor = _muted,
                Text = $"{info.Hits} hits",
                Width = 70,
                Height = 20,
                Location = new Point(DamagePanelWidth - 80, 4),
                BackColor = Color.Transparent
            });
            _damageTopAbilitiesPanel.Controls.Add(row);
        }
    }

    private void PaintTopAbilityRow(PaintEventArgs e, Panel row)
    {
        if (row.Tag is not double fraction) return;
        var g = e.Graphics;
        var w = (int)((row.Width - 4) * Math.Clamp(fraction, 0, 1));
        if (w > 0)
        {
            using var brush = new SolidBrush(Color.FromArgb(40, _accentAlt));
            g.FillRectangle(brush, 0, 0, w, row.Height);
        }
        using var stripe = new SolidBrush(_accentAlt);
        g.FillRectangle(stripe, 0, 0, 3, row.Height);
    }

    private void RefreshDamageMeter()
    {
        if (_damageBarsPanel is null)
        {
            return;
        }

        var fight = ResolveSelectedFight();

        if (fight is null)
        {
            UpdateStatLabels(0, 0, TimeSpan.Zero);
            EnterView(DamageMeterView.None);
            ShowEmptyMessage("Waiting for combat — start a fight in SWTOR.");
            UpdateBackButton();
            RefreshDamageHistoryGraph();
            RefreshDamageAnalyticsSummary();
            RefreshTopAbilitiesAllTime();
            return;
        }

        UpdateStatLabels(fight.TotalDamage, fight.DamagePerSecond, fight.Duration);

        if (_damageDrilledPlayer is not null
            && fight.Participants.TryGetValue(_damageDrilledPlayer, out var drilled))
        {
            EnterView(DamageMeterView.Abilities);
            UpdateAbilityRows(drilled);
        }
        else
        {
            _damageDrilledPlayer = null;
            EnterView(DamageMeterView.Players);
            UpdatePlayerRows(fight);
        }
        UpdateBackButton();

        // Refresh aux panels with current store state.
        RefreshDamageHistoryGraph();
        RefreshDamageAnalyticsSummary();
        RefreshTopAbilitiesAllTime();
    }

    private FightSegment? ResolveSelectedFight()
    {
        if (_damageSelectedHistoryIndex < 0)
        {
            return _damageStore.ActiveOrLastFight;
        }
        var history = _damageStore.History;
        if (_damageSelectedHistoryIndex >= history.Count)
        {
            // History shrunk (e.g. user pressed Reset) — revert to current.
            _damageSelectedHistoryIndex = -1;
            return _damageStore.ActiveOrLastFight;
        }
        // Newest fight first in the dropdown.
        return history[history.Count - 1 - _damageSelectedHistoryIndex];
    }

    private void EnterView(DamageMeterView view)
    {
        if (_damageActiveView == view)
        {
            return;
        }

        // Tear down rows from the previous view; click handlers and panels
        // can't be reused across player ↔ ability layouts.
        ClearAllDamageRows();
        _damageActiveView = view;
    }

    private void UpdateStatLabels(long totalDamage, long dps, TimeSpan duration)
    {
        if (_damageHeaderTotal is not null)
        {
            _damageHeaderTotal.Text = FormatNumber(totalDamage);
        }
        if (_damageHeaderDps is not null)
        {
            _damageHeaderDps.Text = FormatNumber(dps);
        }
        if (_damageHeaderDuration is not null)
        {
            _damageHeaderDuration.Text = duration.TotalHours >= 1
                ? $"{(int)duration.TotalHours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}"
                : $"{duration.Minutes:D2}:{duration.Seconds:D2}";
        }
    }

    private void UpdatePlayerRows(FightSegment fight)
    {
        if (_damageBarsPanel is null)
        {
            return;
        }

        HideEmptyMessage();

        var ordered = fight.Participants.Values
            .OrderByDescending(p => p.TotalDamage)
            .Where(p => p.TotalDamage > 0)
            .ToList();

        if (ordered.Count == 0)
        {
            ClearAllDamageRows();
            ShowEmptyMessage("No damage recorded yet for this fight.");
            return;
        }

        var liveKeys = new HashSet<string>(ordered.Select(p => p.Name), StringComparer.OrdinalIgnoreCase);
        foreach (var stale in _damagePlayerRows.Keys.Where(k => !liveKeys.Contains(k)).ToList())
        {
            var ctrl = _damagePlayerRows[stale];
            _damageBarsPanel.Controls.Remove(ctrl.Row);
            ctrl.Row.Dispose();
            _damagePlayerRows.Remove(stale);
        }

        var max = ordered[0].TotalDamage;
        for (var i = 0; i < ordered.Count; i++)
        {
            var player = ordered[i];
            if (!_damagePlayerRows.TryGetValue(player.Name, out var ctrl))
            {
                ctrl = BuildPlayerRow(player.Name);
                _damagePlayerRows[player.Name] = ctrl;
                _damageBarsPanel.Controls.Add(ctrl.Row);
            }
            _damageBarsPanel.Controls.SetChildIndex(ctrl.Row, i);

            var fraction = max > 0 ? (double)player.TotalDamage / max : 0;
            var share = fight.TotalDamage > 0 ? (double)player.TotalDamage / fight.TotalDamage : 0;
            var dps = fight.Duration.TotalSeconds > 0
                ? (long)(player.TotalDamage / fight.Duration.TotalSeconds)
                : 0;
            var hits = player.DamageAbilities.Sum(a => a.Value.HitCount);

            ctrl.Title.Text = player.Name;
            ctrl.Detail.Text = dps > 0
                ? $"{hits} hits · {FormatNumber(dps)} dps"
                : $"{hits} hits";
            ctrl.Value.Text = FormatNumber(player.TotalDamage);
            ctrl.Share.Text = $"{share:P1}";
            ctrl.Row.Tag = fraction;
            ctrl.Row.AccessibleDescription = ColorTranslator.ToHtml(DamageSeriesColors[i % DamageSeriesColors.Length]);
            ctrl.Row.Invalidate();
        }
    }

    private void UpdateAbilityRows(ParticipantStats player)
    {
        if (_damageBarsPanel is null)
        {
            return;
        }

        HideEmptyMessage();

        var ordered = player.DamageAbilities.Values
            .OrderByDescending(a => a.TotalAmount)
            .Where(a => a.TotalAmount > 0)
            .ToList();

        if (ordered.Count == 0)
        {
            ClearAllDamageRows();
            ShowEmptyMessage($"No abilities recorded for {player.Name}.");
            return;
        }

        var liveKeys = new HashSet<string>(ordered.Select(a => a.Name), StringComparer.OrdinalIgnoreCase);
        foreach (var stale in _damageAbilityRows.Keys.Where(k => !liveKeys.Contains(k)).ToList())
        {
            var ctrl = _damageAbilityRows[stale];
            _damageBarsPanel.Controls.Remove(ctrl.Row);
            ctrl.Row.Dispose();
            _damageAbilityRows.Remove(stale);
        }

        var max = ordered[0].TotalAmount;
        var totalForPlayer = player.TotalDamage;
        for (var i = 0; i < ordered.Count; i++)
        {
            var ability = ordered[i];
            if (!_damageAbilityRows.TryGetValue(ability.Name, out var ctrl))
            {
                ctrl = BuildAbilityRow(ability.Name);
                _damageAbilityRows[ability.Name] = ctrl;
                _damageBarsPanel.Controls.Add(ctrl.Row);
            }
            _damageBarsPanel.Controls.SetChildIndex(ctrl.Row, i);

            var expanded = _damageExpandedAbilities.Contains(ability.Name);
            var fraction = max > 0 ? (double)ability.TotalAmount / max : 0;
            var share = totalForPlayer > 0 ? (double)ability.TotalAmount / totalForPlayer : 0;

            ctrl.Row.Height = expanded ? 116 : 54;
            ctrl.Row.BackColor = expanded ? Color.FromArgb(242, 12, 27, 42) : Color.FromArgb(232, 8, 20, 32);
            ctrl.Title.Text = (expanded ? "▾  " : "▸  ") + ability.Name;
            ctrl.Detail.Text = $"{ability.HitCount} hits";
            ctrl.Value.Text = FormatNumber(ability.TotalAmount);
            ctrl.Share.Text = $"{share:P1}";
            ctrl.Row.Tag = fraction;
            ctrl.Row.AccessibleDescription = ColorTranslator.ToHtml(DamageAbilityColor);

            // Toggle visibility of expanded detail columns instead of recreating them.
            ctrl.TimesUsedHeader.Visible = expanded;
            ctrl.MinHeader.Visible = expanded;
            ctrl.MaxHeader.Visible = expanded;
            ctrl.AvgHeader.Visible = expanded;
            ctrl.CritHeader.Visible = expanded;
            ctrl.TimesUsed.Visible = expanded;
            ctrl.Min.Visible = expanded;
            ctrl.Max.Visible = expanded;
            ctrl.Avg.Visible = expanded;
            ctrl.Crit.Visible = expanded;
            if (expanded)
            {
                ctrl.TimesUsed.Text = ability.HitCount.ToString("N0");
                ctrl.Min.Text = FormatNumber(ability.MinHit == long.MaxValue ? 0 : ability.MinHit);
                ctrl.Max.Text = FormatNumber(ability.MaxHit);
                ctrl.Avg.Text = FormatNumber(ability.AverageHit);
                ctrl.Crit.Text = $"{ability.CritRate:P0} ({ability.CritCount})";
            }

            ctrl.Row.Invalidate();
        }
    }

    private DamagePlayerRowControls BuildPlayerRow(string playerName)
    {
        var row = new Panel
        {
            Width = DamagePanelWidth,
            Height = 54,
            BackColor = Color.FromArgb(232, 8, 20, 32),
            Margin = new Padding(0, 0, 0, 6),
            Tag = 0.0,
            Cursor = Cursors.Hand
        };
        row.Paint += (_, e) => PaintDamageBarRow(e, row);

        var title = new Label
        {
            AutoSize = true,
            ForeColor = _text,
            Font = new Font(Font.FontFamily, 10.5F, FontStyle.Bold),
            Text = playerName,
            Location = new Point(18, 8),
            BackColor = Color.Transparent
        };
        var detail = new Label
        {
            AutoSize = true,
            ForeColor = _muted,
            Font = new Font(Font.FontFamily, 8.75F, FontStyle.Regular),
            Text = "",
            Location = new Point(18, 30),
            BackColor = Color.Transparent
        };
        var value = new Label
        {
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = _accentAlt,
            Font = new Font("Consolas", 12F, FontStyle.Bold),
            Width = 120,
            Height = 20,
            Location = new Point(DamagePanelWidth - 154, 9),
            BackColor = Color.Transparent
        };
        var share = new Label
        {
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = _muted,
            Font = new Font(Font.FontFamily, 8.75F, FontStyle.Regular),
            Width = 120,
            Height = 16,
            Location = new Point(DamagePanelWidth - 154, 31),
            BackColor = Color.Transparent
        };
        row.Controls.Add(title);
        row.Controls.Add(detail);
        row.Controls.Add(value);
        row.Controls.Add(share);

        var captured = playerName;
        void Drill(object? s, EventArgs e)
        {
            _damageDrilledPlayer = captured;
            RefreshDamageMeter();
        }
        row.Click += Drill;
        foreach (Control c in row.Controls)
        {
            c.Click += Drill;
        }
        return new DamagePlayerRowControls(row, title, detail, value, share);
    }

    private DamageAbilityRowControls BuildAbilityRow(string abilityName)
    {
        const int iconLeft = 14;
        const int iconSize = 28;
        const int textLeft = iconLeft + iconSize + 10;

        var row = new Panel
        {
            Width = DamagePanelWidth,
            Height = 54,
            BackColor = Color.FromArgb(232, 8, 20, 32),
            Margin = new Padding(0, 0, 0, 6),
            Tag = 0.0,
            Cursor = Cursors.Hand
        };
        row.Paint += (_, e) => PaintDamageBarRow(e, row);

        var icon = _abilityIcons.GetIcon(abilityName);
        if (icon is not null)
        {
            row.Controls.Add(new PictureBox
            {
                Image = icon,
                SizeMode = PictureBoxSizeMode.Zoom,
                Width = iconSize,
                Height = iconSize,
                Location = new Point(iconLeft, 10),
                BackColor = Color.FromArgb(20, 28, 38)
            });
        }
        else
        {
            var tile = new Panel
            {
                Width = iconSize,
                Height = iconSize,
                Location = new Point(iconLeft, 10),
                BackColor = Color.FromArgb(28, 50, 72)
            };
            var letter = string.IsNullOrEmpty(abilityName) ? "?" : abilityName[..1].ToUpperInvariant();
            tile.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = _accentAlt,
                Font = new Font(Font.FontFamily, 12F, FontStyle.Bold),
                Text = letter,
                BackColor = Color.Transparent
            });
            row.Controls.Add(tile);
        }

        var title = new Label
        {
            AutoSize = true,
            ForeColor = _text,
            Font = new Font(Font.FontFamily, 10.5F, FontStyle.Bold),
            Text = abilityName,
            Location = new Point(textLeft, 8),
            BackColor = Color.Transparent
        };
        var detail = new Label
        {
            AutoSize = true,
            ForeColor = _muted,
            Font = new Font(Font.FontFamily, 8.75F, FontStyle.Regular),
            Text = "",
            Location = new Point(textLeft, 30),
            BackColor = Color.Transparent
        };
        var value = new Label
        {
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = _accentAlt,
            Font = new Font("Consolas", 12F, FontStyle.Bold),
            Width = 120,
            Height = 20,
            Location = new Point(DamagePanelWidth - 154, 9),
            BackColor = Color.Transparent
        };
        var share = new Label
        {
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = _muted,
            Font = new Font(Font.FontFamily, 8.75F, FontStyle.Regular),
            Width = 120,
            Height = 16,
            Location = new Point(DamagePanelWidth - 154, 31),
            BackColor = Color.Transparent
        };

        // Pre-create expanded-detail columns (hidden until expanded). Building them
        // up front means toggling expanded state doesn't re-allocate controls.
        var (timesUsedLabel, timesUsedValue) = MakeAbilityDetailColumn(row, "TIMES USED", textLeft, 50);
        var (minLabel, minValue) = MakeAbilityDetailColumn(row, "MIN", textLeft + 150, 50);
        var (maxLabel, maxValue) = MakeAbilityDetailColumn(row, "MAX", textLeft + 290, 50);
        var (avgLabel, avgValue) = MakeAbilityDetailColumn(row, "AVERAGE", textLeft + 430, 50);
        var (critLabel, critValue) = MakeAbilityDetailColumn(row, "CRIT", textLeft + 580, 50);
        timesUsedLabel.Visible = timesUsedValue.Visible = false;
        minLabel.Visible = minValue.Visible = false;
        maxLabel.Visible = maxValue.Visible = false;
        avgLabel.Visible = avgValue.Visible = false;
        critLabel.Visible = critValue.Visible = false;

        row.Controls.Add(title);
        row.Controls.Add(detail);
        row.Controls.Add(value);
        row.Controls.Add(share);

        var captured = abilityName;
        void Toggle(object? s, EventArgs e)
        {
            if (!_damageExpandedAbilities.Add(captured))
            {
                _damageExpandedAbilities.Remove(captured);
            }
            RefreshDamageMeter();
        }
        row.Click += Toggle;
        foreach (Control c in row.Controls)
        {
            c.Click += Toggle;
        }

        return new DamageAbilityRowControls(
            row, title, detail, value, share,
            timesUsedLabel, minLabel, maxLabel, avgLabel, critLabel,
            timesUsedValue, minValue, maxValue, avgValue, critValue);
    }

    private (Label header, Label value) MakeAbilityDetailColumn(Control parent, string label, int x, int y)
    {
        var headerLabel = new Label
        {
            AutoSize = true,
            Text = label,
            ForeColor = _muted,
            Font = new Font(Font.FontFamily, 7.5F, FontStyle.Bold),
            Location = new Point(x, y),
            BackColor = Color.Transparent
        };
        var valueLabel = new Label
        {
            AutoSize = true,
            Text = "",
            ForeColor = _text,
            Font = new Font("Consolas", 11F, FontStyle.Bold),
            Location = new Point(x, y + 14),
            BackColor = Color.Transparent
        };
        parent.Controls.Add(headerLabel);
        parent.Controls.Add(valueLabel);
        return (headerLabel, valueLabel);
    }

    private void ClearAllDamageRows()
    {
        if (_damageBarsPanel is null)
        {
            return;
        }
        foreach (var ctrl in _damagePlayerRows.Values)
        {
            _damageBarsPanel.Controls.Remove(ctrl.Row);
            ctrl.Row.Dispose();
        }
        _damagePlayerRows.Clear();
        foreach (var ctrl in _damageAbilityRows.Values)
        {
            _damageBarsPanel.Controls.Remove(ctrl.Row);
            ctrl.Row.Dispose();
        }
        _damageAbilityRows.Clear();
    }

    private void PaintDamageBarRow(PaintEventArgs e, Panel row)
    {
        if (row.Tag is not double fraction)
        {
            return;
        }

        var graphics = e.Graphics;
        var clamped = Math.Clamp(fraction, 0, 1);
        var rowColor = _accentAlt;
        if (!string.IsNullOrWhiteSpace(row.AccessibleDescription))
        {
            try
            {
                rowColor = ColorTranslator.FromHtml(row.AccessibleDescription);
            }
            catch
            {
                rowColor = _accentAlt;
            }
        }

        using var border = new Pen(Color.FromArgb(75, 58, 90, 116), 1);
        graphics.DrawRectangle(border, 0, 0, row.Width - 1, row.Height - 1);

        using var stripe = new SolidBrush(Color.FromArgb(220, rowColor.R, rowColor.G, rowColor.B));
        graphics.FillRectangle(stripe, 0, 0, 4, row.Height);

        var bandWidth = (int)((row.Width - 12) * clamped);
        if (bandWidth > 0)
        {
            using var bandBrush = new SolidBrush(Color.FromArgb(48, rowColor.R, rowColor.G, rowColor.B));
            graphics.FillRectangle(bandBrush, 4, 2, bandWidth, row.Height - 7);
        }

        const int barLeft = 8;
        var barRight = row.Width - 8;
        var barTop = row.Height - 6;
        var barWidth = barRight - barLeft;

        using (var trackBrush = new SolidBrush(Color.FromArgb(55, 70, 86)))
        {
            graphics.FillRectangle(trackBrush, barLeft, barTop, barWidth, 3);
        }

        var fillWidth = (int)(barWidth * clamped);
        if (fillWidth > 0)
        {
            using var fillBrush = new SolidBrush(rowColor);
            graphics.FillRectangle(fillBrush, barLeft, barTop, fillWidth, 3);
        }
    }

    /* legacy block below — replaced by persistent-row rendering above. */
#if FALSE

        const int iconLeft = 14;
        const int iconSize = 28;
        const int textLeft = iconLeft + iconSize + 10;

        var icon = _abilityIcons.GetIcon(ability.Name);
        if (icon is not null)
        {
            row.Controls.Add(new PictureBox
            {
                Image = icon,
                SizeMode = PictureBoxSizeMode.Zoom,
                Width = iconSize,
                Height = iconSize,
                Location = new Point(iconLeft, 6),
                BackColor = Color.FromArgb(20, 28, 38)
            });
        }
        else
        {
            // Procedural placeholder: dim cyan tile with the ability's first letter.
            var tile = new Panel
            {
                Width = iconSize,
                Height = iconSize,
                Location = new Point(iconLeft, 6),
                BackColor = Color.FromArgb(28, 50, 72)
            };
            var letter = string.IsNullOrEmpty(ability.Name) ? "?" : ability.Name[..1].ToUpperInvariant();
            tile.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = _accentAlt,
                Font = new Font(Font.FontFamily, 12F, FontStyle.Bold),
                Text = letter,
                BackColor = Color.Transparent
            });
            row.Controls.Add(tile);
        }

        row.Controls.Add(new Label
        {
            AutoSize = true,
            ForeColor = _text,
            Font = new Font(Font.FontFamily, 10.5F, FontStyle.Bold),
            Text = (expanded ? "▾  " : "▸  ") + ability.Name,
            Location = new Point(textLeft, 5),
            BackColor = Color.Transparent
        });

        row.Controls.Add(new Label
        {
            AutoSize = true,
            ForeColor = _muted,
            Font = new Font(Font.FontFamily, 8.75F, FontStyle.Regular),
            Text = $"{ability.HitCount} hits",
            Location = new Point(textLeft, 24),
            BackColor = Color.Transparent
        });

        row.Controls.Add(new Label
        {
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = _accentAlt,
            Font = new Font("Consolas", 12F, FontStyle.Bold),
            Width = 120,
            Height = 20,
            Location = new Point(820 - 140, 6),
            BackColor = Color.Transparent,
            Text = FormatNumber(ability.TotalAmount)
        });

        row.Controls.Add(new Label
        {
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = _muted,
            Font = new Font(Font.FontFamily, 8.75F, FontStyle.Regular),
            Width = 120,
            Height = 16,
            Location = new Point(820 - 140, 24),
            BackColor = Color.Transparent,
            Text = $"{share:P1}"
        });

        if (expanded)
        {
            AddAbilityDetailColumn(row, "TIMES USED", ability.HitCount.ToString("N0"), textLeft, 50);
            AddAbilityDetailColumn(row, "MIN", FormatNumber(ability.MinHit == long.MaxValue ? 0 : ability.MinHit), textLeft + 150, 50);
            AddAbilityDetailColumn(row, "MAX", FormatNumber(ability.MaxHit), textLeft + 290, 50);
            AddAbilityDetailColumn(row, "AVERAGE", FormatNumber(ability.AverageHit), textLeft + 430, 50);
            AddAbilityDetailColumn(row, "CRIT", $"{ability.CritRate:P0} ({ability.CritCount})", textLeft + 580, 50);
        }

        return row;
    }

    private void AddAbilityDetailColumn(Control parent, string label, string value, int x, int y)
    {
        parent.Controls.Add(new Label
        {
            AutoSize = true,
            Text = label,
            ForeColor = _muted,
            Font = new Font(Font.FontFamily, 7.5F, FontStyle.Bold),
            Location = new Point(x, y),
            BackColor = Color.Transparent
        });
        parent.Controls.Add(new Label
        {
            AutoSize = true,
            Text = value,
            ForeColor = _text,
            Font = new Font("Consolas", 11F, FontStyle.Bold),
            Location = new Point(x, y + 14),
            BackColor = Color.Transparent
        });
    }

    private Panel BuildDamageBarRow(string title, long value, double fraction, double share, long rate, int hits, bool isClickable, string? detail = null)
    {
        var row = new Panel
        {
            Width = 820,
            Height = 44,
            BackColor = _panel,
            Margin = new Padding(0, 0, 0, 4),
            Tag = fraction,
            Cursor = isClickable ? Cursors.Hand : Cursors.Default
        };
        row.Paint += (_, e) => PaintDamageBarRow(e, row);

        var titleLabel = new Label
        {
            AutoSize = true,
            ForeColor = _text,
            Font = new Font(Font.FontFamily, 10.5F, FontStyle.Bold),
            Text = title,
            Location = new Point(14, 5),
            BackColor = Color.Transparent
        };
        row.Controls.Add(titleLabel);

        var detailText = detail ?? (rate > 0
            ? $"{hits} hits · {FormatNumber(rate)} dps"
            : $"{hits} hits");
        var detailLabel = new Label
        {
            AutoSize = true,
            ForeColor = _muted,
            Font = new Font(Font.FontFamily, 8.75F, FontStyle.Regular),
            Text = detailText,
            Location = new Point(14, 24),
            BackColor = Color.Transparent
        };
        row.Controls.Add(detailLabel);

        var valueLabel = new Label
        {
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = _accentAlt,
            Font = new Font("Consolas", 12F, FontStyle.Bold),
            Width = 120,
            Height = 20,
            Location = new Point(820 - 140, 6),
            BackColor = Color.Transparent,
            Text = FormatNumber(value)
        };
        row.Controls.Add(valueLabel);

        var shareLabel = new Label
        {
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = _muted,
            Font = new Font(Font.FontFamily, 8.75F, FontStyle.Regular),
            Width = 120,
            Height = 16,
            Location = new Point(820 - 140, 24),
            BackColor = Color.Transparent,
            Text = $"{share:P1}"
        };
        row.Controls.Add(shareLabel);

        return row;
    }

    private void PaintDamageBarRow(PaintEventArgs e, Panel row)
    {
        if (row.Tag is not double fraction)
        {
            return;
        }

        var graphics = e.Graphics;
        using var stripe = new SolidBrush(_accentAlt);
        graphics.FillRectangle(stripe, 0, 0, 4, row.Height);

        const int barLeft = 8;
        var barRight = row.Width - 8;
        var barTop = row.Height - 6;
        var barWidth = barRight - barLeft;

        using (var trackBrush = new SolidBrush(Color.FromArgb(28, 38, 52)))
        {
            graphics.FillRectangle(trackBrush, barLeft, barTop, barWidth, 3);
        }

        var fillWidth = (int)(barWidth * Math.Clamp(fraction, 0, 1));
        if (fillWidth > 0)
        {
            using var fillBrush = new SolidBrush(_accentAlt);
            graphics.FillRectangle(fillBrush, barLeft, barTop, fillWidth, 3);
        }
    }

#endif

    private void ShowEmptyMessage(string message)
    {
        if (_damageBarsPanel is null)
        {
            return;
        }
        if (_damageEmptyLabel is null)
        {
            _damageEmptyLabel = new Label
            {
                AutoSize = true,
                ForeColor = _muted,
                Font = new Font(Font.FontFamily, 10F, FontStyle.Italic),
                Margin = new Padding(8, 16, 0, 0),
                BackColor = Color.Transparent
            };
        }
        _damageEmptyLabel.Text = message;
        if (!_damageBarsPanel.Controls.Contains(_damageEmptyLabel))
        {
            _damageBarsPanel.Controls.Add(_damageEmptyLabel);
        }
        _damageEmptyLabel.Visible = true;
    }

    private void HideEmptyMessage()
    {
        if (_damageEmptyLabel is not null)
        {
            _damageEmptyLabel.Visible = false;
        }
    }

    private void UpdateBackButton()
    {
        if (_damageBackButton is null)
        {
            return;
        }
        _damageBackButton.Visible = _damageDrilledPlayer is not null;
    }

    private void ToggleDamageOverlay()
    {
        if (_damageOverlay is not null && !_damageOverlay.IsDisposed)
        {
            _damageOverlay.Close();
            _damageOverlay = null;
            if (_damageOverlayButton is not null)
            {
                _damageOverlayButton.Text = "Show Overlay";
            }
            return;
        }

        _damageOverlay = new DamageOverlayForm(_damageStore, _abilityIcons, _settings);
        var bounds = ResolveOverlayBounds(_damageOverlay.Width, _damageOverlay.Height);
        _damageOverlay.Location = bounds.Location;
        _damageOverlay.Size = bounds.Size;

        _damageOverlay.FormClosed += (_, _) =>
        {
            // Persist the last position/size for next time.
            if (_damageOverlay is not null)
            {
                _settings.OverlayX = _damageOverlay.Bounds.X;
                _settings.OverlayY = _damageOverlay.Bounds.Y;
                _settings.OverlayWidth = _damageOverlay.Bounds.Width;
                _settings.OverlayHeight = _damageOverlay.Bounds.Height;
                _settings.Save();
            }
            _damageOverlay = null;
            if (_damageOverlayButton is not null && !_damageOverlayButton.IsDisposed && !IsDisposed)
            {
                try { _damageOverlayButton.Text = "Show Overlay"; } catch { }
            }
        };
        _damageOverlay.Show();
        if (_damageOverlayButton is not null)
        {
            _damageOverlayButton.Text = "Hide Overlay";
        }
    }

    private Rectangle ResolveOverlayBounds(int defaultW, int defaultH)
    {
        // Prefer the saved bounds from last session.
        if (_settings.OverlayX is int sx && _settings.OverlayY is int sy
            && _settings.OverlayWidth is int sw && _settings.OverlayHeight is int sh
            && sw > 100 && sh > 80)
        {
            var savedRect = new Rectangle(sx, sy, sw, sh);
            // Make sure the saved rect is still inside one of the available monitors —
            // otherwise the overlay would open off-screen on a disconnected display.
            foreach (var screen in Screen.AllScreens)
            {
                if (screen.WorkingArea.IntersectsWith(savedRect))
                {
                    return savedRect;
                }
            }
        }

        // Fall back to SWTOR's screen if its window is open, then top-right of that screen.
        var swtorScreen = FindSwtorScreen()?.WorkingArea ?? Screen.FromControl(this).WorkingArea;
        return new Rectangle(
            swtorScreen.Right - defaultW - 24,
            swtorScreen.Top + 80,
            defaultW,
            defaultH);
    }

    private void UpdateDamageStatePill()
    {
        if (_damageStatePill is null)
        {
            return;
        }

        if (_damageTailer.IsRunning)
        {
            _damageStatePill.Text = "●  TAILING";
            _damageStatePill.ForeColor = Color.FromArgb(120, 230, 140);
            _damageStatePill.BackColor = Color.FromArgb(20, 50, 32);
        }
        else
        {
            _damageStatePill.Text = "○  STOPPED";
            _damageStatePill.ForeColor = _muted;
            _damageStatePill.BackColor = Color.FromArgb(28, 38, 52);
        }
    }

    private static string FormatNumber(long value)
    {
        if (value >= 1_000_000)
        {
            return $"{value / 1_000_000.0:F2}M";
        }
        if (value >= 10_000)
        {
            return $"{value / 1_000.0:F1}K";
        }
        return value.ToString("N0");
    }

    private Control BuildSettingsPage()
    {
        var scroller = new DarkScrollPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 22, 24, 24),
            BackColor = Color.Transparent
        };

        var center = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            BackColor = Color.Transparent
        };
        center.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        center.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        scroller.Controls.Add(center);

        var stack = new FlowLayoutPanel
        {
            Anchor = AnchorStyles.None,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.Transparent
        };
        center.Controls.Add(stack, 0, 0);

        stack.Controls.Add(new Label
        {
            AutoSize = true,
            ForeColor = _accentAlt,
            Text = "SETTINGS",
            Font = new Font(Font.FontFamily, 19F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 4),
            BackColor = Color.Transparent
        });

        stack.Controls.Add(new Label
        {
            AutoSize = true,
            ForeColor = _muted,
            Font = new Font(Font.FontFamily, 9.75F, FontStyle.Regular),
            Text = "Saved automatically to data/settings.json.",
            Margin = new Padding(0, 0, 0, 18),
            BackColor = Color.Transparent
        });

        stack.Controls.Add(BuildSettingsSection("Notifications", "Fired when a crew skill mission timer hits zero.",
        [
            BuildSettingsToggleRow(
                "Show Windows notification",
                "Pops a balloon tip from the system tray when a mission is ready to collect.",
                _settings.ShowNotificationOnComplete,
                value => { _settings.ShowNotificationOnComplete = value; _settings.Save(); }),
            BuildSettingsToggleRow(
                "Play sound",
                "Plays the system Asterisk sound on completion.",
                _settings.PlaySoundOnComplete,
                value => { _settings.PlaySoundOnComplete = value; _settings.Save(); })
        ]));

        stack.Controls.Add(BuildSettingsSection("Crew Skills", "Behavior of the skill tracking watcher.",
        [
            BuildSettingsToggleRow(
                "Auto-enable Skill Tracking on launch",
                "Starts watching for SEND COMPANION clicks as soon as Holotracker opens.",
                _settings.AutoStartSkillTracking,
                value => { _settings.AutoStartSkillTracking = value; _settings.Save(); }),
            BuildSettingsToggleRow(
                "Auto-remove completed timers",
                "Removes the mission card the moment its timer reaches zero. By default, completed cards stay visible as 'READY' so you can review them.",
                _settings.AutoRemoveCompletedTimers,
                value => { _settings.AutoRemoveCompletedTimers = value; _settings.Save(); }),
            BuildSettingsButtonRow(
                "Clear all active timers",
                "Removes every crew mission timer from the Crew Skills tab. Doesn't affect the actual missions in SWTOR.",
                "Clear Timers",
                () =>
                {
                    foreach (var key in _crewMissionStore.Snapshot().Select(t => t.Companion).ToList())
                    {
                        _crewMissionStore.Remove(key);
                    }
                })
        ]));

        stack.Controls.Add(BuildSettingsSection("Damage Meter Overlay", "Behavior of the floating damage overlay window.",
        [
            BuildSettingsToggleRow(
                "Auto-show overlay on launch",
                "Pops the floating overlay open as soon as Holotracker starts.",
                _settings.AutoShowDamageOverlay,
                value => { _settings.AutoShowDamageOverlay = value; _settings.Save(); }),
            BuildSettingsToggleRow(
                "Click-through (overlay ignores clicks)",
                "SWTOR receives mouse clicks under the overlay. Useful if the overlay covers UI you want to interact with — but you can no longer click the overlay itself, so re-open it with Show Overlay to disable.",
                _settings.OverlayClickThrough,
                value => { _settings.OverlayClickThrough = value; _settings.Save(); RecreateDamageOverlayIfOpen(); }),
            BuildSettingsToggleRow(
                "Require double-click to open player breakdown",
                "Single-click defaults to opening the per-ability breakdown immediately. Enable this if you want a single click to be reserved for selection only.",
                _settings.OverlayDoubleClickToOpen,
                value => { _settings.OverlayDoubleClickToOpen = value; _settings.Save(); RecreateDamageOverlayIfOpen(); }),
            BuildSettingsSliderRow(
                "Player rows in overlay",
                "How many top-damage players to show in the floating overlay. Default 6.",
                3, 10, _settings.OverlayPlayerRows,
                value => { _settings.OverlayPlayerRows = value; _settings.Save(); }),
            BuildSettingsSliderRow(
                "Overlay opacity",
                "Window opacity 50–100%. Lower values make the overlay more transparent over the game.",
                50, 100, _settings.OverlayOpacityPercent,
                value =>
                {
                    _settings.OverlayOpacityPercent = value;
                    _settings.Save();
                    if (_damageOverlay is not null && !_damageOverlay.IsDisposed)
                    {
                        _damageOverlay.Opacity = value / 100.0;
                    }
                }),
        ]));

        return scroller;
    }

    private void RecreateDamageOverlayIfOpen()
    {
        if (_damageOverlay is null || _damageOverlay.IsDisposed)
        {
            return;
        }
        // Click-through and double-click flags are baked into the overlay at construction
        // (CreateParams + event wiring), so we tear it down and reopen for the new value
        // to take effect. Re-uses the saved bounds via ToggleDamageOverlay.
        ToggleDamageOverlay(); // closes
        ToggleDamageOverlay(); // re-opens with fresh settings
    }

    private Control BuildSettingsSliderRow(string title, string description, int min, int max, int initial, Action<int> onChanged)
    {
        var row = new Panel
        {
            Width = 760,
            Height = 76,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 8)
        };

        var titleLabel = new Label
        {
            AutoSize = true,
            ForeColor = _text,
            Font = new Font(Font.FontFamily, 10.5F, FontStyle.Bold),
            Text = title,
            Location = new Point(0, 4),
            BackColor = Color.Transparent
        };
        var descriptionLabel = new Label
        {
            AutoSize = true,
            ForeColor = _muted,
            Font = new Font(Font.FontFamily, 9F, FontStyle.Regular),
            Text = description,
            Location = new Point(0, 26),
            MaximumSize = new Size(660, 0),
            BackColor = Color.Transparent
        };

        var trackBar = new TrackBar
        {
            Minimum = min,
            Maximum = max,
            Value = Math.Clamp(initial, min, max),
            TickFrequency = 1,
            SmallChange = 1,
            LargeChange = 1,
            Width = 360,
            Height = 28,
            Location = new Point(0, 48),
            BackColor = _panel
        };

        var valueLabel = new Label
        {
            AutoSize = false,
            Width = 70,
            Height = 22,
            Location = new Point(376, 50),
            ForeColor = _accentAlt,
            Font = new Font("Consolas", 11F, FontStyle.Bold),
            Text = trackBar.Value.ToString(),
            BackColor = Color.Transparent
        };

        trackBar.ValueChanged += (_, _) =>
        {
            valueLabel.Text = trackBar.Value.ToString();
            onChanged(trackBar.Value);
        };

        row.Controls.Add(titleLabel);
        row.Controls.Add(descriptionLabel);
        row.Controls.Add(trackBar);
        row.Controls.Add(valueLabel);
        return row;
    }

    private Control BuildSettingsSection(string title, string subtitle, List<Control> rows)
    {
        // GrowOnly preserves the explicit 820px width — GrowAndShrink would let the panel
        // collapse to its content's width, which combined with the inner FlowLayoutPanel's
        // AutoSize created a layout deadlock that shrunk the section to zero.
        var section = new FlowLayoutPanel
        {
            Width = 820,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowOnly,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = _panel,
            Padding = new Padding(20, 18, 20, 18),
            Margin = new Padding(0, 0, 0, 16)
        };

        section.Controls.Add(new Label
        {
            AutoSize = true,
            ForeColor = _accentAlt,
            Font = new Font(Font.FontFamily, 11.5F, FontStyle.Bold),
            Text = title.ToUpperInvariant(),
            Margin = new Padding(0, 0, 0, 4),
            BackColor = Color.Transparent
        });
        section.Controls.Add(new Label
        {
            AutoSize = true,
            ForeColor = _muted,
            Font = new Font(Font.FontFamily, 9F, FontStyle.Italic),
            Text = subtitle,
            Margin = new Padding(0, 0, 0, 14),
            MaximumSize = new Size(760, 0),
            BackColor = Color.Transparent
        });

        foreach (var row in rows)
        {
            section.Controls.Add(row);
        }

        return section;
    }

    private Control BuildSettingsToggleRow(string title, string description, bool initial, Action<bool> onChanged)
    {
        var row = new Panel
        {
            Width = 760,
            Height = 64,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 8)
        };

        var titleLabel = new Label
        {
            AutoSize = true,
            ForeColor = _text,
            Font = new Font(Font.FontFamily, 10.5F, FontStyle.Bold),
            Text = title,
            Location = new Point(0, 4),
            BackColor = Color.Transparent
        };

        var description2 = new Label
        {
            AutoSize = true,
            ForeColor = _muted,
            Font = new Font(Font.FontFamily, 9F, FontStyle.Regular),
            Text = description,
            Location = new Point(0, 26),
            MaximumSize = new Size(660, 0),
            BackColor = Color.Transparent
        };

        var toggle = new SwitchControl
        {
            Checked = initial,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(700, 8),
            Accent = _accentAlt,
            OffColor = Color.FromArgb(28, 38, 52)
        };
        toggle.CheckedChanged += (_, _) => onChanged(toggle.Checked);

        row.Controls.Add(titleLabel);
        row.Controls.Add(description2);
        row.Controls.Add(toggle);
        return row;
    }

    private Control BuildSettingsButtonRow(string title, string description, string buttonText, Action onClick)
    {
        var row = new Panel
        {
            Width = 760,
            Height = 64,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 8)
        };

        var titleLabel = new Label
        {
            AutoSize = true,
            ForeColor = _text,
            Font = new Font(Font.FontFamily, 10.5F, FontStyle.Bold),
            Text = title,
            Location = new Point(0, 4),
            BackColor = Color.Transparent
        };

        var descriptionLabel = new Label
        {
            AutoSize = true,
            ForeColor = _muted,
            Font = new Font(Font.FontFamily, 9F, FontStyle.Regular),
            Text = description,
            Location = new Point(0, 26),
            MaximumSize = new Size(580, 0),
            BackColor = Color.Transparent
        };

        var button = new Button
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Text = buttonText,
            BackColor = Color.FromArgb(32, 41, 56),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Padding = new Padding(14, 6, 14, 6),
            Font = new Font(Font.FontFamily, 9.5F, FontStyle.Bold),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(620, 12)
        };
        button.FlatAppearance.BorderColor = _accentAlt;
        button.Click += (_, _) => onClick();

        row.Controls.Add(titleLabel);
        row.Controls.Add(descriptionLabel);
        row.Controls.Add(button);
        return row;
    }

    private static void Safely(Action action)
    {
        try { action(); } catch { /* shutdown is best-effort */ }
    }

    private void SafelyBeginInvoke(Action action)
    {
        if (!IsHandleCreated || IsDisposed) return;
        try { BeginInvoke(action); } catch { /* handle race during shutdown */ }
    }

    private NotifyIcon GetOrCreateTrayIcon()
    {
        if (_trayIcon is not null)
        {
            return _trayIcon;
        }

        _trayIcon = new NotifyIcon
        {
            Icon = Icon ?? SystemIcons.Application,
            Visible = true,
            Text = "SWTOR Holotracker"
        };
        _trayIcon.BalloonTipClicked += (_, _) =>
        {
            if (WindowState == FormWindowState.Minimized)
            {
                WindowState = FormWindowState.Normal;
            }
            BringToFront();
            Activate();
        };
        return _trayIcon;
    }

    private void ToggleCrewMissionWatcher()
    {
        _crewMissionWatcher ??= CreateCrewMissionWatcher();
        if (_crewMissionWatcher.IsRunning)
        {
            _crewMissionWatcher.Stop();
            if (_crewMissionToggle is not null)
            {
                _crewMissionToggle.Text = "Enable Skill Tracking";
            }
            if (_crewMissionStatus is not null)
            {
                _crewMissionStatus.Text = "Paused.";
            }
        }
        else
        {
            try
            {
                _crewMissionWatcher.Start();
                _missionOcrPipeline ??= new MissionOcrPipeline();
                if (_crewMissionToggle is not null)
                {
                    _crewMissionToggle.Text = "Disable Skill Tracking";
                }
                if (_crewMissionStatus is not null)
                {
                    _crewMissionStatus.Text = _missionOcrPipeline.IsAvailable
                        ? "Watching. If SWTOR is running as admin, run Holotracker as admin too."
                        : "Tesseract OCR was not found. Install Tesseract, then restart Holotracker to enable crew timer detection.";
                }
            }
            catch (Exception ex)
            {
                if (_crewMissionStatus is not null)
                {
                    _crewMissionStatus.Text = $"Failed to install mouse hook: {ex.Message}";
                }
            }
        }

        UpdateCrewMissionStatePill();
    }

    private CrewMissionWatcher CreateCrewMissionWatcher()
    {
        var watcher = new CrewMissionWatcher();
        watcher.StatusChanged += message =>
        {
            if (_crewMissionStatus is not null)
            {
                _crewMissionStatus.Text = message;
            }
        };
        watcher.MissionSendDetected += (_, args) =>
        {
            _missionOcrPipeline ??= new MissionOcrPipeline();
            if (!_missionOcrPipeline.IsAvailable)
            {
                if (_crewMissionStatus is not null)
                {
                    _crewMissionStatus.Text = "Tesseract OCR was not found. Install Tesseract, then restart Holotracker to enable crew timer detection.";
                }
                args.Screenshot.Dispose();
                return;
            }

            var startedAtUtc = DateTime.UtcNow;
            var captureTask = _missionOcrPipeline.CaptureAsync(args);
            args.Screenshot.Dispose();

            captureTask.ContinueWith(t =>
            {
                if (t.IsFaulted || t.Result is null)
                {
                    var msg = t.IsFaulted
                        ? $"OCR error: {t.Exception?.GetBaseException().Message}"
                        : "Could not parse mission text from the click — timer not added.";
                    BeginInvoke(() =>
                    {
                        if (_crewMissionStatus is not null)
                        {
                            _crewMissionStatus.Text = msg;
                        }
                    });
                    return;
                }

                var capture = t.Result;
                UpsertCrewMissionTimer(capture, startedAtUtc);
                BeginInvoke(() =>
                {
                    if (_crewMissionStatus is not null)
                    {
                        _crewMissionStatus.Text = $"Started: {capture.Companion} → {capture.MissionName} ({FormatDuration(capture.Duration)})";
                    }
                });
            }, TaskScheduler.Default);
        };
        watcher.ActiveCrewSnapshotCaptured += (_, args) =>
        {
            _missionOcrPipeline ??= new MissionOcrPipeline();
            if (!_missionOcrPipeline.IsAvailable)
            {
                args.Screenshot.Dispose();
                return;
            }

            var capturedAtUtc = DateTime.UtcNow;
            var captureTask = _missionOcrPipeline.CaptureActiveCrewMissionsAsync(
                args.Screenshot,
                args.ScreenshotOrigin,
                args.Layout);
            args.Screenshot.Dispose();

            captureTask.ContinueWith(t =>
            {
                if (t.IsFaulted || t.Result.Count == 0)
                {
                    return;
                }

                var count = 0;
                var existingCompanions = new HashSet<string>(
                    _crewMissionStore.Snapshot().Select(timer => timer.Companion),
                    StringComparer.OrdinalIgnoreCase);
                foreach (var capture in t.Result)
                {
                    if (existingCompanions.Contains(capture.Companion))
                    {
                        continue;
                    }

                    UpsertCrewMissionTimer(capture, capturedAtUtc);
                    existingCompanions.Add(capture.Companion);
                    count++;
                }

                if (count == 0)
                {
                    return;
                }

                BeginInvoke(() =>
                {
                    if (_crewMissionStatus is not null)
                    {
                        _crewMissionStatus.Text = count == 1
                            ? "Recovered 1 active crew mission from the Crew Skills panel."
                            : $"Recovered {count} active crew missions from the Crew Skills panel.";
                    }
                });
            }, TaskScheduler.Default);
        };
        return watcher;
    }

    private void UpsertCrewMissionTimer(MissionSendCapture capture, DateTime startedAtUtc)
    {
        var timer = new CrewMissionTimer(
            capture.Companion,
            capture.MissionName,
            startedAtUtc,
            capture.Duration,
            capture.Yield,
            capture.Influence);
        _crewMissionStore.Upsert(timer);
        _crewMissionNotified.Remove(capture.Companion);
    }

    private static string FormatDuration(TimeSpan duration)
    {
        return duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours}h {duration.Minutes}m"
            : $"{duration.Minutes}m {duration.Seconds}s";
    }

    private Control BuildDatacronPage()
    {
        var host = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(7, 13, 19)
        };

        ShowDatacronRegions(host);
        return host;
    }

    private void ShowDatacronRegions(Panel host)
    {
        host.Controls.Clear();
        _cards.RemoveAll(card => card.IsDisposed || card.Parent is null);

        var scroller = new DarkScrollPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(7, 13, 19),
            ThumbColor = Color.FromArgb(60, 80, 100),
            TrackColor = Color.FromArgb(7, 13, 19)
        };
        var stack = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(4)
        };
        scroller.Controls.Add(stack);
        scroller.Resize += (_, _) => ResizeCards(scroller);

        stack.Controls.Add(new Label
        {
            AutoSize = true,
            MaximumSize = new Size(1060, 0),
            Margin = new Padding(0, 0, 0, 12),
            ForeColor = _muted,
            Text = $"{_datacrons.Description} Verified: {_datacrons.LastVerified}"
        });

        foreach (var region in _datacrons.Regions)
        {
            stack.Controls.Add(BuildDatacronRegionCard(host, region));
        }

        host.Controls.Add(scroller);
    }

    private Control BuildDatacronRegionCard(Panel host, DatacronRegionData region)
    {
        var count = region.Planets.Sum(planet => planet.Datacrons.Count);
        var card = new Panel
        {
            Width = 980,
            Height = 132,
            Margin = new Padding(0, 0, 0, 14),
            Padding = new Padding(16),
            BackColor = _panel
        };
        _cards.Add(card);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        card.Controls.Add(layout);

        var copy = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };
        copy.Controls.Add(new Label
        {
            AutoSize = true,
            ForeColor = _text,
            Text = region.Name,
            Font = new Font(Font.FontFamily, 14F, FontStyle.Bold)
        });
        copy.Controls.Add(new Label
        {
            AutoSize = true,
            ForeColor = _accent,
            Text = $"{region.Planets.Count} planets  |  {count} datacron entries",
            Font = new Font(Font.FontFamily, 9.5F, FontStyle.Bold)
        });
        copy.Controls.Add(new Label
        {
            AutoSize = true,
            MaximumSize = new Size(760, 0),
            Margin = new Padding(0, 8, 0, 0),
            ForeColor = _muted,
            Text = region.Description
        });

        var open = MakeButton("Open");
        open.Width = 104;
        open.Height = 42;
        open.Location = new Point(22, 42);
        open.Click += (_, _) => ShowDatacronRegion(host, region);
        var action = new Panel { Dock = DockStyle.Fill };
        action.Controls.Add(open);

        layout.Controls.Add(copy, 0, 0);
        layout.Controls.Add(action, 1, 0);
        return card;
    }

    private void ShowDatacronRegion(Panel host, DatacronRegionData region)
    {
        host.Controls.Clear();
        _cards.RemoveAll(card => card.IsDisposed || card.Parent is null);

        var scroller = new DarkScrollPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(7, 13, 19),
            ThumbColor = Color.FromArgb(60, 80, 100),
            TrackColor = Color.FromArgb(7, 13, 19)
        };
        var stack = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(4)
        };
        scroller.Controls.Add(stack);
        scroller.Resize += (_, _) => ResizeCards(scroller);

        var back = MakeButton("Back to regions");
        back.Margin = new Padding(0, 0, 0, 12);
        back.Click += (_, _) => ShowDatacronRegions(host);
        stack.Controls.Add(back);

        stack.Controls.Add(new Label
        {
            AutoSize = true,
            MaximumSize = new Size(1060, 0),
            Margin = new Padding(0, 0, 0, 12),
            ForeColor = _muted,
            Text = $"{region.Name}: {region.Description}"
        });

        foreach (var planet in region.Planets)
        {
            stack.Controls.Add(BuildDatacronPlanetCard(planet));
        }

        host.Controls.Add(scroller);
    }

    private Control BuildDatacronPlanetCard(DatacronPlanetData planet)
    {
        // Right column needs: 132 image + 13 gap + 38 View Map button + 32 padding = 215.
        var height = Math.Max(220, 116 + planet.Datacrons.Count * 28);
        var card = new Panel
        {
            Width = 980,
            Height = height,
            Margin = new Padding(0, 0, 0, 14),
            Padding = new Padding(16),
            BackColor = _panel
        };
        _cards.Add(card);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));
        card.Controls.Add(layout);

        var copy = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = false
        };
        copy.Controls.Add(new Label
        {
            AutoSize = true,
            ForeColor = _text,
            Text = planet.Name,
            Font = new Font(Font.FontFamily, 14F, FontStyle.Bold)
        });
        copy.Controls.Add(new Label
        {
            AutoSize = true,
            ForeColor = _accent,
            Text = $"{planet.Faction}  |  {planet.RecommendedLevel}  |  {planet.Datacrons.Count} entries",
            Font = new Font(Font.FontFamily, 9.5F, FontStyle.Bold)
        });
        copy.Controls.Add(new Label
        {
            AutoSize = true,
            MaximumSize = new Size(650, 0),
            Margin = new Padding(0, 6, 0, 6),
            ForeColor = _muted,
            Text = planet.Summary
        });

        foreach (var datacron in planet.Datacrons)
        {
            copy.Controls.Add(BuildDatacronLine(datacron));
        }

        var imagePanel = BuildDatacronImagePanel(planet);

        layout.Controls.Add(copy, 0, 0);
        layout.Controls.Add(imagePanel, 1, 0);
        return card;
    }

    private static string DatacronKey(DatacronLocationData datacron) =>
        $"datacron::{datacron.Name}::{datacron.Reward}::{datacron.Area}::{datacron.Coordinates}";

    private static string LegacyDatacronKey(DatacronLocationData datacron) =>
        $"datacron::{datacron.Name}::{datacron.Reward}";

    private void MigrateDatacronProgressKeys()
    {
        var datacrons = _datacrons.Regions
            .SelectMany(region => region.Planets)
            .SelectMany(planet => planet.Datacrons)
            .ToList();

        foreach (var group in datacrons.GroupBy(LegacyDatacronKey, StringComparer.OrdinalIgnoreCase))
        {
            if (group.Count() == 1)
            {
                var datacron = group.Single();
                _progress.RenameKey(group.Key, DatacronKey(datacron));
            }
        }
    }

    private Control BuildDatacronLine(DatacronLocationData datacron)
    {
        var text = datacron.Coordinates.Length > 0
            ? $"{datacron.Name} - {datacron.Reward} - {datacron.Area} ({datacron.Coordinates})"
            : $"{datacron.Name} - {datacron.Reward} - {datacron.Area}";

        var row = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 3)
        };

        row.Controls.Add(new Label
        {
            AutoSize = true,
            MaximumSize = new Size(600, 0),
            ForeColor = _text,
            Text = text,
            Font = new Font(Font.FontFamily, 9.5F, FontStyle.Regular)
        });

        return row;
    }

    private void ShowDatacronDetailDialog(DatacronLocationData datacron)
    {
        var dialogSize = new Size(720, 520);
        var targetScreen = Screen.FromControl(this).WorkingArea;
        using var dialog = new Form
        {
            Text = datacron.Name,
            StartPosition = FormStartPosition.Manual,
            Size = dialogSize,
            Location = new Point(
                targetScreen.Left + (targetScreen.Width - dialogSize.Width) / 2,
                targetScreen.Top + (targetScreen.Height - dialogSize.Height) / 2),
            MinimumSize = new Size(560, 420),
            BackColor = _background,
            ForeColor = _text,
            Font = Font
        };

        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            BackColor = Color.Transparent
        };
        dialog.Controls.Add(panel);

        var close = MakeButton("Close");
        close.Width = 100;
        close.Height = 36;
        close.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
        close.Location = new Point(panel.ClientSize.Width - close.Width, panel.ClientSize.Height - close.Height);
        close.Click += (_, _) => dialog.Close();
        panel.Resize += (_, _) =>
        {
            close.Location = new Point(panel.ClientSize.Width - close.Width, panel.ClientSize.Height - close.Height);
        };

        var title = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(500, 0),
            ForeColor = _accentAlt,
            Text = datacron.Name,
            Font = new Font(Font.FontFamily, 15F, FontStyle.Bold)
        };

        var reward = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(500, 0),
            Location = new Point(0, 34),
            ForeColor = _accent,
            Text = datacron.Reward,
            Font = new Font(Font.FontFamily, 10F, FontStyle.Bold)
        };

        var location = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(500, 0),
            Location = new Point(0, 64),
            ForeColor = _text,
            Text = datacron.Coordinates.Length > 0
                ? $"{datacron.Area}  |  {datacron.Coordinates}"
                : datacron.Area,
            Font = new Font(Font.FontFamily, 9.5F, FontStyle.Bold)
        };

        var notes = new TextBox
        {
            Location = new Point(0, 110),
            Size = new Size(650, 300),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            ForeColor = _muted,
            BackColor = _panel,
            BorderStyle = BorderStyle.None,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Text = datacron.Guide.Length > 0
                ? datacron.Guide
                : datacron.Notes.Length > 0 ? datacron.Notes : "No route details added yet.",
            Font = new Font(Font.FontFamily, 10F, FontStyle.Regular)
        };
        panel.Resize += (_, _) =>
        {
            notes.Size = new Size(Math.Max(260, panel.ClientSize.Width - 2), Math.Max(120, panel.ClientSize.Height - 166));
        };

        panel.Controls.Add(title);
        panel.Controls.Add(reward);
        panel.Controls.Add(location);
        panel.Controls.Add(notes);
        panel.Controls.Add(close);
        dialog.ShowDialog(this);
    }

    private Control BuildDatacronImagePanel(DatacronPlanetData planet)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 0, 0, 0)
        };

        var imageSlot = new Panel
        {
            Dock = DockStyle.Top,
            Height = 132,
            BackColor = _panelAlt,
            Padding = new Padding(8)
        };
        var fullPath = Path.Combine(AppContext.BaseDirectory, planet.ImagePath);
        if (planet.ImagePath.Length > 0 && File.Exists(fullPath))
        {
            imageSlot.Controls.Add(new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                ImageLocation = fullPath,
                BackColor = Color.Black
            });
        }
        else
        {
            imageSlot.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = _muted,
                Text = "Local map view"
            });
        }

        imageSlot.Cursor = Cursors.Hand;
        imageSlot.Click += (_, _) => ShowDatacronMapDialog(planet);
        foreach (Control child in imageSlot.Controls)
        {
            child.Cursor = Cursors.Hand;
            child.Click += (_, _) => ShowDatacronMapDialog(planet);
        }

        var mapButton = MakeButton("View Map");
        mapButton.Width = 116;
        mapButton.Height = 38;
        mapButton.Location = new Point(12, 145);
        mapButton.Click += (_, _) => ShowDatacronMapDialog(planet);

        panel.Controls.Add(mapButton);
        panel.Controls.Add(imageSlot);
        return panel;
    }

    private void ShowDatacronMapDialog(DatacronPlanetData planet)
    {
        // Position the dialog on the same monitor as the main form before maximizing,
        // otherwise CenterScreen sends it to the primary monitor regardless of where
        // Holotracker is running.
        var targetScreen = Screen.FromControl(this).WorkingArea;
        using var dialog = new Form
        {
            Text = $"{planet.Name} Datacrons",
            StartPosition = FormStartPosition.Manual,
            Location = targetScreen.Location,
            Size = targetScreen.Size,
            WindowState = FormWindowState.Maximized,
            MinimumSize = new Size(1100, 720),
            BackColor = _background,
            ForeColor = _text,
            Font = Font
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(18),
            BackColor = Color.Transparent
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
        dialog.Controls.Add(layout);

        var selectedIndex = 0;
        var map = new DoubleBufferedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(7, 13, 19),
            Margin = new Padding(0, 0, 16, 0)
        };
        map.Paint += (_, e) => DrawLocalDatacronMap(e.Graphics, map.ClientRectangle, planet);
        layout.Controls.Add(map, 0, 0);

        var stack = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(12, 6, 12, 12)
        };

        var closeButton = MakeButton("Close");
        closeButton.Width = 120;
        closeButton.Height = 38;
        closeButton.Margin = new Padding(0, 0, 0, 18);
        closeButton.Click += (_, _) => dialog.Close();
        stack.Controls.Add(closeButton);

        stack.Controls.Add(new Label
        {
            AutoSize = true,
            MaximumSize = new Size(560, 0),
            ForeColor = _text,
            Text = planet.Name,
            Font = new Font(Font.FontFamily, 20F, FontStyle.Bold)
        });
        stack.Controls.Add(new Label
        {
            AutoSize = true,
            MaximumSize = new Size(560, 0),
            Margin = new Padding(0, 4, 0, 18),
            ForeColor = _muted,
            Text = planet.Summary,
            Font = new Font(Font.FontFamily, 10F, FontStyle.Regular)
        });

        for (var i = 0; i < planet.Datacrons.Count; i++)
        {
            var index = i;
            var datacron = planet.Datacrons[i];
            var row = BuildDatacronLegendCard(i + 1, datacron);
            void SelectRow()
            {
                selectedIndex = index;
                foreach (Control control in stack.Controls)
                {
                    if (control.Tag as string == "datacron-card")
                    {
                        control.BackColor = _panel;
                    }
                }

                row.BackColor = Color.FromArgb(38, 50, 65);
            }

            row.Click += (_, _) => SelectRow();
            foreach (Control child in row.Controls)
            {
                child.Click += (_, _) => SelectRow();
            }

            if (i == 0)
            {
                row.BackColor = Color.FromArgb(38, 50, 65);
            }

            stack.Controls.Add(row);
        }

        layout.Controls.Add(stack, 1, 0);
        dialog.ShowDialog(this);
    }

    private Control BuildDatacronLegendCard(int number, DatacronLocationData datacron)
    {
        var card = new Panel
        {
            Width = 560,
            Height = 176,
            BackColor = _panel,
            Padding = new Padding(16),
            Margin = new Padding(0, 0, 0, 14),
            Cursor = Cursors.Hand,
            Tag = "datacron-card"
        };

        var badge = new Label
        {
            Width = 34,
            Height = 34,
            Location = new Point(0, 2),
            TextAlign = ContentAlignment.MiddleCenter,
            Text = number.ToString(),
            BackColor = _accentAlt,
            ForeColor = Color.Black,
            Font = new Font(Font.FontFamily, 10F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        card.Controls.Add(badge);

        var title = new Label
        {
            AutoSize = false,
            Location = new Point(52, 0),
            Size = new Size(350, 28),
            ForeColor = _accentAlt,
            Text = datacron.Name,
            Font = new Font(Font.FontFamily, 11.5F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        card.Controls.Add(title);

        var reward = new Label
        {
            AutoSize = false,
            Location = new Point(52, 30),
            Size = new Size(350, 24),
            ForeColor = _accent,
            Text = datacron.Reward,
            Font = new Font(Font.FontFamily, 9.5F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        card.Controls.Add(reward);

        var location = new Label
        {
            AutoSize = false,
            Location = new Point(52, 56),
            Size = new Size(476, 38),
            ForeColor = _text,
            Text = datacron.Coordinates.Length > 0
                ? $"{datacron.Area}  |  {datacron.Coordinates}"
                : datacron.Area,
            Font = new Font(Font.FontFamily, 9.3F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        card.Controls.Add(location);

        var notes = new Label
        {
            AutoSize = false,
            Location = new Point(52, 100),
            Size = new Size(300, 60),
            ForeColor = _muted,
            Text = datacron.Notes,
            Font = new Font(Font.FontFamily, 9.2F, FontStyle.Regular),
            Cursor = Cursors.Hand
        };
        card.Controls.Add(notes);

        var guide = MakeButton("Guide");
        guide.Width = 82;
        guide.Height = 32;
        guide.Location = new Point(424, 126);
        guide.Font = new Font(Font.FontFamily, 8.5F, FontStyle.Bold);
        guide.Click += (_, _) => ShowDatacronDetailDialog(datacron);
        card.Controls.Add(guide);

        var key = DatacronKey(datacron);
        var collectedCheck = new CheckBox
        {
            AutoSize = true,
            Location = new Point(424, 8),
            Checked = _progress.IsCompleted(key),
            Text = "Collected",
            ForeColor = _progress.IsCompleted(key) ? Color.FromArgb(72, 199, 116) : _muted,
            Font = new Font(Font.FontFamily, 8.5F, FontStyle.Bold)
        };
        collectedCheck.CheckedChanged += (_, _) =>
        {
            _progress.SetCompleted(key, collectedCheck.Checked);
            collectedCheck.ForeColor = collectedCheck.Checked ? Color.FromArgb(72, 199, 116) : _muted;
        };
        card.Controls.Add(collectedCheck);

        return card;
    }

    private sealed class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }
    }

    private sealed class BorderlessTabControl : TabControl
    {
        // FlatButtons appearance has no content-area border — nothing extra needed
    }

    private sealed class BackgroundPanel : Panel
    {
        private readonly Image? _bg;
        private readonly Color _dim;

        public BackgroundPanel(Image? bg, Color dim)
        {
            _bg = bg;
            _dim = dim;
            DoubleBuffered = true;
            ResizeRedraw = true;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (_bg is not null)
            {
                e.Graphics.DrawImage(_bg, ClientRectangle);
                using var brush = new SolidBrush(_dim);
                e.Graphics.FillRectangle(brush, ClientRectangle);
            }
            else
            {
                base.OnPaintBackground(e);
            }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x02000000; // WS_EX_COMPOSITED
                return cp;
            }
        }
    }

    private sealed class HeaderBackgroundPanel : Panel
    {
        private readonly Image? _bg;

        public HeaderBackgroundPanel(Image? bg)
        {
            _bg = bg;
            DoubleBuffered = true;
            ResizeRedraw = true;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

            if (_bg is not null)
            {
                // Draw background image cropped/scaled to fill, anchored to the right side
                var srcRatio = (float)_bg.Width / _bg.Height;
                var dstRatio = (float)Width / Height;
                Rectangle src;
                if (srcRatio > dstRatio)
                {
                    // Image is wider — crop sides, show center-right
                    var srcH = _bg.Height;
                    var srcW = (int)(_bg.Height * dstRatio);
                    var srcX = _bg.Width - srcW; // anchor right
                    src = new Rectangle(srcX, 0, srcW, srcH);
                }
                else
                {
                    var srcW = _bg.Width;
                    var srcH = (int)(_bg.Width / dstRatio);
                    src = new Rectangle(0, 0, srcW, srcH);
                }
                g.DrawImage(_bg, ClientRectangle, src, GraphicsUnit.Pixel);
            }

            // Dark gradient overlay — heavier on left for text legibility, lighter on right
            using var overlay = new System.Drawing.Drawing2D.LinearGradientBrush(
                ClientRectangle,
                Color.FromArgb(220, 6, 14, 24),
                Color.FromArgb(160, 10, 22, 36),
                System.Drawing.Drawing2D.LinearGradientMode.Horizontal);
            g.FillRectangle(overlay, ClientRectangle);

            // Subtle bottom separator line
            using var sep = new Pen(Color.FromArgb(60, 80, 120, 160), 1);
            g.DrawLine(sep, 0, Height - 1, Width, Height - 1);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x02000000; // WS_EX_COMPOSITED
                return cp;
            }
        }
    }

    private void DrawLocalDatacronMap(Graphics graphics, Rectangle bounds, DatacronPlanetData planet)
    {
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.Clear(Color.FromArgb(7, 13, 19));

        using var canvasBrush = new SolidBrush(Color.FromArgb(7, 13, 19));
        graphics.FillRectangle(canvasBrush, bounds);

        var availableRect = Rectangle.Inflate(bounds, -28, -28);
        var fullPath = Path.Combine(AppContext.BaseDirectory, planet.ImagePath);
        if (planet.ImagePath.Length > 0 && File.Exists(fullPath))
        {
            using var image = Image.FromFile(fullPath);
            var imageRect = FitRectangle(availableRect, image.Width, image.Height);
            _activeDatacronMapRect = imageRect;
            graphics.DrawImage(image, imageRect);
            using var imageBorder = new Pen(Color.FromArgb(72, 92, 118), 1);
            graphics.DrawRectangle(imageBorder, imageRect);
            return;
        }

        var mapRect = availableRect;
        _activeDatacronMapRect = mapRect;

        using var mapBrush = new SolidBrush(Color.FromArgb(13, 22, 32));
        using var gridPen = new Pen(Color.FromArgb(42, 61, 78), 1);
        using var borderPen = new Pen(Color.FromArgb(72, 92, 118), 1);
        graphics.FillRectangle(mapBrush, mapRect);
        graphics.DrawRectangle(borderPen, mapRect);

        for (var x = mapRect.Left + mapRect.Width / 4; x < mapRect.Right; x += mapRect.Width / 4)
        {
            graphics.DrawLine(gridPen, x, mapRect.Top, x, mapRect.Bottom);
        }

        for (var y = mapRect.Top + mapRect.Height / 4; y < mapRect.Bottom; y += mapRect.Height / 4)
        {
            graphics.DrawLine(gridPen, mapRect.Left, y, mapRect.Right, y);
        }

        using var titleBrush = new SolidBrush(_text);
        using var mutedBrush = new SolidBrush(_muted);
        using var pinBrush = new SolidBrush(_accentAlt);
        using var pinTextBrush = new SolidBrush(Color.Black);
        using var titleFont = new Font(Font.FontFamily, 13F, FontStyle.Bold);
        using var smallFont = new Font(Font.FontFamily, 8.5F, FontStyle.Bold);
        graphics.DrawString(planet.Name, titleFont, titleBrush, mapRect.Left, mapRect.Top - 24);
        graphics.DrawString("No local map image yet. Add imagePath in data/datacrons.json to show a planet map here.", smallFont, mutedBrush, mapRect.Left, mapRect.Bottom + 8);

    }

    private static Rectangle FitRectangle(Rectangle bounds, int sourceWidth, int sourceHeight)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0 || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return bounds;
        }

        var scale = Math.Min(bounds.Width / (double)sourceWidth, bounds.Height / (double)sourceHeight);
        var width = (int)Math.Round(sourceWidth * scale);
        var height = (int)Math.Round(sourceHeight * scale);
        return new Rectangle(
            bounds.Left + (bounds.Width - width) / 2,
            bounds.Top + (bounds.Height - height) / 2,
            width,
            height);
    }

    private Control BuildRedeemCodeCard(RedeemCodeItem code)
    {
        var card = new Panel
        {
            Width = 980,
            Height = 150,
            Margin = new Padding(0, 0, 0, 14),
            Padding = new Padding(16),
            BackColor = _panel
        };
        _cards.Add(card);

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        card.Controls.Add(content);

        var codeBlock = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };
        codeBlock.Controls.Add(new Label
        {
            AutoSize = true,
            ForeColor = _text,
            Text = code.Code,
            Font = new Font(Font.FontFamily, 15F, FontStyle.Bold)
        });
        codeBlock.Controls.Add(new Label
        {
            AutoSize = true,
            ForeColor = _accentAlt,
            Text = code.Status,
            Font = new Font(Font.FontFamily, 9.5F, FontStyle.Bold)
        });

        var notes = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = _muted,
            Text = $"{code.Reward}{Environment.NewLine}{code.Note}",
            Font = new Font(Font.FontFamily, 10F, FontStyle.Regular)
        };

        var imageSlot = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8, 0, 12, 0),
            BackColor = Color.Transparent
        };
        var imagePath = Path.Combine(AppContext.BaseDirectory, code.ImagePath);
        if (File.Exists(imagePath))
        {
            imageSlot.Controls.Add(new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                ImageLocation = imagePath,
                BackColor = Color.Black
            });
        }

        var copy = MakeButton("Copy");
        copy.Width = 88;
        copy.Height = 42;
        copy.Location = new Point(21, 38);
        copy.Click += (_, _) =>
        {
            Clipboard.SetText(code.Code);
            copy.Text = "Copied";
            var timer = new System.Windows.Forms.Timer { Interval = 1200 };
            timer.Tick += (_, _) =>
            {
                copy.Text = "Copy";
                timer.Stop();
                timer.Dispose();
            };
            timer.Start();
        };
        var copyPanel = new Panel
        {
            Dock = DockStyle.Fill
        };
        copyPanel.Controls.Add(copy);

        content.Controls.Add(codeBlock, 0, 0);
        content.Controls.Add(notes, 1, 0);
        content.Controls.Add(imageSlot, 2, 0);
        content.Controls.Add(copyPanel, 3, 0);
        return card;
    }

    private Control BuildCategoryPage(CategoryData category)
    {
        var scroller = new DarkScrollPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(7, 13, 19),
            ThumbColor = Color.FromArgb(60, 80, 100),
            TrackColor = Color.FromArgb(7, 13, 19)
        };

        var stack = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(4)
        };
        scroller.Controls.Add(stack);
        scroller.Resize += (_, _) => ResizeCards(scroller);

        stack.Controls.Add(new Label
        {
            AutoSize = true,
            MaximumSize = new Size(1060, 0),
            Margin = new Padding(0, 0, 0, 12),
            ForeColor = _muted,
            Text = category.Description
        });

        foreach (var item in FilterItems(category.Items))
        {
            stack.Controls.Add(BuildDailyCard(item));
        }

        return scroller;
    }

    private Control BuildOperationsSubTabs(CategoryData category)
    {
        var tabs = new BorderlessTabControl
        {
            Dock = DockStyle.Fill,
            DrawMode = TabDrawMode.OwnerDrawFixed,
            ItemSize = new Size(150, 30),
            SizeMode = TabSizeMode.Fixed,
            Font = new Font(Font.FontFamily, 9F, FontStyle.Bold),
            BackColor = _background
        };
        tabs.DrawItem += DrawNavigationTab;
        tabs.Paint += PaintTabControlBackground;

        AddDailySubTab(tabs, "Priority", category.Description, category.Items
            .Where(item => item.Tags.Any(tag => tag.Contains("Lair", StringComparison.OrdinalIgnoreCase)) || item.Name.Contains("R-4") || item.Name.Contains("Ravagers"))
            .ToList());

        foreach (var group in category.Items.GroupBy(GetOperationGroup).OrderBy(group => group.Key))
        {
            AddDailySubTab(tabs, group.Key, $"{group.Key} operation weekly missions.", group.ToList());
        }

        return tabs;
    }

    private static string GetOperationGroup(DailyRecommendation item)
    {
        var name = item.Name
            .Replace("[WEEKLY] ", "")
            .Replace(" (Story)", "")
            .Replace(" (Veteran)", "")
            .Replace(" (Master)", "");
        return name.Length > 18 ? name[..18] : name;
    }

    private Control BuildDailySubTabs(CategoryData category)
    {
        var tabs = new BorderlessTabControl
        {
            Dock = DockStyle.Fill,
            DrawMode = TabDrawMode.OwnerDrawFixed,
            ItemSize = new Size(135, 30),
            SizeMode = TabSizeMode.Fixed,
            Font = new Font(Font.FontFamily, 9F, FontStyle.Bold),
            BackColor = _background
        };
        tabs.DrawItem += DrawNavigationTab;
        tabs.Paint += PaintTabControlBackground;

        var priority = category.Items
            .Where(item => item.Planet is "PvP" or "Starfighter" or "CZ-198" or "Black Hole")
            .Take(12)
            .ToList();
        AddDailySubTab(tabs, "Priority", category.Description, priority);

        foreach (var group in category.Items.GroupBy(item => item.Planet).OrderBy(group => group.Key))
        {
            AddDailySubTab(tabs, group.Key, $"{group.Key} daily missions.", group.ToList());
        }

        return tabs;
    }

    private void AddDailySubTab(TabControl tabs, string name, string description, List<DailyRecommendation> items)
    {
        var page = new TabPage(BuildTabText(name, items))
        {
            BackColor = Color.Transparent,
            ForeColor = _text,
            Padding = new Padding(0, 0, 0, 0)
        };
        page.Controls.Add(BuildItemList(description, items));
        tabs.TabPages.Add(page);
    }

    private Control BuildItemList(string description, List<DailyRecommendation> items)
    {
        var scroller = new DarkScrollPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(7, 13, 19),
            ThumbColor = Color.FromArgb(60, 80, 100),
            TrackColor = Color.FromArgb(7, 13, 19)
        };

        var stack = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(4)
        };
        scroller.Controls.Add(stack);
        scroller.Resize += (_, _) => ResizeCards(scroller);

        stack.Controls.Add(new Label
        {
            AutoSize = true,
            MaximumSize = new Size(1060, 0),
            Margin = new Padding(0, 0, 0, 12),
            ForeColor = _muted,
            Text = description
        });

        foreach (var item in FilterItems(items))
        {
            stack.Controls.Add(BuildDailyCard(item));
        }

        return scroller;
    }

    private IEnumerable<DailyRecommendation> FilterItems(IEnumerable<DailyRecommendation> items)
    {
        return _hideCompleted
            ? items.Where(item => !_progress.IsCompleted(item.Name))
            : items;
    }

    private Control BuildDailyCard(DailyRecommendation item)
    {
        var card = new Panel
        {
            Width = 980,
            Height = 205,
            Margin = new Padding(0, 0, 0, 14),
            Padding = new Padding(16),
            BackColor = _progress.IsCompleted(item.Name) ? Color.FromArgb(0, 55, 35) : _panel
        };
        _cards.Add(card);

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 520));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
        card.Controls.Add(content);

        var left = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            Dock = DockStyle.Fill,
            WrapContents = false,
            AutoScroll = false
        };
        left.Controls.Add(new Label
        {
            AutoSize = true,
            ForeColor = _text,
            Text = item.Name,
            Font = new Font(Font.FontFamily, 14F, FontStyle.Bold)
        });
        left.Controls.Add(new Label
        {
            AutoSize = true,
            ForeColor = _accent,
            Text = $"{item.Planet}  |  {item.Priority}",
            Font = new Font(Font.FontFamily, 9.5F, FontStyle.Bold)
        });
        left.Controls.Add(new Label
        {
            AutoSize = true,
            MaximumSize = new Size(620, 0),
            Margin = new Padding(0, 8, 0, 8),
            ForeColor = _muted,
            Text = item.Why
        });
        left.Controls.Add(BuildTagSection("Earns", item.Rewards, _accentAlt));
        left.Controls.Add(BuildTagRow(item.Tags));

        var right = new TableLayoutPanel
        {
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            BackColor = _panelAlt,
            Padding = new Padding(10),
            ColumnCount = 2,
            RowCount = 2,
            Height = 128,
            Margin = new Padding(8, 21, 8, 0)
        };
        right.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        right.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        right.Controls.Add(MetaChip("Level", item.LevelRequirement), 0, 0);
        right.Controls.Add(MetaChip("Access", item.Access), 1, 0);
        right.Controls.Add(MetaChip("Location", item.Location), 0, 1);
        right.Controls.Add(MetaChip("Tasks", string.Join("; ", item.Objectives)), 1, 1);

        var actionPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8, 0, 0, 0)
        };
        var complete = MakeButton(_progress.IsCompleted(item.Name) ? "Undo" : "Done");
        complete.Width = 92;
        complete.Height = 44;
        complete.Location = new Point(8, 64);
        complete.Click += (_, _) => _progress.SetCompleted(item.Name, !_progress.IsCompleted(item.Name));
        actionPanel.Controls.Add(complete);
        if (!_cardBindings.TryGetValue(item.Name, out var bindings))
        {
            bindings = [];
            _cardBindings[item.Name] = bindings;
        }

        bindings.Add(new CardBinding(card, complete));

        content.Controls.Add(left, 0, 0);
        content.Controls.Add(right, 1, 0);
        content.Controls.Add(actionPanel, 2, 0);
        return card;
    }

    private void ResizeCards(Control scroller)
    {
        var width = Math.Max(760, scroller.ClientSize.Width - 36);
        foreach (var card in _cards.Where(card => card.Parent?.Parent == scroller))
        {
            card.Width = width;
        }
    }

    private Control BuildTagRow(List<string> tags)
    {
        var row = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            MaximumSize = new Size(760, 0)
        };

        foreach (var tag in tags)
        {
            row.Controls.Add(new Label
            {
                AutoSize = true,
                Text = $"{GetTagIcon(tag)} {tag}",
                ForeColor = _text,
                BackColor = _panelAlt,
                Font = new Font(Font.FontFamily, 8F, FontStyle.Bold),
                Padding = new Padding(6, 3, 6, 3),
                Margin = new Padding(0, 0, 8, 8)
            });
        }

        return row;
    }

    private static string GetTagIcon(string tag)
    {
        var value = tag.ToLowerInvariant();
        if (value.Contains("conquest"))
        {
            return "[C]";
        }

        if (value.Contains("season"))
        {
            return "[S]";
        }

        if (value.Contains("daily"))
        {
            return "[D]";
        }

        if (value.Contains("weekly"))
        {
            return "[W]";
        }

        if (value.Contains("flashpoint"))
        {
            return "[FP]";
        }

        if (value.Contains("operation"))
        {
            return "[OP]";
        }

        if (value.Contains("pvp"))
        {
            return "[PVP]";
        }

        if (value.Contains("story"))
        {
            return "[ST]";
        }

        if (value.Contains("datacron"))
        {
            return "[DC]";
        }

        return "[+]";
    }

    private Control BuildTagSection(string label, List<string> tags, Color color)
    {
        var wrapper = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0, 4, 0, 0)
        };

        wrapper.Controls.Add(new Label
        {
            AutoSize = true,
            ForeColor = color,
            Text = label,
            Font = new Font(Font.FontFamily, 8.5F, FontStyle.Bold)
        });

        wrapper.Controls.Add(BuildTagRow(tags));
        return wrapper;
    }

    private Control MetaLine(string label, string value)
    {
        return new Label
        {
            AutoSize = true,
            MaximumSize = new Size(220, 0),
            Margin = new Padding(0, 0, 0, 10),
            ForeColor = _text,
            Text = $"{label}: {value}"
        };
    }

    private Control MetaChip(string label, string value)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = _panel,
            Margin = new Padding(3),
            Padding = new Padding(8, 6, 8, 6)
        };

        var title = new Label
        {
            Dock = DockStyle.Top,
            Height = 16,
            ForeColor = _accentAlt,
            Text = label,
            Font = new Font(Font.FontFamily, 8F, FontStyle.Bold)
        };
        var body = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = _text,
            Text = value,
            Font = new Font(Font.FontFamily, 8.5F, FontStyle.Bold)
        };

        panel.Controls.Add(body);
        panel.Controls.Add(title);
        return panel;
    }

    private Button MakeButton(string text)
    {
        var button = new Button
        {
            AutoSize = true,
            Text = text,
            BackColor = _panelAlt,
            ForeColor = _text,
            FlatStyle = FlatStyle.Flat,
            Padding = new Padding(12, 6, 12, 6),
            Margin = new Padding(8, 0, 0, 0)
        };
        button.FlatAppearance.BorderColor = _accentAlt;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(36, 50, 66);
        return button;
    }

    private Button MakeResetButton()
    {
        var button = new Button
        {
            AutoSize = false,
            Width = 110,
            Height = 26,
            Text = "Reset done",
            BackColor = Color.FromArgb(60, 39, 29, 31),
            ForeColor = Color.FromArgb(255, 200, 200),
            FlatStyle = FlatStyle.Flat,
            Font = new Font(Font.FontFamily, 7.5F, FontStyle.Bold),
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(160, 72, 74);
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(80, 64, 37, 40);
        button.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var glowPen = new Pen(Color.FromArgb(50, 200, 60, 62), 3);
            e.Graphics.DrawRectangle(glowPen, 1, 1, button.Width - 3, button.Height - 3);
            using var topAccent = new SolidBrush(Color.FromArgb(160, 72, 74));
            e.Graphics.FillRectangle(topAccent, 0, 0, button.Width, 2);
        };
        return button;
    }

    private Control MakeSwitch(string text, bool isOn, Action<bool> changed)
    {
        var wrapper = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 0, 12, 0),
            Padding = new Padding(0, 7, 0, 0)
        };

        var toggle = new SwitchControl
        {
            Checked = isOn,
            Accent = _accent,
            OffColor = _panelAlt,
            Margin = new Padding(0, 0, 7, 0)
        };
        var label = new Label
        {
            AutoSize = true,
            Text = text,
            ForeColor = _text,
            Padding = new Padding(0, 2, 0, 0),
            Font = new Font(Font.FontFamily, 9F, FontStyle.Bold)
        };

        toggle.CheckedChanged += (_, _) => changed(toggle.Checked);
        label.Click += (_, _) => toggle.Checked = !toggle.Checked;
        wrapper.Controls.Add(toggle);
        wrapper.Controls.Add(label);
        return wrapper;
    }

    private static Color ParseColor(string hex)
    {
        try
        {
            return ColorTranslator.FromHtml(hex);
        }
        catch
        {
            return Color.White;
        }
    }
}
