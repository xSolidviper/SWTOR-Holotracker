namespace SwtorDailyTool;

public sealed class DamageMeterStore
{
    public event Action? Changed;

    private static readonly TimeSpan FightIdleGap = TimeSpan.FromSeconds(6);

    private readonly object _lock = new();
    private readonly List<FightSegment> _history = [];
    private FightSegment? _current;
    private string? _localPlayerName;

    public IReadOnlyList<FightSegment> History
    {
        get
        {
            lock (_lock)
            {
                return _history.ToArray();
            }
        }
    }

    public FightSegment? CurrentFight
    {
        get
        {
            lock (_lock)
            {
                return _current;
            }
        }
    }

    public FightSegment? ActiveOrLastFight
    {
        get
        {
            lock (_lock)
            {
                return _current ?? (_history.Count > 0 ? _history[^1] : null);
            }
        }
    }

    public string? LocalPlayerName
    {
        get
        {
            lock (_lock)
            {
                return _localPlayerName;
            }
        }
    }

    public void Process(CombatEvent ev)
    {
        var changed = false;
        lock (_lock)
        {
            LearnLocalPlayer(ev);

            if (ev.Kind is CombatEventKind.EnterCombat)
            {
                StartNewFight(ev.Timestamp);
                changed = true;
            }
            else if (ev.Kind is CombatEventKind.ExitCombat)
            {
                if (_current is not null)
                {
                    _current.LastEventUtc = ev.Timestamp;
                    _history.Add(_current);
                    _current = null;
                    changed = true;
                }
            }
            else if (ev.Kind is CombatEventKind.Damage or CombatEventKind.Heal)
            {
                if (_current is null)
                {
                    StartNewFight(ev.Timestamp);
                    changed = true;
                }
                else if ((ev.Timestamp - _current.LastEventUtc) > FightIdleGap)
                {
                    _history.Add(_current);
                    StartNewFight(ev.Timestamp);
                    changed = true;
                }

                if (_current is not null)
                {
                    ApplyEvent(_current, ev);
                    changed = true;
                }
            }
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _history.Clear();
            _current = null;
            _localPlayerName = null;
        }
        Changed?.Invoke();
    }

    private void LearnLocalPlayer(CombatEvent ev)
    {
        if (!ev.SourceIsPlayer || string.IsNullOrWhiteSpace(ev.Source))
        {
            return;
        }

        if (ev.Kind is CombatEventKind.EnterCombat or CombatEventKind.ExitCombat)
        {
            _localPlayerName = ev.Source;
        }
        else if (_localPlayerName is null && ev.Target is "" or "=")
        {
            _localPlayerName = ev.Source;
        }
    }

    private void StartNewFight(DateTime startedAt)
    {
        _current = new FightSegment
        {
            StartedAtUtc = startedAt,
            LastEventUtc = startedAt
        };
    }

    private void ApplyEvent(FightSegment fight, CombatEvent ev)
    {
        fight.LastEventUtc = ev.Timestamp;
        if (ev.Amount <= 0 || string.IsNullOrEmpty(ev.AbilityName))
        {
            return;
        }

        // We're tracking the SOURCE of the action — players hitting NPCs / healing allies.
        // Skip events sourced from NPCs to keep the meter focused on the group.
        if (!ev.SourceIsPlayer)
        {
            return;
        }

        if (!fight.Participants.TryGetValue(ev.Source, out var participant))
        {
            participant = new ParticipantStats
            {
                Name = ev.Source,
                IsPlayer = ev.SourceIsPlayer,
                IsLocalPlayer = IsLocalPlayer(ev.Source)
            };
            fight.Participants[ev.Source] = participant;
        }
        else
        {
            participant.IsLocalPlayer = IsLocalPlayer(ev.Source);
        }

        if (ev.Kind is CombatEventKind.Damage)
        {
            participant.TotalDamage += ev.Amount;
            fight.TotalDamage += ev.Amount;
            GetOrCreateAbility(participant.DamageAbilities, ev.AbilityName).Record(ev.Amount, ev.IsCrit);
        }
        else if (ev.Kind is CombatEventKind.Heal)
        {
            participant.TotalHealing += ev.Amount;
            fight.TotalHealing += ev.Amount;
            GetOrCreateAbility(participant.HealingAbilities, ev.AbilityName).Record(ev.Amount, ev.IsCrit);
        }
    }

    private bool IsLocalPlayer(string name)
    {
        return !string.IsNullOrWhiteSpace(_localPlayerName)
            && string.Equals(name, _localPlayerName, StringComparison.OrdinalIgnoreCase);
    }

    private static AbilityStats GetOrCreateAbility(Dictionary<string, AbilityStats> table, string name)
    {
        if (!table.TryGetValue(name, out var stats))
        {
            stats = new AbilityStats { Name = name };
            table[name] = stats;
        }
        return stats;
    }
}
