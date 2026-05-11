using System.Text.Json;

namespace SwtorDailyTool;

public sealed class AbilityIconCache
{
    private readonly Dictionary<string, string> _abilityToFile;
    private readonly Dictionary<string, Image?> _imageCache =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly string _iconsDirectory;
    private readonly object _lock = new();

    public AbilityIconCache(string baseDirectory)
    {
        _iconsDirectory = Path.Combine(baseDirectory, "data", "images", "abilities");
        _abilityToFile = LoadMapping(baseDirectory);
    }

    public Image? GetIcon(string abilityName)
    {
        if (string.IsNullOrWhiteSpace(abilityName))
        {
            return null;
        }

        lock (_lock)
        {
            if (_imageCache.TryGetValue(abilityName, out var cached))
            {
                return cached;
            }

            var image = ResolveImage(abilityName);
            _imageCache[abilityName] = image;
            return image;
        }
    }

    private Image? ResolveImage(string abilityName)
    {
        string? file = null;
        if (!_abilityToFile.TryGetValue(abilityName, out file))
        {
            var trimmed = abilityName.Split('(')[0].Trim();
            if (!string.IsNullOrEmpty(trimmed))
            {
                _abilityToFile.TryGetValue(trimmed, out file);
            }
        }

        if (file is null)
        {
            return null;
        }

        var path = Path.Combine(_iconsDirectory, file);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            // Image.FromStream needs the stream to live as long as the Image. The
            // MemoryStream is unmanaged-resource-free, so leaking it via the Image
            // reference is safe — both get reclaimed together by GC.
            var bytes = File.ReadAllBytes(path);
            var ms = new MemoryStream(bytes, writable: false);
            return Image.FromStream(ms);
        }
        catch
        {
            return null;
        }
    }

    private static Dictionary<string, string> LoadMapping(string baseDirectory)
    {
        var path = Path.Combine(baseDirectory, "data", "abilities-icons.json");
        var diagnosticPath = Path.Combine(baseDirectory, "data", "ability-icon-load.txt");
        try
        {
            if (!File.Exists(path))
            {
                File.WriteAllText(diagnosticPath, $"Mapping file not found: {path}\n");
                return new(StringComparer.OrdinalIgnoreCase);
            }

            using var stream = File.OpenRead(path);
            var raw = JsonSerializer.Deserialize<Dictionary<string, string>>(stream);
            if (raw is null)
            {
                File.WriteAllText(diagnosticPath, $"Mapping deserialized as null from: {path}\n");
                return new(StringComparer.OrdinalIgnoreCase);
            }

            var dict = new Dictionary<string, string>(raw, StringComparer.OrdinalIgnoreCase);
            File.WriteAllText(diagnosticPath, $"Loaded {dict.Count} ability icon mappings from {path}\n  Vital Shot -> {(dict.TryGetValue("Vital Shot", out var v) ? v : "(missing)")}\n");
            return dict;
        }
        catch (Exception ex)
        {
            try { File.WriteAllText(diagnosticPath, $"Exception: {ex.GetType().Name}: {ex.Message}\nPath: {path}\n"); } catch { }
            return new(StringComparer.OrdinalIgnoreCase);
        }
    }
}
