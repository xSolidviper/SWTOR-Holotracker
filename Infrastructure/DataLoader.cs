using System.Text.Json;

namespace SwtorDailyTool;

public static class DataLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static DailyToolData LoadDailyData(string baseDirectory)
    {
        var path = Path.Combine(baseDirectory, "data", "dailies.json");
        if (!File.Exists(path))
        {
            return new DailyToolData
            {
                Title = "SWTOR Daily Planner",
                Subtitle = "Missing data/dailies.json",
                AccuracyNote = "Create or restore data/dailies.json beside the executable."
            };
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<DailyToolData>(json, Options) ?? new DailyToolData();
    }

    public static ThemeData LoadTheme(string baseDirectory)
    {
        var path = Path.Combine(baseDirectory, "data", "theme.json");
        if (!File.Exists(path))
        {
            return new ThemeData();
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<ThemeData>(json, Options) ?? new ThemeData();
    }

    public static RedeemCodeData LoadRedeemCodes(string baseDirectory)
    {
        var path = Path.Combine(baseDirectory, "data", "redeem-codes.json");
        if (!File.Exists(path))
        {
            return new RedeemCodeData
            {
                Description = "Missing data/redeem-codes.json."
            };
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<RedeemCodeData>(json, Options) ?? new RedeemCodeData();
    }

    public static DatacronGuideData LoadDatacrons(string baseDirectory)
    {
        var path = Path.Combine(baseDirectory, "data", "datacrons.json");
        if (!File.Exists(path))
        {
            return new DatacronGuideData
            {
                Description = "Missing data/datacrons.json."
            };
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<DatacronGuideData>(json, Options) ?? new DatacronGuideData();
    }
}
