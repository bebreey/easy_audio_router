using System.Windows.Media;

namespace AudioRouter.Gui.Services;

/// <summary>
/// 表现层：字母头像的调色板。
/// 颜色属于 UI，所以它住在 GUI 里 —— 核心库只保留一个 <c>object?</c> 槽位。
/// </summary>
internal static class AvatarPalette
{
    private static readonly Color[] Colors =
    {
        Color.FromRgb(0x4C, 0x9D, 0xFF), Color.FromRgb(0x3D, 0xDC, 0x97),
        Color.FromRgb(0xFF, 0xB4, 0x54), Color.FromRgb(0xC4, 0x8A, 0xFF),
        Color.FromRgb(0xFF, 0x8A, 0xB4), Color.FromRgb(0x54, 0xD6, 0xD6),
        Color.FromRgb(0x9D, 0xB4, 0xFF), Color.FromRgb(0xE0, 0xE0, 0x6B),
    };

    /// <summary>同一个 key 永远得到同一种颜色（按 exe 路径稳定）。</summary>
    public static Brush BrushFor(string key)
    {
        var hash = 17;
        foreach (var ch in key) hash = unchecked(hash * 31 + ch);

        var brush = new SolidColorBrush(Colors[Math.Abs(hash) % Colors.Length]);
        brush.Freeze();
        return brush;
    }
}
