using System.Diagnostics;
using System.Text.RegularExpressions;

namespace SwtorDailyTool;

public sealed record MissionSendCapture(
    string Companion,
    string MissionName,
    TimeSpan Duration,
    string? Yield,
    string? Influence,
    string RawRowText,
    string RawCompanionText);

public sealed class MissionOcrPipeline
{
    private readonly string? _tesseractPath;

    public MissionOcrPipeline()
    {
        _tesseractPath = FindTesseract();
    }

    public bool IsAvailable => _tesseractPath is not null;

    public Task<MissionSendCapture?> CaptureAsync(MissionSendDetectedEventArgs args)
    {
        if (_tesseractPath is null)
        {
            return Task.FromResult<MissionSendCapture?>(null);
        }

        // Copy the rectangles + a snapshot of the screenshot before returning to the caller —
        // the caller disposes args.Screenshot as soon as the event handler returns.
        var rowRect = ClampToBitmap(ToBitmapRect(args.SelectedMissionRowOnScreen, args.ScreenshotOrigin), args.Screenshot);
        var companionRect = ClampToBitmap(ToBitmapRect(args.Layout.CompanionPanelRect, args.ScreenshotOrigin), args.Screenshot);

        Bitmap rowCrop = args.Screenshot.Clone(rowRect, args.Screenshot.PixelFormat);
        Bitmap companionCrop = args.Screenshot.Clone(companionRect, args.Screenshot.PixelFormat);

        return Task.Run(() =>
        {
            try
            {
                var rowText = OcrRegion(rowCrop, psm: 6);
                var companionText = OcrRegion(companionCrop, psm: 6);

                var duration = ParseDuration(rowText);
                var missionName = ParseMissionName(rowText);
                var companion = ParseCompanionName(companionText);
                var yield = ParseYield(rowText);
                var influence = ParseInfluence(rowText);

                WriteDebug(rowText, companionText, missionName, companion, duration);

                // Reject captures that landed on the Mission Complete popup instead of the
                // Missions panel. The completion popup overlays the screen briefly when one
                // mission returns just as the user is sending another — its OCR text is very
                // different from the mission row text, so we skip rather than create a bad timer.
                var combined = (rowText + "\n" + companionText).ToLowerInvariant();
                if (combined.Contains("provided rewards") || combined.Contains("mission complete")
                    || combined.Contains("mission completed") || combined.Contains("influence gain"))
                {
                    return null;
                }

                if (companion is null || missionName is null || duration is null)
                {
                    return null;
                }

                return new MissionSendCapture(
                    companion,
                    missionName,
                    duration.Value,
                    yield,
                    influence,
                    rowText,
                    companionText);
            }
            finally
            {
                rowCrop.Dispose();
                companionCrop.Dispose();
            }
        });
    }

