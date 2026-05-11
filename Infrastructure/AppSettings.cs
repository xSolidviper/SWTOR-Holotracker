using System.Text.Json;

namespace SwtorDailyTool;

public sealed class AppSettings
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _path;

    public bool ShowNotificationOnComplete { get; set; } = true;
    public bool PlaySoundOnComplete { get; set; } = true;
    public bool AutoStartSkillTracking { get; set; } = false;
    public bool AutoRemoveCompletedTimers { get; set; } = false;

    // Damage overlay last-used bounds. Null means "not set yet".
    public int? OverlayX { get; set; }
    public int? OverlayY { get; set; }
    public int? OverlayWidth { get; set; }
    public int? OverlayHeight { get; set; }

    // Damage Meter behavior
    public bool AutoShowDamageOverlay { get; set; } = false;
    public int OverlayPlayerRows { get; set; } = 6;        // 3–10
    public int OverlayOpacityPercent { get; set; } = 94;   // 50–100
    public bool OverlayClickThrough { get; set; } = false; // SWTOR receives clicks under it
    public bool OverlayDoubleClickToOpen { get; set; } = false; // double-click opens breakdown

    public event EventHandler? Changed;

    private AppSettings(string baseDirectory)
    {
        _path = Path.Combine(baseDirectory, "data", "settings.json");
    }

    public static AppSettings Load(string baseDirectory)
    {
        var settings = new AppSettings(baseDirectory);
        if (!File.Exists(settings._path))
        {
            return settings;
        }

        try
        {
            var json = File.ReadAllText(settings._path);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json, Options);
            if (loaded is not null)
            {
                settings.ShowNotificationOnComplete = loaded.ShowNotificationOnComplete;
                settings.PlaySoundOnComplete = loaded.PlaySoundOnComplete;
                settings.AutoStartSkillTracking = loaded.AutoStartSkillTracking;
                settings.AutoRemoveCompletedTimers = loaded.AutoRemoveCompletedTimers;
                settings.OverlayX = loaded.OverlayX;
                settings.OverlayY = loaded.OverlayY;
                settings.OverlayWidth = loaded.OverlayWidth;
                settings.OverlayHeight = loaded.OverlayHeight;
                settings.AutoShowDamageOverlay = loaded.AutoShowDamageOverlay;
                settings.OverlayPlayerRows = Math.Clamp(loaded.OverlayPlayerRows, 3, 10);
                settings.OverlayOpacityPercent = Math.Clamp(loaded.OverlayOpacityPercent, 50, 100);
                settings.OverlayClickThrough = loaded.OverlayClickThrough;
                settings.OverlayDoubleClickToOpen = loaded.OverlayDoubleClickToOpen;
            }
        }
        catch
        {
            // Ignore corrupt settings file — fall back to defaults.
        }

        return settings;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(this, Options));
            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            // Best-effort save.
        }
    }
}
