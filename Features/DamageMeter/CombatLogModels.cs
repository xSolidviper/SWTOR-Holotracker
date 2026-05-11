namespace SwtorDailyTool;

public sealed record CombatEvent(
    DateTime Timestamp,
    string Source,
    string Target,
    bool SourceIsPlayer,
    bool TargetIsPlayer,
    string AbilityName,
    CombatEventKind Kind,
    long Amount,
    bool IsCrit,
    string DamageType);

public enum CombatEventKind
{
    Other,
    Damage,
    Heal,
    EnterCombat,
    ExitCombat
}

public sealed class AbilityStats
{
    public string Name { get; init; } = "";
    public long TotalAmount { get; set; }
    public int HitCount { get; set; }
    public int CritCount { get; set; }
    public long MinHit { get; set; } = long.MaxValue;
    public long MaxHit { get; set; }

    public long AverageHit => HitCount > 0 ? TotalAmount / HitCount : 0;
    public double CritRate => HitCount > 0 ? (double)CritCount / HitCount : 0;

    public void Record(long amount, bool isCrit)
    {
        TotalAmount += amount;
        HitCount++;
        if (isCrit)
        {
            CritCount++;
        }
        if (amount > 0)
        {
            if (amount < MinHit)
            {
                MinHit = amount;
            }
            if (amount > MaxHit)
            {
                MaxHit = amount;
            }
        }
    }
}

public sealed class ParticipantStats
{
    public string Name { get; init; } = "";
    public bool IsPlayer { get; init; }
    public bool IsLocalPlayer { get; set; }
    public long TotalDamage { get; set; }
    public long TotalHealing { get; set; }
    public Dictionary<string, AbilityStats> DamageAbilities { get; } =
        new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, AbilityStats> HealingAbilities { get; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class FightSegment
{
    public string Label { get; set; } = "Combat";
    public DateTime StartedAtUtc { get; set; }
    public DateTime LastEventUtc { get; set; }

    public Dictionary<string, ParticipantStats> Participants { get; } =
        new(StringComparer.OrdinalIgnoreCase);
    public long TotalDamage { get; set; }
    public long TotalHealing { get; set; }

    public TimeSpan Duration =>
        LastEventUtc > StartedAtUtc ? LastEventUtc - StartedAtUtc : TimeSpan.Zero;

    public long DamagePerSecond
    {
        get
        {
            var seconds = Duration.TotalSeconds;
            return seconds >= 1 ? (long)(TotalDamage / seconds) : TotalDamage;
        }
    }

    public long HealingPerSecond
    {
        get
        {
            var seconds = Duration.TotalSeconds;
            return seconds >= 1 ? (long)(TotalHealing / seconds) : TotalHealing;
        }
    }
}
