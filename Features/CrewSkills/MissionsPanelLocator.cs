namespace SwtorDailyTool;

public sealed record MissionsPanelLayout(
    Rectangle DialogRect,
    Rectangle SendCompanionRect,
    Rectangle MissionListRect,
    Rectangle CompanionPanelRect);

public static class MissionsPanelLocator
{
    public static MissionsPanelLayout? Locate(Bitmap screenshot)
    {
        var dialogs = DetectCyanDialogs(screenshot);
        // The Missions panel is significantly taller than wide (aspect ~1.4). The Crew
        // Skills panel sits to its left and is closer to square (~1.25). When their cyan
        // borders touch, flood-fill merges them into a near-square (~1.12) blob — reject
        // anything below 1.20 to avoid that case. Among valid candidates, prefer rightmost
        // because the Missions panel is always to the right of Crew Skills.
        // Order by aspect ratio (tallest-narrowest wins). The Missions panel is consistently
        // taller and narrower (aspect ~1.45) than the Crew Skills panel (~1.30), so this
        // disambiguates them when both are visible. Rightmost preference doesn't work
        // reliably across multi-monitor setups where panels sit on different screens.
        var dialog = dialogs
            .Where(rect => rect.Height >= rect.Width * 1.20 && rect.Width >= 280 && rect.Height >= 380)
            .OrderByDescending(rect => (double)rect.Height / rect.Width)
            .ThenByDescending(rect => rect.Width * rect.Height)
            .FirstOrDefault();

        if (dialog.IsEmpty)
        {
            return null;
        }

        var sendRect = ApproximateSendCompanionRect(dialog);
        var companionRect = ApproximateCompanionPanelRect(dialog);
        var listRect = ApproximateMissionListRect(dialog, companionRect);
        return new MissionsPanelLayout(dialog, sendRect, listRect, companionRect);
    }

    public static List<Rectangle> DetectCyanDialogs(Bitmap screenshot)
    {
        const int stride = 4;
        var visited = new bool[(screenshot.Width / stride + 2), (screenshot.Height / stride + 2)];
        var candidates = new List<Rectangle>();

        for (var y = 0; y < screenshot.Height; y += stride)
        {
            for (var x = 0; x < screenshot.Width; x += stride)
            {
                var gx = x / stride;
                var gy = y / stride;
                if (visited[gx, gy] || !IsSwtorCyan(screenshot.GetPixel(x, y)))
                {
                    continue;
                }

                var component = FloodCyanComponent(screenshot, visited, gx, gy, stride);
                if (component.Width < 200 || component.Height < 200)
                {
                    continue;
                }

                candidates.Add(component);
            }
        }

        return candidates
            .OrderByDescending(rect => rect.Width * rect.Height)
            .Take(4)
            .ToList();
    }

    public static bool IsSwtorCyan(Color color)
    {
        return color.B >= 120
            && color.G >= 95
            && color.R <= 80
            && color.B - color.R >= 70
            && color.G - color.R >= 45;
    }

    private static Rectangle ApproximateSendCompanionRect(Rectangle dialog)
    {
        // Cover the bottom 16% of the dialog. The cyan flood-fill sometimes picks up
        // extra UI area below the visible Missions panel, which pushes the SEND COMPANION
        // button up to ~88% from the dialog top instead of ~94%. A taller rect catches the
        // button in both compact layouts (button near very bottom) and inflated ones.
        var width = (int)(dialog.Width * 0.46);
        var height = (int)(dialog.Height * 0.16);
        var x = dialog.Right - width - (int)(dialog.Width * 0.02);
        var y = dialog.Bottom - height;
        return new Rectangle(x, y, width, height);
    }

    private static Rectangle ApproximateCompanionPanelRect(Rectangle dialog)
    {
        // The cyan flood-fill `dialog.Bottom` is at the bottom of the SEND COMPANION button
        // (which is inside the cyan border). Walking upward inside the dialog: SEND button
        // (~bottom 7%), buffs (~7-15%), companion picker (~15-30%), missions list above.
        // 14-30% from bottom targets the companion picker — confirmed via debug overlay
        // matching exactly on the companion name.
        var topFromBottom = (int)(dialog.Height * 0.30);
        var bottomFromBottom = (int)(dialog.Height * 0.14);
        var x = dialog.X + (int)(dialog.Width * 0.04);
        var width = dialog.Width - (int)(dialog.Width * 0.08);
        return Rectangle.FromLTRB(x, dialog.Bottom - topFromBottom, x + width, dialog.Bottom - bottomFromBottom);
    }

    private static Rectangle ApproximateMissionListRect(Rectangle dialog, Rectangle companionPanel)
    {
        var x = dialog.X + (int)(dialog.Width * 0.04);
        var y = dialog.Y + (int)(dialog.Height * 0.10);
        var width = dialog.Width - (int)(dialog.Width * 0.08);
        var bottom = companionPanel.Y - 4;
        return Rectangle.FromLTRB(x, y, x + width, bottom);
    }

    private static Rectangle FloodCyanComponent(Bitmap screenshot, bool[,] visited, int startX, int startY, int stride)
    {
        var queue = new Queue<Point>();
        queue.Enqueue(new Point(startX, startY));
        visited[startX, startY] = true;

        var minX = startX * stride;
        var minY = startY * stride;
        var maxX = minX;
        var maxY = minY;
        var maxGridX = screenshot.Width / stride;
        var maxGridY = screenshot.Height / stride;

        while (queue.Count > 0)
        {
            var point = queue.Dequeue();
            var pixelX = Math.Min(point.X * stride, screenshot.Width - 1);
            var pixelY = Math.Min(point.Y * stride, screenshot.Height - 1);
            minX = Math.Min(minX, pixelX);
            minY = Math.Min(minY, pixelY);
            maxX = Math.Max(maxX, pixelX);
            maxY = Math.Max(maxY, pixelY);

            foreach (var next in new[]
            {
                new Point(point.X + 1, point.Y),
                new Point(point.X - 1, point.Y),
                new Point(point.X, point.Y + 1),
                new Point(point.X, point.Y - 1)
            })
            {
                if (next.X < 0 || next.Y < 0 || next.X > maxGridX || next.Y > maxGridY || visited[next.X, next.Y])
                {
                    continue;
                }

                var nx = Math.Min(next.X * stride, screenshot.Width - 1);
                var ny = Math.Min(next.Y * stride, screenshot.Height - 1);
                visited[next.X, next.Y] = true;
                if (IsSwtorCyan(screenshot.GetPixel(nx, ny)))
                {
                    queue.Enqueue(next);
                }
            }
        }

        return Rectangle.FromLTRB(minX, minY, maxX + stride, maxY + stride);
    }
}
