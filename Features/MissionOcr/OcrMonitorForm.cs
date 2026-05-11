using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace SwtorDailyTool;

public sealed class OcrMonitorForm : Form
{
    private readonly DailyToolData _data;
    private readonly ProgressStore _progress;
    private readonly string _baseDirectory;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly Label _status;
    private readonly Button _toggle;
    private readonly Button _debugBoxes;
    private readonly string? _tesseractPath;
    private readonly List<ScanOverlayForm> _scanOverlays = [];
    private int _scanCount;
    private bool _running;
    private bool _busy;

    public OcrMonitorForm(DailyToolData data, ProgressStore progress)
    {
        _data = data;
        _progress = progress;
        _baseDirectory = AppContext.BaseDirectory;
        _tesseractPath = FindTesseract();

        Text = "SWTOR OCR Overlay";
        Size = new Size(390, 170);
        MinimumSize = new Size(390, 170);
        StartPosition = FormStartPosition.Manual;
        Location = new Point(40, 40);
        TopMost = true;
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        BackColor = Color.FromArgb(22, 29, 39);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14),
            RowCount = 3,
            ColumnCount = 1
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(layout);

        layout.Controls.Add(new Label
        {
            AutoSize = true,
            ForeColor = Color.FromArgb(72, 191, 227),
            Text = "Mission Complete OCR",
            Font = new Font(Font.FontFamily, 12F, FontStyle.Bold)
        }, 0, 0);

