using System.Globalization;
using System.Text.RegularExpressions;

namespace SwtorDailyTool;

public static class CombatLogParser
{
    // Format: [time] [source] [target] [ability] [effect] (amount)? <threat>?
    private static readonly Regex LineRegex = new(
        @"^\[(?<time>\d+:\d+:\d+\.\d+)\]\s+" +
        @"\[(?<source>[^\]]*)\]\s+" +
        @"\[(?<target>[^\]]*)\]\s+" +
        @"\[(?<ability>[^\]]*)\]\s+" +
        @"\[(?<effect>[^\]]*)\]" +
        @"(?:\s+\((?<amount>[^)]*)\))?" +
        @"(?:\s+<(?<threat>[^>]*)>)?\s*$",
        RegexOptions.Compiled);

    private static readonly Regex AmountRegex =
        new(@"(?<value>-?\d[\d,]*)\s*(?<type>[A-Za-z]+)?", RegexOptions.Compiled);

    public static CombatEvent? Parse(string line, DateTime logDate)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        var match = LineRegex.Match(line);
        if (!match.Success)
        {
            return null;
        }

        if (!TimeSpan.TryParse(match.Groups["time"].Value, CultureInfo.InvariantCulture, out var time))
        {
            return null;
        }

        var timestamp = logDate.Date + time;
        // Logs span midnight when SWTOR is left running — bump the date forward if the
        // timestamp went backwards from yesterday into early-morning today.
        if (timestamp < logDate)
        {
            timestamp = timestamp.AddDays(1);
        }

        var source = ExtractActorName(match.Groups["source"].Value);
        var target = ExtractActorName(match.Groups["target"].Value);
        var sourceIsPlayer = IsPlayerActor(match.Groups["source"].Value);
        var targetIsPlayer = IsPlayerActor(match.Groups["target"].Value);
        var abilityName = ExtractName(match.Groups["ability"].Value);
        var effectRaw = match.Groups["effect"].Value;
        var amountRaw = match.Groups["amount"].Value;

        var kind = ClassifyEffect(effectRaw, abilityName);
        var (amount, damageType, isCrit) = ParseAmount(amountRaw, line);

        return new CombatEvent(
            timestamp,
            source,
            target,
            sourceIsPlayer,
            targetIsPlayer,
            abilityName,
            kind,
            amount,
            isCrit,
            damageType);
    }

    private static CombatEventKind ClassifyEffect(string effectRaw, string abilityName)
    {
        if (string.IsNullOrEmpty(effectRaw))
        {
            return CombatEventKind.Other;
        }

        var lower = effectRaw.ToLowerInvariant();
        if (lower.Contains("entercombat"))
        {
            return CombatEventKind.EnterCombat;
        }
        if (lower.Contains("exitcombat"))
        {
            return CombatEventKind.ExitCombat;
        }
        // The damage/heal information typically sits after a colon in the effect block
        // ("ApplyEffect {id}: Damage {id}" / "ApplyEffect {id}: Heal {id}").
        if (lower.Contains(": damage") || lower.EndsWith("damage"))
        {
            return CombatEventKind.Damage;
        }
        if (lower.Contains(": heal") || lower.EndsWith("heal"))
        {
            return CombatEventKind.Heal;
        }
        return CombatEventKind.Other;
    }

    private static (long amount, string damageType, bool isCrit) ParseAmount(string amountRaw, string line)
    {
        if (string.IsNullOrEmpty(amountRaw))
        {
            return (0, "", false);
        }

        var match = AmountRegex.Match(amountRaw);
        if (!match.Success)
        {
            return (0, "", false);
        }

        var value = match.Groups["value"].Value.Replace(",", "");
        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount))
        {
            amount = 0;
        }

        var damageType = match.Groups["type"].Success ? match.Groups["type"].Value : "";

        // SWTOR marks crits with '*' immediately after the damage number, e.g.
        // "(5694* ~5696 internal {...})". Detect any digit-followed-by-asterisk
        // pattern in the amount block.
        var isCrit = Regex.IsMatch(amountRaw, @"\d+\*");

        return (Math.Abs(amount), damageType, isCrit);
    }

    private static string ExtractActorName(string raw)
    {
        var name = ExtractName(raw);
        if (name.StartsWith('@'))
        {
            name = name[1..];
        }
        // Strip "#hash" suffix some SWTOR servers append to player names.
        var hashIndex = name.IndexOf('#');
        if (hashIndex > 0)
        {
            name = name[..hashIndex];
        }
        return name;
    }

    private static string ExtractName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "";
        }
        // Names have an optional "{id}" or "{id}:instanceId" suffix — drop it.
        var braceIndex = raw.IndexOf('{');
        if (braceIndex > 0)
        {
            raw = raw[..braceIndex];
        }
        return raw.Trim();
    }

    private static bool IsPlayerActor(string raw)
    {
        return !string.IsNullOrWhiteSpace(raw) && raw.TrimStart().StartsWith('@');
    }
}