    public Task<IReadOnlyList<MissionSendCapture>> CaptureActiveCrewMissionsAsync(
        Bitmap screenshot,
        Point screenshotOrigin,
        MissionsPanelLayout? missionsPanel)
    {
        if (_tesseractPath is null)
        {
            return Task.FromResult<IReadOnlyList<MissionSendCapture>>([]);
        }

        var localMissionPanel = missionsPanel is null
            ? null
            : TranslateLayout(missionsPanel, new Point(-screenshotOrigin.X, -screenshotOrigin.Y));
        var layout = MissionsPanelLocator.LocateCrewSkillsPanel(screenshot, localMissionPanel);
        if (layout is null)
        {
            CrewMissionDebugLog.Write("ACTIVE CREW SCAN — Crew Skills panel was not detected.");
            return Task.FromResult<IReadOnlyList<MissionSendCapture>>([]);
        }

        return Task.Run<IReadOnlyList<MissionSendCapture>>(() =>
        {
            var captures = new List<MissionSendCapture>();
            var debug = new System.Text.StringBuilder();
            var rowHeight = Math.Clamp((int)(layout.DialogRect.Height * 0.17), 82, 120);
            var rowStep = Math.Max(72, rowHeight - 8);

            for (var y = layout.ActiveMissionListRect.Top; y < layout.ActiveMissionListRect.Bottom - 40; y += rowStep)
            {
                var rowRect = Rectangle.FromLTRB(
                    layout.ActiveMissionListRect.Left + (int)(layout.ActiveMissionListRect.Width * 0.13),
                    y,
                    layout.ActiveMissionListRect.Right - (int)(layout.ActiveMissionListRect.Width * 0.12),
                    Math.Min(y + rowHeight, layout.ActiveMissionListRect.Bottom));
                rowRect = ClampToBitmap(rowRect, screenshot);

                using var rowCrop = screenshot.Clone(rowRect, screenshot.PixelFormat);
                var rowText = OcrRegion(rowCrop, psm: 6);
                if (string.IsNullOrWhiteSpace(rowText))
                {
                    continue;
                }

                debug.AppendLine($"  --- active row y={y} ---");
                debug.AppendLine(IndentLines(rowText.TrimEnd()));

                var capture = ParseActiveCrewMissionRow(rowText);
                if (capture is not null)
                {
                    captures.Add(capture);
                }
            }

            var distinct = captures
                .GroupBy(capture => capture.Companion, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();

            CrewMissionDebugLog.Write(
                $"ACTIVE CREW SCAN — found {distinct.Count} visible mission row(s).\n"
                + debug.ToString().TrimEnd());
            return distinct;
        });
    }

    private string OcrRegion(Bitmap source, int psm)
    {
        if (_tesseractPath is null)
        {
            return "";
        }

        using var prepared = PrepareForOcr(source);
        var imagePath = Path.Combine(Path.GetTempPath(), $"swtor-crew-ocr-{Guid.NewGuid():N}.png");
        prepared.Save(imagePath, System.Drawing.Imaging.ImageFormat.Png);

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = _tesseractPath,
                Arguments = $"\"{imagePath}\" stdout --psm {psm}",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var process = Process.Start(psi);
            if (process is null)
            {
                return "";
            }
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(8000);
            return output;
        }
        finally
        {
            try { File.Delete(imagePath); } catch { /* best-effort cleanup */ }
        }
    }

    private static Bitmap PrepareForOcr(Bitmap source)
    {
        const int scale = 2;
        // SWTOR is light text on a dark cyan/blue background. Tesseract works best with
        // black text on white, so we upscale, convert to grayscale, threshold, and invert.
        var scaled = new Bitmap(source.Width * scale, source.Height * scale);
        using (var graphics = Graphics.FromImage(scaled))
        {
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(source, 0, 0, scaled.Width, scaled.Height);
        }

        var output = new Bitmap(scaled.Width, scaled.Height);
        for (var y = 0; y < scaled.Height; y++)
        {
            for (var x = 0; x < scaled.Width; x++)
            {
                var pixel = scaled.GetPixel(x, y);
                // Approximate luminance.
                var lum = (int)(0.299 * pixel.R + 0.587 * pixel.G + 0.114 * pixel.B);
                // Threshold ~140 separates SWTOR's bright text from the dim cyan/blue panel.
                // Invert so text comes out black.
                var v = lum >= 140 ? 0 : 255;
                output.SetPixel(x, y, Color.FromArgb(v, v, v));
            }
        }

        scaled.Dispose();
        return output;
    }

    public static TimeSpan? ParseDuration(string ocrText)
    {
        if (string.IsNullOrWhiteSpace(ocrText))
        {
            return null;
        }

        // Tesseract often confuses certain letters for digits adjacent to the m/s markers
        // (e.g. "6m 29s" → "Gm 29s", "8m" → "Bm", "0m" → "Om"). Run two passes: strict
        // digit pattern first, then a relaxed pass that allows confusable letters.
        var cleaned = Regex.Replace(ocrText, @"[Oo]", "0");
        cleaned = Regex.Replace(cleaned, @"\brn\b", "m");

        var direct = TryParseDuration(cleaned);
        if (direct is not null)
        {
            return direct;
        }

        var normalized = NormalizeDigitConfusables(cleaned);
        return TryParseDuration(normalized);
    }