        _status = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(210, 223, 240),
            Text = _tesseractPath is null
                ? "Tesseract OCR was not found. Install Tesseract and restart the app to enable auto-detection."
                : "Ready. Put SWTOR in borderless/windowed mode. The scanner checks the whole screen in overlapping tiles, so the popup can be anywhere.",
        };
        layout.Controls.Add(_status, 0, 1);

        var buttonRow = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight
        };

        _toggle = new Button
        {
            AutoSize = true,
            Text = "Start Watching",
            BackColor = Color.FromArgb(32, 41, 56),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Enabled = _tesseractPath is not null,
            Padding = new Padding(12, 6, 12, 6)
        };
        _toggle.Click += (_, _) => Toggle();
        buttonRow.Controls.Add(_toggle);

        _debugBoxes = new Button
        {
            AutoSize = true,
            Text = "Show Scan Boxes",
            BackColor = Color.FromArgb(32, 41, 56),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Padding = new Padding(12, 6, 12, 6)
        };
        _debugBoxes.Click += (_, _) => ToggleScanBoxes();
        buttonRow.Controls.Add(_debugBoxes);
        layout.Controls.Add(buttonRow, 0, 2);

        _timer = new System.Windows.Forms.Timer { Interval = 250 };
        _timer.Tick += (_, _) => ScanScreen();
    }

    private void Toggle()
    {
        if (_running)
        {
            StopWatching();
        }
        else
        {
            StartWatching();
        }
    }

    public void StartWatching()
    {
        if (_running || _tesseractPath is null)
        {
            return;
        }

        _running = true;
        _toggle.Text = _running ? "Stop Watching" : "Start Watching";
        _status.Text = "Watching the active screen for SWTOR Mission Complete windows...";
        _timer.Start();
    }

    public void StopWatching()
    {
        if (!_running)
        {
            return;
        }

        _running = false;
        _toggle.Text = "Start Watching";
        _status.Text = "Stopped.";
        _timer.Stop();
    }

    private async void ScanScreen()
    {
        if (_busy || _tesseractPath is null)
        {
            return;
        }

        _busy = true;
        try
        {
            var text = await Task.Run(CaptureAndRead);
            SaveLastOcrText(text);
            if (string.IsNullOrWhiteSpace(text))
            {
                _status.Text = "Watching... OCR returned no text.";
                return;
            }

            if (!IsCompletionContext(text))
            {
                _status.Text = "Watching... mission text found only in a non-completion window.";
                return;
            }

            var match = FindMissionMatch(text);
            if (match is null)
            {
                _status.Text = "Completion window detected, but no known daily/weekly name matched.";
                return;
            }

            _progress.SetCompleted(match.Name, true);
            _status.Text = $"Completed: {match.Name}";
        }
        catch (Exception ex)
        {
            _status.Text = $"OCR error: {ex.Message}";
        }
        finally
        {
            _busy = false;
        }
    }

    private string CaptureAndRead()
    {
        if (_tesseractPath is null)
        {
            return "";
        }

        var debugResults = new List<string>();
        var scanIndex = 0;
        var screen = GetActiveScreen();
        foreach (var currentScreen in new[] { screen })
        {
            var bounds = currentScreen.Bounds;
            if (bounds == Rectangle.Empty)
            {
                continue;
            }

            using var bitmap = new Bitmap(bounds.Width, bounds.Height);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
            }

            var debugCapture = Path.Combine(_baseDirectory, "data", $"ocr-screen-{currentScreen.DeviceName.Replace("\\", "").Replace(".", "")}.png");
            bitmap.Save(debugCapture, System.Drawing.Imaging.ImageFormat.Png);

            var regions = BuildScanRegions(bitmap);
            if (regions.Count == 0)
            {
                debugResults.Add($"SCREEN {currentScreen.DeviceName} NO_DIALOG_CANDIDATE");
                continue;
            }

            for (var i = 0; i < Math.Min(1, regions.Count); i++)
            {
                using var crop = bitmap.Clone(regions[i], bitmap.PixelFormat);
                using var prepared = PrepareForOcr(crop);
                var imagePath = Path.Combine(Path.GetTempPath(), $"swtor-daily-ocr-{scanIndex}.png");
                prepared.Save(imagePath, System.Drawing.Imaging.ImageFormat.Png);
                var output = RunTesseract(imagePath, i == 0 ? 6 : 11);
                debugResults.Add($"SCREEN {currentScreen.DeviceName} FAST_REGION {regions[i]}");
                debugResults.Add(output);
                scanIndex++;

                if (IsCompletionContext(output) && FindMissionMatch(output) is not null)
                {
                    debugResults.Add("MATCH FOUND - OCR scan stopped early.");
                    return string.Join(Environment.NewLine, debugResults.Where(result => !string.IsNullOrWhiteSpace(result)));
                }
            }
        }

        return string.Join(Environment.NewLine, debugResults.Where(result => !string.IsNullOrWhiteSpace(result)));
    }

    private string RunTesseract(string imagePath, int pageSegmentationMode)
    {
        if (_tesseractPath is null)
        {
            return "";
        }

        var psi = new ProcessStartInfo
        {
            FileName = _tesseractPath,
            Arguments = $"\"{imagePath}\" stdout --psm {pageSegmentationMode}",
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(psi);
        if (process is null)
        {
            return "";
        }

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(10000);
        return output;
    }

    private List<Rectangle> BuildScanRegions(Bitmap screenshot)
    {
        _scanCount++;
        var width = screenshot.Width;
        var height = screenshot.Height;
        var regions = DetectMissionWindowCandidates(screenshot);
        if (regions.Count > 0)
        {
            return regions;
        }

        return regions;
    }

    private static List<Rectangle> DetectMissionWindowCandidates(Bitmap screenshot)
    {
        var stride = 4;
        var visited = new bool[(screenshot.Width / stride + 2), (screenshot.Height / stride + 2)];
        var candidates = new List<Rectangle>();

        for (var y = 0; y < screenshot.Height; y += stride)
        {
            for (var x = 0; x < screenshot.Width; x += stride)
            {
                var gx = x / stride;
                var gy = y / stride;
                if (visited[gx, gy] || !IsSwtorCyan(screenshot.GetPixel(x, y)))
                {
                    continue;
                }

                var component = FloodCyanComponent(screenshot, visited, gx, gy, stride);
                if (component.Width < 180 || component.Height < 100)
                {
                    continue;
                }

                var expanded = ExpandRectangle(component, screenshot.Size, 50, 70);
                if (expanded.Width >= 260 && expanded.Height >= 180)
                {
                    candidates.Add(expanded);
                }
            }
        }

        return candidates
            .OrderByDescending(rectangle => LooksLikeDialogShape(rectangle, screenshot.Size))
            .ThenByDescending(rectangle => rectangle.Width * rectangle.Height)
            .Take(3)
            .ToList();
    }

    private static bool LooksLikeDialogShape(Rectangle rectangle, Size screen)
    {
        var ratio = rectangle.Width / (double)Math.Max(1, rectangle.Height);
        var area = rectangle.Width * rectangle.Height;
        var screenArea = screen.Width * screen.Height;
        return ratio is > 0.55 and < 1.25
            && area > screenArea * 0.035
            && area < screenArea * 0.45;
    }

    private static Rectangle FloodCyanComponent(Bitmap screenshot, bool[,] visited, int startX, int startY, int stride)
    {
        var queue = new Queue<Point>();
        queue.Enqueue(new Point(startX, startY));
        visited[startX, startY] = true;

        var minX = startX * stride;
        var minY = startY * stride;
        var maxX = minX;
        var maxY = minY;
        var maxGridX = screenshot.Width / stride;
        var maxGridY = screenshot.Height / stride;

        while (queue.Count > 0)
        {
            var point = queue.Dequeue();
            var pixelX = Math.Min(point.X * stride, screenshot.Width - 1);
            var pixelY = Math.Min(point.Y * stride, screenshot.Height - 1);
            minX = Math.Min(minX, pixelX);
            minY = Math.Min(minY, pixelY);
            maxX = Math.Max(maxX, pixelX);
            maxY = Math.Max(maxY, pixelY);

            foreach (var next in new[]
            {
                new Point(point.X + 1, point.Y),
                new Point(point.X - 1, point.Y),
                new Point(point.X, point.Y + 1),
                new Point(point.X, point.Y - 1)
            })
            {
                if (next.X < 0 || next.Y < 0 || next.X > maxGridX || next.Y > maxGridY || visited[next.X, next.Y])
                {
                    continue;
                }

                var nx = Math.Min(next.X * stride, screenshot.Width - 1);
                var ny = Math.Min(next.Y * stride, screenshot.Height - 1);
                visited[next.X, next.Y] = true;
                if (IsSwtorCyan(screenshot.GetPixel(nx, ny)))
                {
                    queue.Enqueue(next);
                }
            }
        }

        return Rectangle.FromLTRB(minX, minY, maxX + stride, maxY + stride);
    }

    private static bool IsSwtorCyan(Color color)
    {
        return color.B >= 120
            && color.G >= 95
            && color.R <= 80
            && color.B - color.R >= 70
            && color.G - color.R >= 45;
    }

    private static Rectangle ExpandRectangle(Rectangle rectangle, Size bounds, int horizontal, int vertical)
    {
        var x = Math.Max(0, rectangle.X - horizontal);
        var y = Math.Max(0, rectangle.Y - vertical);
        var right = Math.Min(bounds.Width, rectangle.Right + horizontal);
        var bottom = Math.Min(bounds.Height, rectangle.Bottom + vertical);
        return Rectangle.FromLTRB(x, y, right, bottom);
    }

    private void ToggleScanBoxes()
    {
        if (_scanOverlays.Count > 0)
        {
            foreach (var overlay in _scanOverlays.ToList())
            {
                overlay.Close();
            }

            _scanOverlays.Clear();
            _debugBoxes.Text = "Show Scan Boxes";
            return;
        }

        foreach (var screen in Screen.AllScreens)
        {
            using var sample = new Bitmap(screen.Bounds.Width, screen.Bounds.Height);
            using (var graphics = Graphics.FromImage(sample))
            {
                graphics.CopyFromScreen(screen.Bounds.Location, Point.Empty, screen.Bounds.Size);
            }

            var overlay = new ScanOverlayForm(screen.Bounds, BuildScanRegions(sample), screen == GetActiveScreen());
            overlay.FormClosed += (_, _) => _scanOverlays.Remove(overlay);
            _scanOverlays.Add(overlay);
            overlay.Show();
        }

        _debugBoxes.Text = "Hide Scan Boxes";
    }

    private static Bitmap PrepareForOcr(Bitmap source)
    {
        var scale = 2;
        var prepared = new Bitmap(source.Width * scale, source.Height * scale);
        using var graphics = Graphics.FromImage(prepared);
        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        graphics.DrawImage(source, 0, 0, prepared.Width, prepared.Height);
        return prepared;
    }

    private void SaveLastOcrText(string text)
    {
        try
        {
            var path = Path.Combine(_baseDirectory, "data", "ocr-last.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text);
        }
        catch
        {
            // Debug OCR text is best-effort only.
        }
    }

    private static bool LooksLikeMissionComplete(string text)
    {
        var normalized = Normalize(text);
        return normalized.Contains("mission complete") || normalized.Contains("mission completed");
    }

    private static bool IsCompletionContext(string text)
    {
        var normalized = Normalize(text);
        if (normalized.Contains("missions") && normalized.Contains("send companion"))
        {
            return false;
        }

        return normalized.Contains("mission complete")
            || normalized.Contains("mission completed")
            || normalized.Contains("has completed")
            || normalized.Contains("successfully completed")
            || normalized.Contains("has returned")
            || normalized.Contains("provided rewards")
            || normalized.Contains("influence gain")
            || normalized.Contains(" accept ");
    }

    private DailyRecommendation? FindMissionMatch(string text)
    {
        var normalizedText = Normalize(text);
        DailyRecommendation? best = null;
        var bestScore = 0;

        foreach (var item in _data.Categories.SelectMany(category => category.Items))
        {
            var aliases = item.MissionAliases.Count > 0 ? item.MissionAliases : [item.Name];
            foreach (var alias in aliases)
            {
                var normalizedAlias = Normalize(alias);
                if (normalizedAlias.Length < 4)
                {
                    continue;
                }

                var score = normalizedText.Contains(normalizedAlias)
                    ? normalizedAlias.Length + 20
                    : FuzzyContainsScore(normalizedText, normalizedAlias);

                if (score > bestScore)
                {
                    best = item;
                    bestScore = score;
                }
            }
        }

        return bestScore >= 12 ? best : null;
    }

    private static int FuzzyContainsScore(string text, string alias)
    {
        var words = Regex.Split(alias, @"\s+").Where(word => word.Length >= 4).ToList();
        return words.Count(word => text.Contains(word)) * 5;
    }

    private static string Normalize(string value)
    {
        return Regex.Replace(value.ToLowerInvariant(), @"[^a-z0-9\s]+", " ").Trim();
    }

    private static string? FindTesseract()
    {
        var candidates = new[]
        {
            "tesseract.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Tesseract-OCR", "tesseract.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Tesseract-OCR", "tesseract.exe")
        };

        foreach (var candidate in candidates)
        {
            if (candidate.Contains(Path.DirectorySeparatorChar) && File.Exists(candidate))
            {
                return candidate;
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "where.exe",
                    Arguments = candidate,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                };
                using var process = Process.Start(psi);
                var result = process?.StandardOutput.ReadLine();
                process?.WaitForExit(1000);
                if (!string.IsNullOrWhiteSpace(result) && File.Exists(result))
                {
                    return result;
                }
            }
            catch
            {
                // Ignore lookup failures and try the next common install path.
            }
        }

        return null;
    }

    private static Screen GetActiveScreen()
    {
        var foreground = GetForegroundWindow();
        if (foreground != IntPtr.Zero && GetWindowRect(foreground, out var rect))
        {
            var bounds = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
            if (!bounds.IsEmpty)
            {
                return Screen.FromRectangle(bounds);
            }
        }

        return Screen.FromPoint(Cursor.Position);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect lpRect);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        foreach (var overlay in _scanOverlays.ToList())
        {
            overlay.Close();
        }

        base.OnFormClosed(e);
    }
}
