namespace SwtorDailyTool;

public static class CrewMissionDebugLog
{
    private static readonly object Lock = new();

    public static void Write(string message)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "data", "crew-ocr-last.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var entry = $"[{DateTime.Now:HH:mm:ss}] {message}\n\n";

            lock (Lock)
            {
                // Keep file bounded — last ~64 KB of history is plenty for diagnosing 6+ sends.
                const long maxBytes = 64 * 1024;
                if (File.Exists(path) && new FileInfo(path).Length > maxBytes)
                {
                    var existing = File.ReadAllText(path);
                    var keep = existing.Length > maxBytes / 2
                        ? existing[^((int)(maxBytes / 2))..]
                        : existing;
                    File.WriteAllText(path, "...(truncated)...\n" + keep);
                }
                File.AppendAllText(path, entry);
            }
        }
        catch
        {
            // Debug log is best-effort.
        }
    }
}