    private static TimeSpan? TryParseDuration(string cleaned)
    {
        var hms = Regex.Match(cleaned, @"(\d+)\s*h\s*(\d+)\s*m(?:\s*(\d+)\s*s)?", RegexOptions.IgnoreCase);
        if (hms.Success)
        {
            var h = int.Parse(hms.Groups[1].Value);
            var m = int.Parse(hms.Groups[2].Value);
            var s = hms.Groups[3].Success ? int.Parse(hms.Groups[3].Value) : 0;
            if (h < 24 && m < 60 && s < 60)
            {
                return new TimeSpan(h, m, s);
            }
        }

        var ms = Regex.Match(cleaned, @"(\d+)\s*m\s*(\d+)\s*s", RegexOptions.IgnoreCase);
        if (ms.Success)
        {
            var minutes = int.Parse(ms.Groups[1].Value);
            var secondsRaw = ms.Groups[2].Value;
            var seconds = int.Parse(secondsRaw);

            // OCR sometimes glues a stray digit onto the seconds (e.g., "9m 51s" → "9m 951s").
            // Seconds must be < 60; if not, drop the leading digit(s) and retake the last two.
            if (seconds >= 60 && secondsRaw.Length >= 2)
            {
                seconds = int.Parse(secondsRaw[^2..]);
            }

            if (seconds < 60 && minutes < 90)
            {
                return new TimeSpan(0, minutes, seconds);
            }
        }

        var mOnly = Regex.Match(cleaned, @"(\d+)\s*m\b", RegexOptions.IgnoreCase);
        if (mOnly.Success)
        {
            var minutes = int.Parse(mOnly.Groups[1].Value);
            if (minutes < 90)
            {
                return TimeSpan.FromMinutes(minutes);
            }
        }

        return null;
    }

    /// <summary>
    /// Replace common Tesseract letter→digit confusions in tokens that sit next to time markers.
    /// Only applied as a fallback after a strict-digit parse fails — keeps false positives low.
    /// </summary>
    private static string NormalizeDigitConfusables(string text)
    {
        // Pattern: any "<token>m <token>s" or "<token>h <token>m" where tokens may contain
        // digits or known confusable letters. Replace inside those tokens.
        return Regex.Replace(
            text,
            @"([\dGSBoOIlZEAgqTbi]+)(\s*[hms])",
            match => DigitsOnly(match.Groups[1].Value) + match.Groups[2].Value,
            RegexOptions.IgnoreCase);

        static string DigitsOnly(string token)
        {
            var sb = new System.Text.StringBuilder(token.Length);
            foreach (var c in token)
            {
                sb.Append(c switch
                {
                    'O' or 'o' or 'D' => '0',
                    'I' or 'l' or '|' or 'i' => '1',
                    'Z' => '2',
                    'E' => '3',
                    'A' => '4',
                    'S' or 's' => '5',
                    'G' or 'b' => '6',
                    'T' => '7',
                    'B' => '8',
                    'g' or 'q' => '9',
                    _ => c
                });
            }
            return sb.ToString();
        }
    }

    private static readonly string[] MissionMetadataKeywords =
    [
        "influence", "yield", "cost", "rich", "bountiful", "abundant",
        "moderate", "dwindling", "biochemical", "compounds", "samples",
        "credits", "credit", "send companion", "missions"
    ];

    public static string? ParseMissionName(string ocrText)
    {
        if (string.IsNullOrWhiteSpace(ocrText))
        {
            return null;
        }

        var lines = ocrText.Split('\n');

        // Strong signal: SWTOR puts the duration on the same line as the mission title.
        // If a line matches "<title> 3m 27s Cost: ..." use that — it's almost always right.
        // Try strict digit pattern first, then fall back to the OCR-confusable pattern
        // (e.g. "Gm 29s", "8m 4Bs") so titles aren't lost to misread digits.
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (!Regex.IsMatch(line, @"\d+\s*[hm]\s*\d+\s*[ms]", RegexOptions.IgnoreCase))
            {
                continue;
            }

            var title = Regex.Replace(line, @"\s+\d+\s*[hm]\s*\d+.*$", "", RegexOptions.IgnoreCase).Trim();
            title = Regex.Replace(title, @"\s+cost\s*:.*$", "", RegexOptions.IgnoreCase).Trim();
            title = title.TrimEnd('.', ',', ';', ':', '-', ' ');
            if (title.Length >= 4 && Regex.IsMatch(title, "[A-Za-z]{3,}"))
            {
                return CleanMissionTitle(title);
            }
        }

