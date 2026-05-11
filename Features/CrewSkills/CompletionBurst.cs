namespace SwtorDailyTool;

/// <summary>
/// A short-lived burst of sparkles played on a Crew Skills mission card the
/// moment its timer hits zero. Self-contained — owns its particle list and
/// renders itself; the caller drives Update() on a tick and Render() in OnPaint.
/// </summary>
public sealed class CompletionBurst
{
    private readonly List<Particle> _particles = [];
    private static readonly Random _rng = new();

    public bool IsAlive => _particles.Count > 0;

    public CompletionBurst(PointF center, int particleCount = 18)
    {
        for (var i = 0; i < particleCount; i++)
        {
            var angle = _rng.NextDouble() * Math.PI * 2;
            var speed = 1.4f + (float)_rng.NextDouble() * 2.6f;
            var color = i % 4 == 0
                ? Color.FromArgb(255, 255, 255)              // white spark
                : Color.FromArgb(242, 184, 75);              // gold spark
            _particles.Add(new Particle
            {
                X = center.X,
                Y = center.Y,
                VX = (float)Math.Cos(angle) * speed,
                VY = (float)Math.Sin(angle) * speed - 1.0f,  // slight upward bias
                Life = 1.0f,
                Color = color,
                Size = 1.5f + (float)_rng.NextDouble() * 2.0f
            });
        }
    }

    public void Update()
    {
        for (var i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.X += p.VX;
            p.Y += p.VY;
            p.VX *= 0.96f;
            p.VY = (p.VY * 0.96f) + 0.18f; // gravity
            p.Life -= 0.022f;
            if (p.Life <= 0)
            {
                _particles.RemoveAt(i);
            }
            else
            {
                _particles[i] = p;
            }
        }
    }

    public void Render(Graphics graphics)
    {
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        foreach (var p in _particles)
        {
            var alpha = (int)(255 * Math.Clamp(p.Life, 0f, 1f));
            using var brush = new SolidBrush(Color.FromArgb(alpha, p.Color));
            var size = p.Size * Math.Clamp(p.Life, 0.2f, 1f);
            graphics.FillEllipse(brush, p.X - size, p.Y - size, size * 2, size * 2);
        }
    }

    private struct Particle
    {
        public float X;
        public float Y;
        public float VX;
        public float VY;
        public float Life;
        public Color Color;
        public float Size;
    }
}
