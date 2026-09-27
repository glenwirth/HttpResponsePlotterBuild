using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace HttpResponsePlotter;

/// <summary>
/// Time-series line chart of response times. Successful samples are joined by lines;
/// failures break the line and are drawn as an "x" near the top of the plot.
/// Hovering shows the nearest sample of each series.
/// </summary>
internal sealed class ResponseTimeChart : Control
{
    private static readonly int[] TimeStepsSeconds =
        [1, 2, 5, 10, 15, 30, 60, 120, 300, 600, 900, 1800, 3600, 7200, 10800, 21600, 43200, 86400];

    private Point? _mouse;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<SeriesData> Series { get; set; } = [];

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public AppSettings Settings { get; set; } = new();

    public ResponseTimeChart()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        _mouse = e.Location;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _mouse = null;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var s = Settings;
        float k = DeviceDpi / 96f;

        g.Clear(s.ChartBackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        using var textBrush = new SolidBrush(s.ChartForeColor);
        using var gridPen = new Pen(s.GridColor, 1);
        using var axisPen = new Pen(s.ChartForeColor, 1);

        var plot = Rectangle.FromLTRB((int)(72 * k), (int)(40 * k), Width - (int)(24 * k), Height - (int)(52 * k));
        if (plot.Width < 40 || plot.Height < 40) return;

        var visible = Series.Where(x => x.Enabled).ToList();
        DrawLegend(g, visible, plot, k, textBrush);

        // ----- X range -----
        var withData = visible.Where(x => x.Samples.Count > 0).ToList();
        if (withData.Count == 0)
        {
            g.DrawRectangle(axisPen, plot);
            DrawCentered(g, "No data yet. Press Start to begin polling.", plot, textBrush);
            return;
        }

        DateTime xMax = withData.Max(x => x.Samples[^1].Time);
        DateTime xMin = withData.Min(x => x.Samples[0].Time);
        if (s.TimeWindowMinutes > 0)
        {
            var windowStart = xMax - TimeSpan.FromMinutes(s.TimeWindowMinutes);
            if (windowStart > xMin) xMin = windowStart;
        }
        var minSpan = TimeSpan.FromSeconds(Math.Max(10, s.IntervalSeconds * 4));
        if (xMax - xMin < minSpan) xMax = xMin + minSpan;
        double spanMs = (xMax - xMin).TotalMilliseconds;

        // ----- Y range -----
        double dataMax = 0;
        foreach (var series in visible)
            foreach (var p in series.Samples)
                if (p.Time >= xMin && p.IsSuccess && p.ElapsedMs > dataMax) dataMax = p.ElapsedMs!.Value;

        bool autoY = s.YAxisMaxMs <= 0;
        double yMax = autoY ? Math.Max(dataMax * 1.1, 1) : s.YAxisMaxMs;
        if (autoY && s.WarningThresholdMs > 0) yMax = Math.Max(yMax, s.WarningThresholdMs * 1.1);
        double yStep = NiceStep(yMax / Math.Max(2, plot.Height / (45 * k)));
        if (autoY) yMax = Math.Ceiling(yMax / yStep) * yStep;

        float X(DateTime t) => plot.Left + (float)((t - xMin).TotalMilliseconds / spanMs * plot.Width);
        float Y(double v) => plot.Bottom - (float)(Math.Min(v, yMax * 1.5) / yMax * plot.Height);

        // ----- Grid and axis labels -----
        using var labelFormatRight = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
        using var labelFormatTop = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near };

        string yFmt = yStep < 1 ? "0.###" : "0";
        for (double v = 0; v <= yMax + yStep * 1e-6; v += yStep)
        {
            float y = Y(v);
            g.DrawLine(gridPen, plot.Left, y, plot.Right, y);
            g.DrawString(v.ToString(yFmt), Font, textBrush, new RectangleF(0, y - 10 * k, plot.Left - 6 * k, 20 * k), labelFormatRight);
        }

