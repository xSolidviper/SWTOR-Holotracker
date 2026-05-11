using System.Runtime.InteropServices;

namespace SwtorDailyTool;

public sealed class MissionSendDetectedEventArgs : EventArgs
{
    public required Point ClickPoint { get; init; }
    public required MissionsPanelLayout Layout { get; init; }
    public required Rectangle SelectedMissionRowOnScreen { get; init; }
    public required Bitmap Screenshot { get; init; }
}

public sealed class CrewMissionWatcher : IDisposable
{
    public event EventHandler<MissionSendDetectedEventArgs>? MissionSendDetected;
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
                if (_lastListClick is { } lastClick && layout.MissionListRect.Contains(lastClick))
                {
                    var screenshot = CaptureScreenContaining(clickPoint);
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
                        Screenshot = screenshot
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

        using var screenshot = CaptureScreenContaining(clickPoint);
        if (screenshot is null)
        {
            return null;
        }

        var layout = MissionsPanelLocator.Locate(screenshot);
        if (layout is null)
        {
            _cachedLayout = null;
            return null;
        }

        _cachedLayout = layout;
        _layoutCachedAt = DateTime.UtcNow;
        return layout;
    }

    private static Rectangle BuildSelectedRowRect(Rectangle listRect, Point click)
    {
        // SWTOR mission rows are ~175px tall (title, 2-3 lines of description, yield line,
        // influence line). Clicking anywhere in the row should still capture the title, so
        // the crop runs from 200px above to 30px below the click. The previous row's bottom
        // metadata gets included sometimes, but the parser drops "Influence:" / "Yield:" /
        // "Cost:" lines and finds the title.
        var top = Math.Max(listRect.Top, click.Y - 200);
        var bottom = Math.Min(listRect.Bottom, click.Y + 30);
        return Rectangle.FromLTRB(listRect.Left, top, listRect.Right, bottom);
    }

    private static Bitmap? CaptureScreenContaining(Point point)
    {
        var screen = Screen.FromPoint(point);
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