        // Relaxed pass: try lines with "<digit-or-confusable>m <digit-or-confusable>s".
        // Case-sensitive — only specific letters look like digits in OCR. Without this,
        // common lowercase letters like 'e' and 'a' get treated as digit confusions and
        // create false matches inside description text (e.g. "EMIOLE S" in "PEMIOLE SUIPVe").
        const string ConfusableSet = @"[0-9GSBOIZEATolsbgqi]";
        var relaxedDuration = new Regex(ConfusableSet + @"+\s*[hHmM]\s*" + ConfusableSet + @"+\s*[mMsS]");
        var relaxedStrip = new Regex(@"\s+" + ConfusableSet + @"+\s*[hHmM]\s*" + ConfusableSet + @"+.*$");

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (!relaxedDuration.IsMatch(line))
            {
                continue;
            }

            var title = relaxedStrip.Replace(line, "").Trim();
            // Only accept if the strip actually removed the duration block — if the line
            // is unchanged, the relaxed regex matched somewhere mid-word inside a description.
            if (title.Length >= line.Length - 3)
            {
                continue;
            }

            title = Regex.Replace(title, @"\s+cost\s*:.*$", "", RegexOptions.IgnoreCase).Trim();
            title = title.TrimEnd('.', ',', ';', ':', '-', ' ');
            if (title.Length >= 4 && Regex.IsMatch(title, "[A-Za-z]{3,}"))
            {
                return CleanMissionTitle(title);
            }
        }

        // Fallback: pick the first line that doesn't look like metadata.
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length < 4)
            {
                continue;
            }

            var trimmed = Regex.Replace(line, @"\s+\d+\s*m\b.*$", "", RegexOptions.IgnoreCase).Trim();
            trimmed = Regex.Replace(trimmed, @"\s+cost\s*:.*$", "", RegexOptions.IgnoreCase).Trim();
            trimmed = trimmed.TrimEnd('.', ',', ';', ':', '-', ' ');

            var lower = trimmed.ToLowerInvariant();
            if (MissionMetadataKeywords.Any(keyword => lower.Contains(keyword)))
            {
                continue;
            }
            if (lower.Contains("send") && lower.Contains("companion"))
            {
                continue;
            }
            if (lower.Contains("investigate") || lower.Contains("recover"))
            {
                continue;
            }

            if (!Regex.IsMatch(trimmed, "[A-Za-z]{3,}"))
            {
                continue;
            }

            var letterCount = trimmed.Count(char.IsLetter);
            if (letterCount * 2 < trimmed.Length)
            {
                continue;
            }

            return CleanMissionTitle(trimmed);
        }

        return null;
    }

    private static readonly Regex DroidNameRegex =
        new(@"\b[A-Z0-9]{1,4}-[A-Z0-9]{1,4}\b", RegexOptions.Compiled);

    /// <summary>
    /// SWTOR droid names always end with a digit (e.g. "PH4-LNX" actually has trailing "X" but
    /// "2V-R8", "HK-51", "T7-O1" follow patterns where final chars are digits). Correct
    /// well-known Tesseract letter→digit confusions inside droid name tokens (B→8, G→6, etc.)
    /// when they sit between or trail other letters/digits.
    /// </summary>
    private static string CorrectDroidNameDigits(string token)
    {
        // Only normalize chars in trailing positions or where they look like garbled digits.
        // Specifically: "RB" → "R8", "RG" → "R6", "OB" → "08", "1G" → "16", etc.
        var corrected = Regex.Replace(token, @"(?<=[A-Z0-9])[BGZSTAEIO]\b", match => match.Value switch
        {
            "B" => "8",
            "G" => "6",
            "Z" => "2",
            "S" => "5",
            "T" => "7",
            "A" => "4",
            "E" => "3",
            "I" => "1",
            "O" => "0",
            _ => match.Value
        });
        return corrected;
    }

    private static readonly Regex ProperNameRegex =
        new(@"\b[A-Z][a-z]{2,}(?:\s+[A-Z][a-z]+)?\b", RegexOptions.Compiled);

    public static string? ParseCompanionName(string ocrText)
    {
        if (string.IsNullOrWhiteSpace(ocrText))
        {
            return null;
        }

        string? best = null;
        var bestScore = double.MinValue;

        foreach (var raw in ocrText.Split('\n'))
        {
            var line = CleanText(raw);
            if (line.Length < 3)
            {
                continue;
            }

            // Hard reject lines that are clearly buff-text descriptions. These phrases
            // never appear inside a companion name.
            var lower = line.ToLowerInvariant();
            if (lower.Contains("efficiency") || lower.Contains("critical rate")
                || lower.Contains("for crew") || lower.Contains("skill tasks")
                || lower.Contains("influence:") || lower.Contains("yield")
                || lower.Contains("companion gifts"))
            {
                continue;
            }

            // Pull droid-style tokens like "PH4-LNX", "2V-R8", "HK-51".
            foreach (Match m in DroidNameRegex.Matches(line))
            {
                var token = CorrectDroidNameDigits(m.Value);
                if (token.Length < 3 || token.Length > 12)
                {
                    continue;
                }
                // Score droid names highly — they're an unambiguous signal.
                var score = 100.0 - Math.Abs(token.Length - 7);
                if (score > bestScore)
                {
                    best = token;
                    bestScore = score;
                }
            }

            // Dashless droid fallback: Tesseract often drops the hyphen and prefixes a stray
            // letter ("2V-R8" → "e2VR8"). Find any 4-7 char uppercase+digit run with at least
            // one of each and treat it as a likely droid name (lower score than dashed match).
            foreach (Match m in Regex.Matches(line, @"[A-Z0-9]{4,7}"))
            {
                var token = CorrectDroidNameDigits(m.Value);
                if (!token.Any(char.IsLetter) || !token.Any(char.IsDigit))
                {
                    continue;
                }
                var score = 65.0 - Math.Abs(token.Length - 6);
                if (score > bestScore)
                {
                    best = token;
                    bestScore = score;
                }
            }

            // Pull proper-name tokens like "Mako", "Torian Cadera".
            foreach (Match m in ProperNameRegex.Matches(line))
            {
                var token = m.Value.Trim();
                if (token.Length < 3 || token.Length > 24)
                {
                    continue;
                }
                var lowerToken = token.ToLowerInvariant();
                // Skip stop-words that may capitalize at sentence start in OCR fragments.
                if (lowerToken is "send" or "send your" or "missions" or "mission"
                    or "grade" or "rich" or "bountiful" or "abundant" or "moderate"
                    or "dwindling" or "yield" or "cost" or "influence" or "lvl" or "level"
                    or "time" or "critical" or "rank" or "companion" or "gifts")
                {
                    continue;
                }

                // Score proper names lower than droid names but still strongly.
                var score = 80.0 - Math.Abs(token.Length - 7);
                if (score > bestScore)
                {
                    best = token;
                    bestScore = score;
                }
            }
        }

        return best;
    }

    public static string? ParseYield(string ocrText)
    {
        if (string.IsNullOrWhiteSpace(ocrText))
        {
            return null;
        }

        // Examples: "Rich Yield: Grade 2 Biochemical Samples",
        //           "Bountiful Yield: Grade 2 Biochemical Compounds".
        // OCR sometimes splits "Biochemical\nCompounds" — collapse multi-line yield blocks.
        var collapsed = Regex.Replace(ocrText, @"\r?\n", " ");
        var match = Regex.Match(
            collapsed,
            @"(Rich|Bountiful|Abundant|Moderate|Dwindling)\s+Yield\s*:\s*([^\n\r]+?)(?=\s+(?:Influence|Cost|Grade\s+\d+\s*:)|$)",
            RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            var gifts = Regex.Match(collapsed, @"Rank\s+\d+\s+Companion\s+Gifts", RegexOptions.IgnoreCase);
            if (gifts.Success)
            {
                return CleanText(gifts.Value);
            }

            // Some grades show "Grade 2: First Aid Kit" instead of a yield line.
            var grade = Regex.Match(collapsed, @"Grade\s+\d+\s*:\s*([^\n\r]+?)(?=\s+(?:Influence|Cost)|$)", RegexOptions.IgnoreCase);
            if (grade.Success)
            {
                return CleanText(grade.Value);
            }
            return null;
        }

        return CleanText(match.Value);
    }

    public static string? ParseInfluence(string ocrText)
    {
        if (string.IsNullOrWhiteSpace(ocrText))
        {
            return null;
        }

        // "Influence: +33", with OCR sometimes mangling the number prefix.
        var match = Regex.Match(ocrText, @"Influence\s*:\s*([+\-]?\s*\d+)", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return null;
        }

        var value = Regex.Replace(match.Groups[1].Value, @"\s+", "");
        if (!value.StartsWith("+") && !value.StartsWith("-"))
        {
            value = "+" + value;
        }
        return $"Influence: {value}";
    }

    private static string CleanText(string value)
    {
        return Regex.Replace(value, @"\s+", " ").Trim();
    }

    private static string CleanMissionTitle(string value)
    {
        var cleaned = CleanText(value);

        // OCR often prefixes selected SWTOR rows with tiny icon fragments or bullet noise:
        // "%& Quiet, Please", "|| |e A Line in the Sand". Strip that junk without removing
        // real mission titles like "2V-R8" companion names elsewhere.
        cleaned = Regex.Replace(cleaned, @"^[^A-Za-z0-9]+", "").Trim();
        cleaned = Regex.Replace(cleaned, @"^[a-z]\s+(?=[A-Z])", "", RegexOptions.CultureInvariant).Trim();
        cleaned = Regex.Replace(cleaned, @"^[a-z]\s+(?=(?:A|An|The|In|On|To|No)\b)", "", RegexOptions.CultureInvariant).Trim();
        cleaned = Regex.Replace(cleaned, @"^\d+\)?\s+(?=[A-Za-z])", "").Trim();
        cleaned = cleaned.TrimEnd('.', ',', ';', ':', '-', ' ');
        return cleaned;
    }

    private static void WriteDebug(string rowText, string companionText, string? missionName, string? companion, TimeSpan? duration)
    {
        var summary = $"OCR ATTEMPT — companion={companion ?? "(null)"}  mission={missionName ?? "(null)"}  duration={(duration?.ToString() ?? "(null)")}\n"
                    + "  --- row OCR ---\n" + IndentLines(rowText.TrimEnd())
                    + "\n  --- companion OCR ---\n" + IndentLines(companionText.TrimEnd());
        CrewMissionDebugLog.Write(summary);
    }

    private static string IndentLines(string text)
    {
        return string.Join('\n', text.Split('\n').Select(line => "    " + line));
    }

    private static Rectangle ClampToBitmap(Rectangle rect, Bitmap bitmap)
    {
        var x = Math.Clamp(rect.X, 0, bitmap.Width - 1);
        var y = Math.Clamp(rect.Y, 0, bitmap.Height - 1);
        var right = Math.Clamp(rect.Right, x + 1, bitmap.Width);
        var bottom = Math.Clamp(rect.Bottom, y + 1, bitmap.Height);
        return Rectangle.FromLTRB(x, y, right, bottom);
    }

    private static Rectangle ToBitmapRect(Rectangle screenRect, Point screenshotOrigin)
    {
        return new Rectangle(
            screenRect.X - screenshotOrigin.X,
            screenRect.Y - screenshotOrigin.Y,
            screenRect.Width,
            screenRect.Height);
    }

    private static MissionsPanelLayout TranslateLayout(MissionsPanelLayout layout, Point offset)
    {
        return new MissionsPanelLayout(
            Offset(layout.DialogRect, offset),
            Offset(layout.SendCompanionRect, offset),
            Offset(layout.MissionListRect, offset),
            Offset(layout.CompanionPanelRect, offset));
    }

    private static Rectangle Offset(Rectangle rect, Point offset)
    {
        return new Rectangle(rect.X + offset.X, rect.Y + offset.Y, rect.Width, rect.Height);
    }

    private static MissionSendCapture? ParseActiveCrewMissionRow(string ocrText)
    {
        if (string.IsNullOrWhiteSpace(ocrText))
        {
            return null;
        }

        var lines = ocrText
            .Split('\n')
            .Select(line => CleanText(line))
            .Where(line => line.Length > 0)
            .ToList();

        var durationLineIndex = -1;
        TimeSpan? duration = null;
        for (var i = 0; i < lines.Count; i++)
        {
            duration = ParseDuration(lines[i]);
            if (duration is not null)
            {
                durationLineIndex = i;
                break;
            }
        }

        if (duration is null || durationLineIndex < 0)
        {
            return null;
        }

        var companion = FindActiveCompanion(lines, durationLineIndex);
        var mission = FindActiveMission(lines, durationLineIndex);
        if (companion is null || mission is null)
        {
            return null;
        }

        return new MissionSendCapture(
            companion,
            mission,
            duration.Value,
            null,
            null,
            string.Join('\n', lines),
            companion);
    }

    private static string? FindActiveCompanion(IReadOnlyList<string> lines, int durationLineIndex)
    {
        for (var i = durationLineIndex - 1; i >= 0; i--)
        {
            var candidate = CleanActiveCrewLine(lines[i]);
            if (candidate is null)
            {
                continue;
            }

            var parsed = ParseCompanionName(candidate);
            if (parsed is not null)
            {
                return parsed;
            }
        }

        return null;
    }

    private static string? FindActiveMission(IReadOnlyList<string> lines, int durationLineIndex)
    {
        for (var i = durationLineIndex + 1; i <= Math.Min(lines.Count - 1, durationLineIndex + 3); i++)
        {
            var candidate = CleanActiveMissionLine(lines[i]);
            if (candidate is null)
            {
                continue;
            }

            var lower = candidate.ToLowerInvariant();
            if (lower.Contains("time remaining") || lower.Contains("influence") || lower.Contains("yield"))
            {
                continue;
            }

            if (candidate.Length >= 4 && Regex.IsMatch(candidate, "[A-Za-z]{3,}"))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? CleanActiveCrewLine(string line)
    {
        var cleaned = Regex.Replace(line, @"^[^A-Za-z0-9+:-]+", "").Trim();
        cleaned = Regex.Replace(cleaned, @"\s+", " ");
        if (cleaned.Length < 3)
        {
            return null;
        }

        var lower = cleaned.ToLowerInvariant();
        if (lower.Contains("crew skills") || lower is "biochem" or "bioanalysis" or "diplomacy")
        {
            return null;
        }

        return cleaned;
    }

    private static string? CleanActiveMissionLine(string line)
    {
        var cleaned = CleanActiveCrewLine(line);
        if (cleaned is null)
        {
            return null;
        }

        cleaned = CleanMissionTitle(cleaned);
        return cleaned.Length >= 3 ? cleaned : null;
    }

    private static string? FindTesseract()
    {
        var candidates = new[]
        {
            "tesseract.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Tesseract-OCR", "tesseract.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Tesseract-OCR", "tesseract.exe")
        };

        foreach (var candidate in candidates)
        {
            if (candidate.Contains(Path.DirectorySeparatorChar) && File.Exists(candidate))
            {
                return candidate;
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "where.exe",
                    Arguments = candidate,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                };
                using var process = Process.Start(psi);
                var result = process?.StandardOutput.ReadLine();
                process?.WaitForExit(1000);
                if (!string.IsNullOrWhiteSpace(result) && File.Exists(result))
                {
                    return result;
                }
            }
            catch
            {
                // Try the next candidate.
            }
        }

        return null;
    }
}
