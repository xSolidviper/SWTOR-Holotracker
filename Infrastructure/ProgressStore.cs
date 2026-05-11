using System.Text.Json;

namespace SwtorDailyTool;

public sealed class ProgressStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    private readonly string _path;
    private readonly Dictionary<string, int> _counts;

    public ProgressStore(string baseDirectory)
    {
        _path = Path.Combine(baseDirectory, "data", "progress.json");
        _counts = Load();
    }

    public event EventHandler? Changed;

    public bool IsCompleted(string key) => GetCount(key) > 0;

    public bool IsCompleted(string key, int repeatLimit) => GetCount(key) >= Math.Max(1, repeatLimit);

    public int GetCount(string key) => _counts.TryGetValue(key, out var count) ? count : 0;

    public void RenameKey(string oldKey, string newKey)
    {
        if (oldKey.Equals(newKey, StringComparison.OrdinalIgnoreCase) || !_counts.TryGetValue(oldKey, out var count) || count <= 0)
        {
            return;
        }

        _counts.Remove(oldKey);
        if (!_counts.ContainsKey(newKey))
        {
            _counts[newKey] = count;
        }

        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Increment(string key, int repeatLimit)
    {
        var limit = Math.Max(1, repeatLimit);
        var next = Math.Min(limit, GetCount(key) + 1);
        SetCount(key, next);
    }

    public void Decrement(string key)
    {
        var next = Math.Max(0, GetCount(key) - 1);
        SetCount(key, next);
    }

    public void SetCompleted(string key, bool completed)
    {
        var next = completed ? 1 : 0;
        if (GetCount(key) == next)
        {
            return;
        }

        SetCount(key, next);
    }

    public void SetCount(string key, int count)
    {
        if (count <= 0)
        {
            if (!_counts.Remove(key))
            {
                return;
            }
        }
        else
        {
            if (GetCount(key) == count)
            {
                return;
            }

            _counts[key] = count;
        }

        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Reset()
    {
        if (_counts.Count == 0)
        {
            return;
        }

        _counts.Clear();
        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private Dictionary<string, int> Load()
    {
        if (!File.Exists(_path))
        {
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var json = File.ReadAllText(_path);
            var data = JsonSerializer.Deserialize<ProgressData>(json, Options);
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (data?.Counts is not null)
            {
                foreach (var pair in data.Counts)
                {
                    if (pair.Value > 0)
                    {
                        counts[pair.Key] = pair.Value;
                    }
                }
            }

            if (data?.Completed is not null)
            {
                foreach (var key in data.Completed)
                {
                    counts.TryAdd(key, 1);
                }
            }

            return counts;
        }
        catch
        {
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(new ProgressData
        {
            Completed = _counts.Where(pair => pair.Value > 0).Select(pair => pair.Key).Order(StringComparer.OrdinalIgnoreCase).ToList(),
            Counts = _counts.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).ToDictionary(pair => pair.Key, pair => pair.Value)
        }, Options));
    }
}
