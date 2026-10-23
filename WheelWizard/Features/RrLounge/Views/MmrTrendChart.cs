using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace WheelWizard.RrLounge.Views;

/// <summary>
/// A player's MMR over the season, like the "MMR TREND" chart on rr-lounge.com: the line over bands in the colours of
/// the ranks it passes through, the lowest and highest MMR on the left and the first and last dates underneath.
/// </summary>
public sealed class MmrTrendChart : Control
{
    private const double Left = 40;
    private const double Bottom = 18;
    private const double Top = 6;

    /// <summary>The MMR after each mogi, oldest first.</summary>
    public IReadOnlyList<int> Points { get; init; } = [];

    public IReadOnlyList<RrLoungeTier> Tiers { get; init; } = [];
    public string FirstDate { get; init; } = string.Empty;
    public string LastDate { get; init; } = string.Empty;

    private static readonly IBrush LabelBrush = new SolidColorBrush(Color.Parse("#B4B4BB"));
    private static readonly IPen LinePen = new Pen(new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)), 1.5);
    private static readonly IPen GridPen = new Pen(new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)), 1, new DashStyle([3, 3], 0));
    private static readonly IBrush DotBrush = new SolidColorBrush(Color.Parse("#0099CC"));

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var width = Bounds.Width - Left - 4;
        var height = Bounds.Height - Bottom - Top;
        if (Points.Count == 0 || width <= 0 || height <= 0)
            return;

        var low = Points.Min();
        var high = Points.Max();
        var pad = Math.Max(20, (high - low) * 0.06);
        var bottom = low - pad;
        var top = high + pad;
        double Y(double mmr) => Top + height - (mmr - bottom) / (top - bottom) * height;
        double X(int index) => Left + (Points.Count == 1 ? width / 2 : index * width / (Points.Count - 1));

        context.FillRectangle(new SolidColorBrush(Color.FromArgb(140, 22, 22, 22)), new Rect(Left, Top, width, height));

        // The rank bands the line passes through.
        var tiers = Tiers.OrderBy(tier => tier.MinMmr).ToList();
        for (var i = 0; i < tiers.Count; i++)
        {
            var from = Math.Max(bottom, tiers[i].MinMmr);
            var to = Math.Min(top, i + 1 < tiers.Count ? tiers[i + 1].MinMmr : top);
            if (to <= from || !Color.TryParse(tiers[i].Color, out var color))
                continue;
            context.FillRectangle(new SolidColorBrush(color, 0.22), new Rect(Left, Y(to), width, Y(from) - Y(to)));
        }

        foreach (var mmr in new[] { low, (low + high) / 2, high }.Distinct())
        {
            var y = Y(mmr);
            context.DrawLine(GridPen, new Point(Left, y), new Point(Left + width, y));
            Label(context, mmr.ToString("N0", CultureInfo.InvariantCulture), new Point(Left - 5, y), TextAlignment.Right);
        }

        var geometry = new StreamGeometry();
        using (var path = geometry.Open())
        {
            path.BeginFigure(new Point(X(0), Y(Points[0])), false);
            for (var i = 1; i < Points.Count; i++)
                path.LineTo(new Point(X(i), Y(Points[i])));
            path.EndFigure(false);
        }

        context.DrawGeometry(null, LinePen, geometry);
        if (Points.Count <= 80)
            for (var i = 0; i < Points.Count; i++)
                context.DrawEllipse(DotBrush, null, new Point(X(i), Y(Points[i])), 2.5, 2.5);

        Label(context, FirstDate, new Point(Left, Top + height + 3), TextAlignment.Left, top: true);
        Label(context, LastDate, new Point(Left + width, Top + height + 3), TextAlignment.Right, top: true);
    }

    private static void Label(DrawingContext context, string text, Point at, TextAlignment align, bool top = false)
    {
        if (text.Length == 0)
            return;
        var formatted = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 10, LabelBrush);
        var x = align == TextAlignment.Right ? at.X - formatted.Width : at.X;
        var y = top ? at.Y : at.Y - formatted.Height / 2;
        context.DrawText(formatted, new Point(x, y));
    }
}
