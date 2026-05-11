using System.Runtime.InteropServices;

namespace SwtorDailyTool;

public sealed class MissionSendDetectedEventArgs : EventArgs
{
    public required Point ClickPoint { get; init; }
    public required MissionsPanelLayout Layout { get; init; }
    public required Rectangle SelectedMissionRowOnScreen { get; init; }
    public required Bitmap Screenshot { get; init; }
    public required Point ScreenshotOrigin { get; init; }
}

public sealed class CrewMissionSnapshotEventArgs : EventArgs
{
    public required Point ClickPoint { get; init; }
    public required MissionsPanelLayout Layout { get; init; }
    public required Bitmap Screenshot { get; init; }
    public required Point ScreenshotOrigin { get; init; }
}

public sealed class CrewMissionWatcher : IDisposable
{
    public event EventHandler<MissionSendDetectedEventArgs>? MissionSendDetected;
    public event EventHandler<CrewMissionSnapshotEventArgs>? ActiveCrewSnapshotCaptured;
    public event Action<string>? StatusChanged;

    private readonly Win32MouseHook _hook = new();
    private readonly SynchronizationContext? _uiContext;
    private Point? _lastListClick;
    private MissionsPanelLayout? _cachedLayout;
    private DateTime _layoutCachedAt = DateTime.MinValue;
    private bool _running;

    public CrewMissionWatcher()
    {
        _uiContext = SynchronizationContext.Current;
        _hook.LeftButtonDown += OnLeftDown;
    }

    public bool IsRunning => _running;

    public void Start()
    {
        if (_running)
        {
            return;
        }

        _hook.Install();
        _running = true;
        CrewMissionDebugLog.Write("Crew mission watcher started; mouse hook installed.");
        Status("Watching for SEND COMPANION clicks.");
    }

    public void Stop()
    {
        if (!_running)
        {
            return;
        }

        _hook.Uninstall();
        _running = false;
        _lastListClick = null;
        _cachedLayout = null;
        CrewMissionDebugLog.Write("Crew mission watcher stopped; mouse hook uninstalled.");
        Status("Stopped.");
    }

    public void Dispose()
    {
        _hook.LeftButtonDown -= OnLeftDown;
        _hook.Dispose();
    }

    private void OnLeftDown(Point clickPoint)
    {
        // Hook callback runs on the hook thread — keep work off it.
        ThreadPool.QueueUserWorkItem(_ => HandleClick(clickPoint));
    }

    private void HandleClick(Point clickPoint)
    {
        try
        {
            var layout = GetLayout(clickPoint);
            if (layout is null)
            {
                CrewMissionDebugLog.Write($"CLICK at {clickPoint} — no Missions dialog detected (skipped).");
                return;
            }

            if (layout.SendCompanionRect.Contains(clickPoint))
            {
                QueueActiveCrewSnapshot(clickPoint, layout);
                if (_lastListClick is { } lastClick && layout.MissionListRect.Contains(lastClick))
                {
                    var screenshot = CaptureScreenContaining(clickPoint, out var screenshotOrigin);
                    if (screenshot is null)
                    {
                        CrewMissionDebugLog.Write($"SEND COMPANION at {clickPoint} — screenshot capture failed.");
                        return;
                    }

                    var rowRect = BuildSelectedRowRect(layout.MissionListRect, lastClick);
                    var args = new MissionSendDetectedEventArgs
                    {
                        ClickPoint = clickPoint,
                        Layout = layout,
                        SelectedMissionRowOnScreen = rowRect,
                        Screenshot = screenshot,
                        ScreenshotOrigin = screenshotOrigin
                    };
                    CrewMissionDebugLog.Write(
                        $"SEND COMPANION at {clickPoint} — captured. lastListClick={lastClick}, dialogRect={layout.DialogRect}, rowRect={rowRect}.");
                    Status($"SEND COMPANION click captured at {clickPoint}.");
                    Raise(args);
                }
                else
                {
                    var why = _lastListClick is null
                        ? "no prior mission row click recorded"
                        : $"last list click {_lastListClick} fell outside MissionListRect {layout.MissionListRect}";
                    CrewMissionDebugLog.Write($"SEND COMPANION at {clickPoint} — IGNORED ({why}).");
                    Status("SEND COMPANION clicked but no prior mission selection observed.");
                }

                return;
            }

            if (layout.MissionListRect.Contains(clickPoint))
            {
                _lastListClick = clickPoint;
                CrewMissionDebugLog.Write($"MISSION ROW CLICK at {clickPoint} — remembered.");
                Status($"Mission row click remembered at {clickPoint}.");
            }
            else
            {
                CrewMissionDebugLog.Write(
                    $"CLICK at {clickPoint} inside dialog {layout.DialogRect} but outside MissionListRect {layout.MissionListRect} and SendCompanionRect {layout.SendCompanionRect}.");
            }
        }
        catch (Exception ex)
        {
            CrewMissionDebugLog.Write($"Click handler error at {clickPoint}: {ex}");
            Status($"Click handler error: {ex.Message}");
        }
    }

