using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace AudioRouter.Gui.Converters;

/// <summary>bool → Visibility；ConverterParameter="invert" 取反。</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var flag = value is bool b && b;
        if (parameter is string s && s.Equals("invert", StringComparison.OrdinalIgnoreCase)) flag = !flag;
        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>null → Collapsed（用于图标缺失时切字母头像）。</summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var has = value is not null;
        if (parameter is string s && s.Equals("invert", StringComparison.OrdinalIgnoreCase)) has = !has;
        return has ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// 音量 0..1 → 迷你条宽度。ConverterParameter 给满格宽度（默认 40）。
/// 刻意放在视图层：像素宽度是表现层概念，核心模型不提供它。
/// </summary>
public sealed class VolumeToWidthConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var volume = value switch
        {
            float f => f,
            double d => (float)d,
            _ => 0f,
        };

        var max = 40d;
        if (parameter is string text &&
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            max = parsed;
        }

        return Math.Round(Math.Clamp(volume, 0f, 1f) * max);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// 峰值 (0..1) → 图标外圈圆弧几何。
/// 半径 18、圆心 (24,24)，从 -90°（正上方）顺时针扫过 peak*360°。
/// </summary>
public sealed class PeakToArcConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var peak = value switch
        {
            float f => f,
            double d => (float)d,
            _ => 0f,
        };

        peak = Math.Clamp(peak, 0f, 1f);
        if (peak <= 0.004f) return Geometry.Empty;

        const double cx = 24, cy = 24, r = 18;
        var sweep = peak * 359.9;
        var start = PointOn(cx, cy, r, -90);
        var end = PointOn(cx, cy, r, -90 + sweep);

        var figure = new PathFigure { StartPoint = start, IsClosed = false, IsFilled = false };
        figure.Segments.Add(new ArcSegment(end, new Size(r, r), 0, sweep > 180,
            SweepDirection.Clockwise, true));

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        geometry.Freeze();
        return geometry;
    }

    private static Point PointOn(double cx, double cy, double r, double degrees)
    {
        var rad = degrees * Math.PI / 180.0;
        return new Point(cx + r * Math.Cos(rad), cy + r * Math.Sin(rad));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
