namespace SwtorDailyTool;

public sealed class DailyToolData
{
    public string Title { get; set; } = "SWTOR Daily Planner";
    public string Subtitle { get; set; } = "";
    public string VersionLabel { get; set; } = "";
    public string LastVerified { get; set; } = "";
    public string AccuracyNote { get; set; } = "";
    public List<CategoryData> Categories { get; set; } = [];
    public List<SourceData> Sources { get; set; } = [];
}

public sealed class ProgressData
{
    public List<string> Completed { get; set; } = [];
    public Dictionary<string, int> Counts { get; set; } = [];
}

public sealed class CategoryData
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<DailyRecommendation> Items { get; set; } = [];
}

public sealed class DailyRecommendation
{
    public string Name { get; set; } = "";
    public string Planet { get; set; } = "";
    public string Priority { get; set; } = "Recommended";
    public string Time { get; set; } = "";
    public string Access { get; set; } = "";
    public string Location { get; set; } = "";
    public string QuestStart { get; set; } = "";
    public string LevelRequirement { get; set; } = "";
    public int RepeatLimit { get; set; } = 1;
    public string Why { get; set; } = "";
    public List<string> Objectives { get; set; } = [];
    public List<string> Rewards { get; set; } = [];
    public List<string> Tags { get; set; } = [];
    public List<string> MissionAliases { get; set; } = [];
}

public sealed class SourceData
{
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
}

public sealed class ThemeData
{
    public string Accent { get; set; } = "#40B7FF";
    public string AccentAlt { get; set; } = "#E7B75F";
    public string Background { get; set; } = "#10141B";
    public string Panel { get; set; } = "#171D27";
    public string PanelAlt { get; set; } = "#202938";
    public string Text { get; set; } = "#F4F7FB";
    public string MutedText { get; set; } = "#AAB5C4";
}

public sealed class RedeemCodeData
{
    public string Title { get; set; } = "Redeem Codes";
    public string Description { get; set; } = "";
    public string LastVerified { get; set; } = "";
    public List<RedeemCodeItem> Codes { get; set; } = [];
}

public sealed class RedeemCodeItem
{
    public string Code { get; set; } = "";
    public string ImagePath { get; set; } = "";
    public string Reward { get; set; } = "";
    public string Status { get; set; } = "";
    public string Note { get; set; } = "";
    public string Source { get; set; } = "";
}

public sealed class DatacronGuideData
{
    public string Title { get; set; } = "Datacron Guide";
    public string Description { get; set; } = "";
    public string LastVerified { get; set; } = "";
    public List<DatacronRegionData> Regions { get; set; } = [];
    public List<SourceData> Sources { get; set; } = [];
}

public sealed class DatacronRegionData
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<DatacronPlanetData> Planets { get; set; } = [];
}

public sealed class DatacronPlanetData
{
    public string Name { get; set; } = "";
    public string Faction { get; set; } = "";
    public string RecommendedLevel { get; set; } = "";
    public string Summary { get; set; } = "";
    public string ImagePath { get; set; } = "";
    public bool ImageIncludesPins { get; set; }
    public string MapUrl { get; set; } = "";
    public string SourceUrl { get; set; } = "";
    public List<DatacronLocationData> Datacrons { get; set; } = [];
}

public sealed class DatacronLocationData
{
    public string Name { get; set; } = "";
    public string Reward { get; set; } = "";
    public string Area { get; set; } = "";
    public string Coordinates { get; set; } = "";
    public string Notes { get; set; } = "";
    public string Guide { get; set; } = "";
    public string ImagePath { get; set; } = "";
    public double? PinX { get; set; }
    public double? PinY { get; set; }
}
