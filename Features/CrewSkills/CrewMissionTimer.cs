namespace SwtorDailyTool;

public sealed record CrewMissionTimer(
    string Companion,
    string MissionName,
    DateTime StartedAtUtc,
    TimeSpan Duration,
    string? Yield = null,
    string? Influence = null)
{
    public DateTime EndsAtUtc => StartedAtUtc + Duration;
    public TimeSpan Remaining => EndsAtUtc - DateTime.UtcNow;
    public bool IsDone => Remaining <= TimeSpan.Zero;

    public double Progress
    {
        get
        {
            var total = Duration.TotalSeconds;
            if (total <= 0)
            {
                return 1.0;
            }
            var elapsed = (DateTime.UtcNow - StartedAtUtc).TotalSeconds;
            return Math.Clamp(elapsed / total, 0.0, 1.0);
        }
    }
}

public sealed class CrewMissionStore
{
    public event Action? Changed;

    private readonly object _lock = new();
    private readonly Dictionary<string, CrewMissionTimer> _byCompanion =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<CrewMissionTimer> Snapshot()
    {
        lock (_lock)
        {
            return _byCompanion.Values
                .OrderBy(timer => timer.EndsAtUtc)
                .ToList();
        }
    }

    public void Upsert(CrewMissionTimer timer)
    {
        lock (_lock)
        {
            _byCompanion[timer.Companion] = timer;
        }
        Changed?.Invoke();
    }

    public bool Remove(string companion)
    {
        bool removed;
        lock (_lock)
        {
            removed = _byCompanion.Remove(companion);
        }
        if (removed)
        {
            Changed?.Invoke();
        }
        return removed;
    }

    public void PruneCompleted(TimeSpan grace)
    {
        var cutoff = DateTime.UtcNow - grace;
        var changed = false;
        lock (_lock)
        {
            var stale = _byCompanion
                .Where(kv => kv.Value.EndsAtUtc < cutoff)
                .Select(kv => kv.Key)
                .ToList();
            foreach (var key in stale)
            {
                _byCompanion.Remove(key);
                changed = true;
            }
        }
        if (changed)
        {
            Changed?.Invoke();
        }
    }
}
