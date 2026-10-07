using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AudioRouter.Gui.Services;

/// <summary>从 exe 提取应用图标（带缓存）。不依赖 System.Drawing。</summary>
internal static class AppIconService
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint ExtractIconEx(string file, int index,
                                             IntPtr[]? largeIcons, IntPtr[]? smallIcons, uint count);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private static readonly Dictionary<string, ImageSource?> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    public static ImageSource? GetIcon(string exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath)) return null;
        if (Cache.TryGetValue(exePath, out var cached)) return cached;

        ImageSource? result = null;
        var large = new IntPtr[1];
        try
        {
            if (ExtractIconEx(exePath, 0, large, null, 1) > 0 && large[0] != IntPtr.Zero)
            {
                var source = Imaging.CreateBitmapSourceFromHIcon(
                    large[0], Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(36, 36));
                source.Freeze();
                result = source;
            }
        }
        catch
        {
            result = null;
        }
        finally
        {
            if (large[0] != IntPtr.Zero) DestroyIcon(large[0]);
        }

        Cache[exePath] = result;
        return result;
    }

    public static void Clear() => Cache.Clear();
}