        int maxTicks = Math.Max(2, (int)(plot.Width / (90 * k)));
        int stepSec = TimeStepsSeconds.FirstOrDefault(st => spanMs / 1000 / st <= maxTicks, TimeStepsSeconds[^1]);
        long stepTicks = TimeSpan.FromSeconds(stepSec).Ticks;
        string xFmt = spanMs > TimeSpan.FromDays(1).TotalMilliseconds ? "MM-dd HH:mm" : stepSec >= 60 ? "HH:mm" : "HH:mm:ss";
        for (long ticks = (xMin.Ticks + stepTicks - 1) / stepTicks * stepTicks; ticks <= xMax.Ticks; ticks += stepTicks)
        {
            var t = new DateTime(ticks, xMin.Kind);
            float x = X(t);
            g.DrawLine(gridPen, x, plot.Top, x, plot.Bottom);
            g.DrawString(t.ToString(xFmt), Font, textBrush, new RectangleF(x - 50 * k, plot.Bottom + 4 * k, 100 * k, 20 * k), labelFormatTop);
        }

        g.DrawRectangle(axisPen, plot);

        // Axis titles
        g.DrawString("Time", Font, textBrush,
            new RectangleF(plot.Left, plot.Bottom + 24 * k, plot.Width, 20 * k), labelFormatTop);
        var state = g.Save();
        g.TranslateTransform(4 * k, plot.Top + plot.Height / 2f);
        g.RotateTransform(-90);
        g.DrawString("Response time (ms)", Font, textBrush,
            new RectangleF(-plot.Height / 2f, 0, plot.Height, 20 * k), labelFormatTop);
        g.Restore(state);

        // ----- Threshold -----
        if (s.WarningThresholdMs > 0 && s.WarningThresholdMs <= yMax)
        {
            using var thresholdPen = new Pen(s.ThresholdColor, 1.5f * k) { DashStyle = DashStyle.Dash };
            float y = Y(s.WarningThresholdMs);
            g.DrawLine(thresholdPen, plot.Left, y, plot.Right, y);
            using var thBrush = new SolidBrush(s.ThresholdColor);
            g.DrawString($"{s.WarningThresholdMs:0.#} ms", Font, thBrush, plot.Left + 4 * k, y - Font.Height - 1);
        }

        // ----- Series -----
        g.SetClip(plot);
        var cutoff = xMin - TimeSpan.FromSeconds(s.IntervalSeconds * 2); // lets the line enter from the left edge
        for (int si = 0; si < visible.Count; si++)
        {
            var series = visible[si];
            using var pen = new Pen(series.Color, s.LineWidth * k) { LineJoin = LineJoin.Round };
            using var brush = new SolidBrush(series.Color);
            using var failPen = new Pen(series.Color, 2 * k);
            using var failLinePen = new Pen(Color.FromArgb(50, series.Color), 1);
            float markerR = (s.LineWidth + 1.5f) * k;
            float failY = plot.Top + (10 + si * 12) * k;

            var run = new List<PointF>();
            void Flush()
            {
                if (run.Count >= 2) g.DrawLines(pen, run.ToArray());
                else if (run.Count == 1) g.FillEllipse(brush, run[0].X - markerR, run[0].Y - markerR, markerR * 2, markerR * 2);
                if (s.ShowMarkers && run.Count >= 2)
                    foreach (var p in run) g.FillEllipse(brush, p.X - markerR, p.Y - markerR, markerR * 2, markerR * 2);
                run.Clear();
            }

            foreach (var p in series.Samples)
            {
                if (p.Time < cutoff) continue;
                float x = X(p.Time);
                if (p.IsSuccess)
                {
                    run.Add(new PointF(x, Y(p.ElapsedMs!.Value)));
                }
                else
                {
                    Flush();
                    g.DrawLine(failLinePen, x, plot.Top, x, plot.Bottom);
                    float r = 4 * k;
                    g.DrawLine(failPen, x - r, failY - r, x + r, failY + r);
                    g.DrawLine(failPen, x - r, failY + r, x + r, failY - r);
                }
            }
            Flush();
        }
        g.ResetClip();

