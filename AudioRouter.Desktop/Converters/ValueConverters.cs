using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using AudioRouter.Core.Models;

namespace AudioRouter.Desktop.Converters;

/// <summary>
/// 矢量图标。几何数据与 WPF 版**同源**（从 Controls.xaml 直接搬过来），
/// 保证两个前端的视觉一致，而不是各画一套。
/// </summary>
public static class Icons
{
    public static readonly Geometry Speaker = Geometry.Parse("M2,7 L5.5,7 L9.5,3.5 L9.5,14.5 L5.5,11 L2,11 Z M11.8,6.4 A4.2,4.2 0 0 1 11.8,11.6");
    public static readonly Geometry Headphones = Geometry.Parse("M3,12.5 L3,9 A6,6 0 0 1 15,9 L15,12.5 M2.2,12.5 L5.2,12.5 L5.2,16.5 L2.2,16.5 Z M12.8,12.5 L15.8,12.5 L15.8,16.5 L12.8,16.5 Z");
    public static readonly Geometry Mute = Geometry.Parse("M2,7 L5.5,7 L9.5,3.5 L9.5,14.5 L5.5,11 L2,11 Z M11,7 L15,11 M15,7 L11,11");
    public static readonly Geometry Route = Geometry.Parse("M2,8 L11,8 M8,4.5 L11.5,8 L8,11.5");
    public static readonly Geometry Chevron = Geometry.Parse("M1,1 L5,5 L9,1");
    public static readonly Geometry ChevronRight = Geometry.Parse("M6,3 L11,8 L6,13");
    public static readonly Geometry Refresh = Geometry.Parse("M13.5,8 A5.5,5.5 0 1 1 8,2.5 M8,0.5 L8,4.5 L12,2.5");
    public static readonly Geometry Globe = Geometry.Parse("M8,1 A7,7 0 1 1 8,15 A7,7 0 1 1 8,1 Z M1.1,8 L14.9,8 M8,1 A3.6,7 0 0 1 8,15 A3.6,7 0 0 1 8,1 Z");
    public static readonly Geometry Search = Geometry.Parse("M1.5,6.5 A5,5 0 1 1 11.5,6.5 A5,5 0 1 1 1.5,6.5 Z M10.4,10.4 L15,15");
    public static readonly Geometry Close = Geometry.Parse("M0,0 L9,9 M9,0 L0,9");
    public static readonly Geometry Empty = Geometry.Parse("M8,1 A7,7 0 1 1 8,15 A7,7 0 1 1 8,1 Z M8,5 L8,9 M8,11 L8.01,11");

    // 窗口按钮（与 WPF 版同一批几何）
    // 最小化必须是有高度的**填充矩形**：写成 `M0,0 L10,0` 这种零高度线段时，
    // 配 Stretch="Uniform" 会被压成 0 高度，图标直接消失（已踩过）。
    public static readonly Geometry WindowMinimize = Geometry.Parse("M0,0 L10,0 L10,1 L0,1 Z");
    public static readonly Geometry WindowMaximize = Geometry.Parse("M0.5,0.5 L9.5,0.5 L9.5,9.5 L0.5,9.5 Z");
    public static readonly Geometry WindowRestore = Geometry.Parse("M0.5,3.5 L6.5,3.5 L6.5,9.5 L0.5,9.5 Z M3.5,3.5 L3.5,0.5 L9.5,0.5 L9.5,6.5 L6.5,6.5");
    public static readonly Geometry WindowClose = Geometry.Parse("M0,0 L9,9 M9,0 L0,9");
}

/// <summary>设备类型 → 图标（耳机/蓝牙用耳机图标，其余用扬声器）。</summary>
public sealed class DeviceKindToGeometryConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DeviceKind kind) return Icons.Speaker;

        return kind switch
        {
            DeviceKind.Headphones or DeviceKind.Bluetooth => Icons.Headphones,
            _ => Icons.Speaker,
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// 应用名 → 稳定的字母头像底色。
/// 与 WPF 版同一套配色与哈希，保证同名应用在两个前端颜色一致。
/// </summary>
public sealed class NameToBrushConverter : IValueConverter
{
    private static readonly Color[] Palette =
    {
        Color.FromRgb(0x4C, 0x9D, 0xFF), Color.FromRgb(0x3D, 0xDC, 0x97),
        Color.FromRgb(0xFF, 0xB4, 0x54), Color.FromRgb(0xC4, 0x8A, 0xFF),
        Color.FromRgb(0xFF, 0x8A, 0xB4), Color.FromRgb(0x54, 0xD6, 0xD6),
        Color.FromRgb(0x9D, 0xB4, 0xFF), Color.FromRgb(0xE0, 0xE0, 0x6B),
    };

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value?.ToString() ?? string.Empty;

        var hash = 17;
        foreach (var ch in key) hash = unchecked(hash * 31 + ch);

        return new SolidColorBrush(Palette[Math.Abs(hash) % Palette.Length]);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>核心模型里的 object? 画刷（WPF 放 ImageSource/Brush，Avalonia 放 IBrush）。</summary>
public sealed class ObjectToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value as IBrush;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>核心模型里的 object? 图标（Avalonia 侧是 Bitmap）。</summary>
public sealed class ObjectToImageConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value as IImage;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>音量 0..1 → 迷你条宽度；ConverterParameter 给满格宽度（默认 40）。</summary>
public sealed class VolumeToWidthConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
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

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>提示类型 → 左侧色条颜色。</summary>
public sealed class ToastKindToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ToastKind kind
            ? kind switch
            {
                ToastKind.Success => new SolidColorBrush(Color.FromRgb(0x3D, 0xDC, 0x97)),
                ToastKind.Warning => new SolidColorBrush(Color.FromRgb(0xFF, 0xB4, 0x54)),
                ToastKind.Error => new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B)),
                _ => new SolidColorBrush(Color.FromRgb(0x4C, 0x9D, 0xFF)),
            }
            : new SolidColorBrush(Color.FromRgb(0x4C, 0x9D, 0xFF));

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