    private MissionsPanelLayout? GetLayout(Point clickPoint)
    {
        // Cache the dialog layout for a few seconds so back-to-back clicks don't each pay
        // the flood-fill cost. Invalidate aggressively on the first click that doesn't fit.
        if (_cachedLayout is { } cached
            && (DateTime.UtcNow - _layoutCachedAt).TotalSeconds < 4
            && cached.DialogRect.Contains(clickPoint))
        {
            return cached;
        }

        using var screenshot = CaptureScreenContaining(clickPoint, out var screenshotOrigin);
        if (screenshot is null)
        {
            return null;
        }

        var layout = MissionsPanelLocator.Locate(screenshot, new Point(clickPoint.X - screenshotOrigin.X, clickPoint.Y - screenshotOrigin.Y));
        if (layout is null)
        {
            _cachedLayout = null;
            return null;
        }

        layout = TranslateLayout(layout, screenshotOrigin);
        _cachedLayout = layout;
        _layoutCachedAt = DateTime.UtcNow;
        return layout;
    }

    private static MissionsPanelLayout TranslateLayout(MissionsPanelLayout layout, Point offset)
    {
        return new MissionsPanelLayout(
            Offset(layout.DialogRect, offset),
            Offset(layout.SendCompanionRect, offset),
            Offset(layout.MissionListRect, offset),
            Offset(layout.CompanionPanelRect, offset));
    }

    private static Rectangle Offset(Rectangle rect, Point offset)
    {
        return new Rectangle(rect.X + offset.X, rect.Y + offset.Y, rect.Width, rect.Height);
    }

    private static Rectangle BuildSelectedRowRect(Rectangle listRect, Point click)
    {
        // SWTOR mission rows put reward/influence below the description. A click near the
        // title needs much more space below it, while a click near the reward line still
        // needs enough space above to keep the title in-frame.
        var top = Math.Max(listRect.Top, click.Y - 120);
        var bottom = Math.Min(listRect.Bottom, click.Y + 190);
        return Rectangle.FromLTRB(listRect.Left, top, listRect.Right, bottom);
    }

    private void QueueActiveCrewSnapshot(Point clickPoint, MissionsPanelLayout layout)
    {
        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                Thread.Sleep(850);
                var screenshot = CaptureScreenContaining(clickPoint, out var screenshotOrigin);
                if (screenshot is null)
                {
                    CrewMissionDebugLog.Write("ACTIVE CREW SNAPSHOT — screenshot capture failed.");
                    return;
                }

                Raise(new CrewMissionSnapshotEventArgs
                {
                    ClickPoint = clickPoint,
                    Layout = layout,
                    Screenshot = screenshot,
                    ScreenshotOrigin = screenshotOrigin
                });
            }
            catch (Exception ex)
            {
                CrewMissionDebugLog.Write($"ACTIVE CREW SNAPSHOT error: {ex}");
            }
        });
    }

    private static Bitmap? CaptureScreenContaining(Point point, out Point screenshotOrigin)
    {
        var screen = Screen.FromPoint(point);
        screenshotOrigin = screen.Bounds.Location;
        if (screen.Bounds.IsEmpty)
        {
            return null;
        }

        var bitmap = new Bitmap(screen.Bounds.Width, screen.Bounds.Height);
        try
        {
            using var graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(screen.Bounds.Location, Point.Empty, screen.Bounds.Size);
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }

        return bitmap;
    }

    private void Status(string message)
    {
        Post(() => StatusChanged?.Invoke(message));
    }

    private void Raise(MissionSendDetectedEventArgs args)
    {
        Post(() => MissionSendDetected?.Invoke(this, args));
    }

    private void Raise(CrewMissionSnapshotEventArgs args)
    {
        Post(() => ActiveCrewSnapshotCaptured?.Invoke(this, args));
    }

    private void Post(Action action)
    {
        if (_uiContext is null)
        {
            action();
        }
        else
        {
            _uiContext.Post(_ => action(), null);
        }
    }
}