        // ----- Hover tooltip -----
        if (_mouse is { } m && plot.Contains(m))
            DrawHover(g, visible, plot, m, k, X, Y, xMin, spanMs);
    }

    private void DrawHover(Graphics g, List<SeriesData> visible, Rectangle plot, Point m, float k,
        Func<DateTime, float> X, Func<double, float> Y, DateTime xMin, double spanMs)
    {
        var mouseTime = xMin + TimeSpan.FromMilliseconds((m.X - plot.Left) / (double)plot.Width * spanMs);
        var hits = new List<(SeriesData Series, Sample Sample)>();
        foreach (var series in visible)
        {
            Sample? best = null;
            double bestDiff = double.MaxValue;
            foreach (var p in series.Samples)
            {
                double diff = Math.Abs((p.Time - mouseTime).TotalMilliseconds);
                if (diff < bestDiff) { bestDiff = diff; best = p; }
            }
            if (best is not null && Math.Abs(X(best.Time) - m.X) <= 25 * k) hits.Add((series, best));
        }
        if (hits.Count == 0) return;

        using var crossPen = new Pen(Color.FromArgb(120, Settings.ChartForeColor), 1) { DashStyle = DashStyle.Dot };
        float cx = X(hits[0].Sample.Time);
        g.DrawLine(crossPen, cx, plot.Top, cx, plot.Bottom);

        var lines = new List<(string Text, Color Color)> { (hits[0].Sample.Time.ToString("yyyy-MM-dd HH:mm:ss.fff"), Settings.ChartForeColor) };
        foreach (var (series, p) in hits)
        {
            if (p.IsSuccess)
            {
                float r = (Settings.LineWidth + 3.5f) * k;
                using var ring = new Pen(series.Color, 2 * k);
                using var fill = new SolidBrush(Settings.ChartBackColor);
                var py = Y(p.ElapsedMs!.Value);
                var px = X(p.Time);
                g.FillEllipse(fill, px - r, py - r, r * 2, r * 2);
                g.DrawEllipse(ring, px - r, py - r, r * 2, r * 2);
                lines.Add(($"{series.Name}: {p.ElapsedMs:0.0} ms  [HTTP {p.StatusCode}]", series.Color));
            }
            else
            {
                string ms = p.ElapsedMs.HasValue ? $" after {p.ElapsedMs:0.0} ms" : "";
                lines.Add(($"{series.Name}: FAILED{ms} - {p.Error}", series.Color));
            }
        }

        float pad = 6 * k;
        float w = lines.Max(l => g.MeasureString(l.Text, Font).Width) + pad * 2;
        float lineH = Font.Height + 2;
        float h = lines.Count * lineH + pad * 2;
        float bx = m.X + 14 * k, by = m.Y + 14 * k;
        if (bx + w > Width) bx = m.X - 14 * k - w;
        if (by + h > Height) by = m.Y - 14 * k - h;
        bx = Math.Max(0, bx);
        by = Math.Max(0, by);

        using var bg = new SolidBrush(Color.FromArgb(240, Settings.ChartBackColor));
        using var border = new Pen(Settings.GridColor.GetBrightness() > 0.5f ? Color.Silver : Color.Gray);
        g.FillRectangle(bg, bx, by, w, h);
        g.DrawRectangle(border, bx, by, w, h);
        for (int i = 0; i < lines.Count; i++)
        {
            using var b = new SolidBrush(i == 0 ? Settings.ChartForeColor : lines[i].Color);
            g.DrawString(lines[i].Text, Font, b, bx + pad, by + pad + i * lineH);
        }
    }

    private void DrawLegend(Graphics g, List<SeriesData> visible, Rectangle plot, float k, Brush textBrush)
    {
        float x = plot.Left;
        float y = 10 * k;
        foreach (var series in visible)
        {
            using var brush = new SolidBrush(series.Color);
            g.FillRectangle(brush, x, y + Font.Height / 2f - 3 * k, 18 * k, 6 * k);
            x += 24 * k;
            string text = $"{series.Name}  ({series.Url})";
            g.DrawString(text, Font, textBrush, x, y);
            x += g.MeasureString(text, Font).Width + 24 * k;
        }
    }

    private void DrawCentered(Graphics g, string text, Rectangle rect, Brush brush)
    {
        using var f = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(text, Font, brush, rect, f);
    }

    private static double NiceStep(double raw)
    {
        if (raw <= 0 || double.IsNaN(raw)) return 1;
        double exp = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        double f = raw / exp;
        double nice = f <= 1 ? 1 : f <= 2 ? 2 : f <= 5 ? 5 : 10;
        return nice * exp;
    }
}
