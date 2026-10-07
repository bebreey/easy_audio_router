using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using AudioRouter.Core.Diagnostics;

namespace AudioRouter.Desktop.Services;

/// <summary>
/// 从 exe 提取应用图标。
///
/// 【为什么不能在核心层做】图标是表现层数据（Core 里是 <c>object?</c> 槽位），
/// 具体类型由 UI 决定：WPF 放 ImageSource，Avalonia 放 Bitmap。
///
/// 非 Windows 或提取失败时返回 null —— 界面自动退回字母头像，**不编造图标**。
/// </summary>
internal static class AppIconService
{
    private const int Size = 32;

    private static readonly ConcurrentDictionary<string, Bitmap?> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    public static Bitmap? Get(string? exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath)) return null;
        if (!OperatingSystem.IsWindows()) return null;

        // 缓存上限：桌面级用量远达不到，超了直接清空，避免无界增长
        if (Cache.Count > 256) Cache.Clear();

        return Cache.GetOrAdd(exePath, Extract);
    }

    private static Bitmap? Extract(string exePath)
    {
        try
        {
            if (!File.Exists(exePath)) return null;

            var large = new IntPtr[1];
            var small = new IntPtr[1];

            if (ExtractIconEx(exePath, 0, large, small, 1) == 0) return null;

            var hIcon = large[0] != IntPtr.Zero ? large[0] : small[0];
            if (hIcon == IntPtr.Zero) return null;

            try
            {
                return Render(hIcon);
            }
            finally
            {
                if (large[0] != IntPtr.Zero) DestroyIcon(large[0]);
                if (small[0] != IntPtr.Zero) DestroyIcon(small[0]);
            }
        }
        catch (Exception ex)
        {
            StartupLog.Write($"icon: 提取失败 '{exePath}'：{ex.Message}");
            return null;
        }
    }

    /// <summary>把 HICON 画进 32bpp DIB，再拷成 Avalonia 的 WriteableBitmap（BGRA，预乘）。</summary>
    private static Bitmap? Render(IntPtr hIcon)
    {
        var hdc = IntPtr.Zero;
        var dib = IntPtr.Zero;
        var previous = IntPtr.Zero;

        try
        {
            hdc = CreateCompatibleDC(IntPtr.Zero);
            if (hdc == IntPtr.Zero) return null;

            var info = new BitmapInfo
            {
                Header = new BitmapInfoHeader
                {
                    Size = 40,
                    Width = Size,
                    Height = -Size, // 负高度 = 自上而下，省一次翻转
                    Planes = 1,
                    BitCount = 32,
                    Compression = 0, // BI_RGB
                },
            };

            dib = CreateDIBSection(hdc, ref info, 0, out var bits, IntPtr.Zero, 0);
            if (dib == IntPtr.Zero || bits == IntPtr.Zero) return null;

            previous = SelectObject(hdc, dib);

            const int DiNormal = 0x0003;
            if (!DrawIconEx(hdc, 0, 0, hIcon, Size, Size, 0, IntPtr.Zero, DiNormal)) return null;

            var rowBytes = Size * 4;
            var rows = new byte[Size][];
            var anyAlpha = false;

            for (var y = 0; y < Size; y++)
            {
                var row = new byte[rowBytes];
                Marshal.Copy(IntPtr.Add(bits, y * rowBytes), row, 0, rowBytes);
                rows[y] = row;

                for (var x = 3; x < rowBytes; x += 4)
                {
                    if (row[x] != 0)
                    {
                        anyAlpha = true;
                        break;
                    }
                }
            }

            // 老式图标只有 1 位掩码、没有 alpha 通道：整幅 alpha 会是 0，直接画等于全透明。
            // 与其显示一个看不见的图标，不如按不透明处理。
            if (!anyAlpha)
            {
                StartupLog.Write("icon: 图标无 alpha 通道，按不透明处理");
                foreach (var row in rows)
                {
                    for (var x = 3; x < rowBytes; x += 4) row[x] = 0xFF;
                }
            }

            var bitmap = new WriteableBitmap(
                new PixelSize(Size, Size),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Premul);

            using (var buffer = bitmap.Lock())
            {
                for (var y = 0; y < Size; y++)
                {
                    Marshal.Copy(rows[y], 0, IntPtr.Add(buffer.Address, y * buffer.RowBytes), rowBytes);
                }
            }

            return bitmap;
        }
        finally
        {
            if (previous != IntPtr.Zero && hdc != IntPtr.Zero) SelectObject(hdc, previous);
            if (dib != IntPtr.Zero) DeleteObject(dib);
            if (hdc != IntPtr.Zero) DeleteDC(hdc);
        }
    }

    // ======================================================================
    //  Win32
    // ======================================================================

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint ExtractIconEx(string file, int index, IntPtr[] large, IntPtr[] small, uint count);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr icon);

    [DllImport("user32.dll")]
    private static extern bool DrawIconEx(IntPtr hdc, int x, int y, IntPtr icon,
                                          int width, int height, int step, IntPtr brush, int flags);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr obj);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateDIBSection(IntPtr hdc, ref BitmapInfo info, uint usage,
                                                  out IntPtr bits, IntPtr section, uint offset);

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public int Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public int Compression;
        public int SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public int ClrUsed;
        public int ClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public BitmapInfoHeader Header;
        public uint Colors;
    }
}
