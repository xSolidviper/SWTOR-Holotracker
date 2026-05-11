using System.Net.Http;
using System.Text.RegularExpressions;

namespace SwtorDailyTool;

public sealed class ServerInfo
{
    public string Name { get; init; } = "";
    public string Region { get; init; } = "";
    public bool Online { get; init; }
}

public sealed class SwtorServerStatus : IDisposable
{
    private static readonly HttpClient Http = CreateHttpClient();
    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36 Holotracker/1.0");
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
        return client;
    }

    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 60_000 };
    private bool _disposed;

    public List<ServerInfo> Servers { get; private set; } = [];
    public DateTime LastChecked { get; private set; } = DateTime.MinValue;
    public bool IsLoading { get; private set; } = true;
    public string? Error { get; private set; }

    public event EventHandler? Updated;

    public SwtorServerStatus()
    {
        _timer.Tick += async (_, _) => await RefreshAsync();
    }

    public async Task StartAsync()
    {
        await RefreshAsync();
        _timer.Start();
    }

    private async Task RefreshAsync()
    {
        IsLoading = true;
        Error = null;
        try
        {
            var html = await Http.GetStringAsync("https://www.swtor.com/server-status");
            Servers = ParseServers(html);
            LastChecked = DateTime.UtcNow;
            Error = null;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }

        Updated?.Invoke(this, EventArgs.Empty);
    }

    private static readonly Dictionary<string, string> KnownRegions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Star Forge"]    = "North America",
        ["Satele Shan"]   = "North America",
        ["Tulak Hord"]    = "Europe",
        ["Darth Malgus"]  = "Europe",
        ["The Leviathan"] = "Europe",
        ["Shae Vizla"]    = "Asia Pacific"
    };

    private static List<ServerInfo> ParseServers(string html)
    {
        var servers = new List<ServerInfo>();

        // Match every server row across the whole page using data-status attribute
        var rowPattern = new Regex(
            @"data-status=""([^""]+)""[^>]*>.*?<div\s+class=""name"">([^<]+)</div>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        foreach (Match m in rowPattern.Matches(html))
        {
            var status = m.Groups[1].Value.Trim();
            var name = m.Groups[2].Value.Trim();
            if (name.Length == 0) continue;

            KnownRegions.TryGetValue(name, out var region);
            servers.Add(new ServerInfo
            {
                Name = name,
                Region = region ?? "Other",
                Online = status.Equals("UP", StringComparison.OrdinalIgnoreCase)
            });
        }

        return servers;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _timer.Dispose();
    }
}
