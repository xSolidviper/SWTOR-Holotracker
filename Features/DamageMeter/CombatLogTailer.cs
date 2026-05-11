using System.Text;

namespace SwtorDailyTool;

public sealed class CombatLogTailer : IDisposable
{
    public event Action<CombatEvent>? EventReceived;
    public event Action<string>? StatusChanged;

    private readonly SynchronizationContext? _uiContext;
    private CancellationTokenSource? _cts;
    private Task? _task;
    private string? _activeFile;
    private bool _running;

    public CombatLogTailer()
    {
        _uiContext = SynchronizationContext.Current;
    }

    public bool IsRunning => _running;
    public string? ActiveFile => _activeFile;

    public static string DefaultLogDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Star Wars - The Old Republic",
            "CombatLogs");

    public void Start()
    {
        if (_running)
        {
            return;
        }

        _running = true;
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _task = Task.Run(() => RunLoop(token), token);
    }

    public void Stop()
    {
        if (!_running)
        {
            return;
        }

        _running = false;
        _cts?.Cancel();
        try { _task?.Wait(2000); } catch { /* shutdown is best-effort */ }
        _cts?.Dispose();
        _cts = null;
        _task = null;
        _activeFile = null;
    }

    public void Dispose() => Stop();

    private async Task RunLoop(CancellationToken token)
    {
        var directory = DefaultLogDirectory;
        if (!Directory.Exists(directory))
        {
            Status($"Combat log folder not found at {directory}. Enable combat logging in SWTOR (Preferences → User Interface → Enable Combat Logging).");
            return;
        }

        Status("Watching for combat log activity…");

        string? currentPath = null;
        FileStream? stream = null;
        StreamReader? reader = null;
        DateTime currentDate = DateTime.Today;

        try
        {
            while (!token.IsCancellationRequested)
            {
                var latest = FindLatestLog(directory);
                if (latest is null)
                {
                    Status("Waiting for SWTOR to create a combat log…");
                    await Task.Delay(1000, token);
                    continue;
                }

                if (latest != currentPath)
                {
                    reader?.Dispose();
                    stream?.Dispose();

                    currentPath = latest;
                    _activeFile = currentPath;
                    currentDate = File.GetLastWriteTime(currentPath).Date;
                    Status($"Tailing: {Path.GetFileName(currentPath)}");

                    stream = new FileStream(
                        currentPath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete);
                    stream.Seek(0, SeekOrigin.End);
                    // SWTOR writes combat logs in Windows-1252 / Latin-1, not UTF-8 —
                    // bytes like 0xD4 ("Ô") would otherwise be replaced with U+FFFD and
                    // render as a diamond glyph in the meter UI.
                    reader = new StreamReader(stream, Encoding.Latin1);
                }

                if (reader is null)
                {
                    await Task.Delay(500, token);
                    continue;
                }

                var line = await reader.ReadLineAsync(token).ConfigureAwait(false);
                if (line is null)
                {
                    // EOF — wait briefly, but check for new file in case of rotation.
                    await Task.Delay(250, token);
                    var rotated = FindLatestLog(directory);
                    if (rotated != currentPath)
                    {
                        currentPath = null;
                    }
                    continue;
                }

                var ev = CombatLogParser.Parse(line, currentDate);
                if (ev is not null)
                {
                    Raise(ev);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        catch (Exception ex)
        {
            Status($"Combat log tailer error: {ex.Message}");
        }
        finally
        {
            reader?.Dispose();
            stream?.Dispose();
        }
    }

    private static string? FindLatestLog(string directory)
    {
        try
        {
            var files = Directory.GetFiles(directory, "combat_*.txt");
            if (files.Length == 0)
            {
                return null;
            }
            return files
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .First();
        }
        catch
        {
            return null;
        }
    }

    private void Raise(CombatEvent ev)
    {
        if (_uiContext is null)
        {
            EventReceived?.Invoke(ev);
        }
        else
        {
            _uiContext.Post(_ => EventReceived?.Invoke(ev), null);
        }
    }

    private void Status(string message)
    {
        if (_uiContext is null)
        {
            StatusChanged?.Invoke(message);
        }
        else
        {
            _uiContext.Post(_ => StatusChanged?.Invoke(message), null);
        }
    }
}
