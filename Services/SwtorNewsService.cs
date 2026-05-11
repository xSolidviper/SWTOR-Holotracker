using System.Net.Http;
using System.Text.RegularExpressions;
using System.Web;

namespace SwtorDailyTool;

public sealed class PatchNoteEntry
{
    public string Title { get; init; } = "";
    public string Date { get; init; } = "";
    public string Url { get; init; } = "";
}

public sealed class NewsEntry
{
    public string Title { get; init; } = "";
    public string Date { get; init; } = "";
    public string Category { get; init; } = "";
    public string Url { get; init; } = "";
}

public sealed class SwtorNewsService : IDisposable
{
    private static readonly HttpClient Http = CreateHttpClient();
    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        // Some CDNs reject empty User-Agent — set one matching a regular browser.
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36 Holotracker/1.0");
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
        return client;
    }

    private const string Base = "https://www.swtor.com";
    private const int RefreshMinutes = 30;
    private CancellationTokenSource _cts = new();
    private bool _disposed;

    public List<PatchNoteEntry> PatchNotes { get; private set; } = [];
    public List<NewsEntry> News { get; private set; } = [];
    public string? PatchNotesError { get; private set; }
    public string? NewsError { get; private set; }

    public event EventHandler? PatchNotesUpdated;
    public event EventHandler? NewsUpdated;

    // Start background polling — fires immediately then every 30 minutes
    public void StartPolling()
    {
        _cts = new CancellationTokenSource();
        _ = PollLoopAsync(_cts.Token);
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            // Run both fetches in parallel on background threads — no UI blocking
            await Task.WhenAll(
                Task.Run(() => LoadPatchNotesAsync(ct), ct),
                Task.Run(() => LoadNewsAsync(ct), ct));

            // Wait 30 minutes before next check, but wake immediately on cancel
            try { await Task.Delay(TimeSpan.FromMinutes(RefreshMinutes), ct); }
            catch (TaskCanceledException) { break; }
        }
    }

    private async Task LoadPatchNotesAsync(CancellationToken ct)
    {
        PatchNotesError = null;
        try
        {
            var html = await Http.GetStringAsync($"{Base}/patchnotes", ct);
            var fresh = ParsePatchNotes(html).Take(6).ToList();
            // Only fire event if content actually changed
            if (fresh.Count != PatchNotes.Count || (fresh.Count > 0 && fresh[0].Url != PatchNotes.FirstOrDefault()?.Url))
            {
                PatchNotes = fresh;
                PatchNotesUpdated?.Invoke(this, EventArgs.Empty);
            }
            else if (PatchNotes.Count == 0)
            {
                PatchNotes = fresh;
                PatchNotesUpdated?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { PatchNotesError = ex.Message; PatchNotesUpdated?.Invoke(this, EventArgs.Empty); }
    }

    private async Task LoadNewsAsync(CancellationToken ct)
    {
        NewsError = null;
        try
        {
            var html = await Http.GetStringAsync($"{Base}/info/news", ct);
            var fresh = ParseNews(html);
            if (fresh.Count != News.Count || (fresh.Count > 0 && fresh[0].Url != News.FirstOrDefault()?.Url))
            {
                News = fresh;
                NewsUpdated?.Invoke(this, EventArgs.Empty);
            }
            else if (News.Count == 0)
            {
                News = fresh;
                NewsUpdated?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { NewsError = ex.Message; NewsUpdated?.Invoke(this, EventArgs.Empty); }
    }

    public async Task<string> FetchArticleTextAsync(string url)
    {
        var fullUrl = url.StartsWith("http") ? url : $"{Base}{url}";
        var html = await Http.GetStringAsync(fullUrl);
        return ParseArticleText(html);
    }

    private static List<PatchNoteEntry> ParsePatchNotes(string html)
    {
        var results = new List<PatchNoteEntry>();
        var pattern = new Regex(
            @"<a\s+href=""(/patchnotes/[^""]+)"">([^<]+)</a>",
            RegexOptions.IgnoreCase);

        foreach (Match m in pattern.Matches(html))
        {
            var href = m.Groups[1].Value.Trim();
            var text = HttpUtility.HtmlDecode(m.Groups[2].Value.Trim());
            var dashIdx = text.IndexOf(" - ", StringComparison.Ordinal);
            if (dashIdx < 0) continue;
            var date = text[..dashIdx].Trim();
            var title = text[(dashIdx + 3)..].Trim();
            if (title.Length == 0) continue;
            results.Add(new PatchNoteEntry { Title = title, Date = date, Url = href });
        }
        return results;
    }

    private static List<NewsEntry> ParseNews(string html)
    {
        var results = new List<NewsEntry>();
        var itemPattern = new Regex(
            @"<div class=""newsItem[^""]*"">(.*?)</div>\s*</div>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var titlePattern = new Regex(
            @"<h2[^>]*swtorTitle[^>]*>\s*<a\s+href=""([^""]+)"">([^<]+)</a>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var datePattern = new Regex(
            @"<span class=""date"">([^<]+)</span>",
            RegexOptions.IgnoreCase);

        foreach (Match item in itemPattern.Matches(html))
        {
            var block = item.Groups[1].Value;
            var titleMatch = titlePattern.Match(block);
            if (!titleMatch.Success) continue;

            var href = titleMatch.Groups[1].Value.Trim();
            var title = HttpUtility.HtmlDecode(titleMatch.Groups[2].Value.Trim());
            if (title.Length == 0) continue;

            var dates = datePattern.Matches(block);
            var category = dates.Count > 0 ? dates[0].Groups[1].Value.Trim() : "";
            var date = dates.Count > 1 ? dates[1].Groups[1].Value.Trim() : "";

            results.Add(new NewsEntry
            {
                Title = title,
                Date = date,
                Category = category,
                Url = href
            });
        }
        return results;
    }

    private static string ParseArticleText(string html)
    {
        // Target #contentPad (patch notes) or .field-items (news articles)
        var contentPad = new Regex(
            @"<div\s+id=""contentPad""[^>]*>(.*?)</div>\s*</div>\s*</div>\s*</div>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var fieldItems = new Regex(
            @"<div\s+class=""field-items""[^>]*>(.*?)</div>\s*</div>\s*</div>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var newsBlurb = new Regex(
            @"<div\s+id=""mainContent""[^>]*>(.*?)</div>\s*<!--\s*end",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        var match = contentPad.Match(html);
        if (!match.Success) match = fieldItems.Match(html);
        if (!match.Success) match = newsBlurb.Match(html);

        var content = match.Success ? match.Groups[1].Value : html;

        // Strip <script> and <style> blocks first
        content = Regex.Replace(content, @"<script[^>]*>.*?</script>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        content = Regex.Replace(content, @"<style[^>]*>.*?</style>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        // Format structure
        content = Regex.Replace(content, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
        content = Regex.Replace(content, @"<p[^>]*>\s*<strong[^>]*>([^<]+)</strong>\s*</p>", "\n\n■ $1\n", RegexOptions.IgnoreCase);
        content = Regex.Replace(content, @"<p[^>]*>", "\n", RegexOptions.IgnoreCase);
        content = Regex.Replace(content, @"</p>", "", RegexOptions.IgnoreCase);
        content = Regex.Replace(content, @"<h[1-6][^>]*>", "\n\n▶ ", RegexOptions.IgnoreCase);
        content = Regex.Replace(content, @"</h[1-6]>", "\n", RegexOptions.IgnoreCase);
        content = Regex.Replace(content, @"<li[^>]*>", "\n  • ", RegexOptions.IgnoreCase);
        content = Regex.Replace(content, @"</li>", "", RegexOptions.IgnoreCase);
        content = Regex.Replace(content, @"<strong[^>]*>([^<]+)</strong>", "$1", RegexOptions.IgnoreCase);
        content = Regex.Replace(content, @"<[^>]+>", "", RegexOptions.Singleline);
        content = HttpUtility.HtmlDecode(content);
        content = Regex.Replace(content, @"\n{3,}", "\n\n");
        return content.Trim();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts.Cancel();
        _cts.Dispose();
    }
}
