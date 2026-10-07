using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace AudioRouter.Desktop;

/// <summary>
/// 应用图标生成器（`--export-icon &lt;dir&gt;`）。
///
/// 为什么用代码画而不是塞一张图：图标要同时满足多种尺寸与两种用途
/// （窗口图标用 PNG、exe 内嵌图标用 ICO），用代码生成能保证**同一份设计**派生全部产物，
/// 且不引入任何外部图形依赖。
///
/// 图形语义：扬声器（音频）+ 右向箭头（路由），白描于品牌蓝渐变圆角底。
/// </summary>
internal static class IconGenerator
{
    private static readonly int[] Sizes = { 16, 32, 48, 256 };

    public static void Run(string outputDirectory)
    {
        // 渲染位图需要平台已初始化，但不需要真的开窗口
        Program.BuildAvaloniaApp().SetupWithoutStarting();

        Directory.CreateDirectory(outputDirectory);

        var images = new List<(int Size, byte[] Data)>();
        foreach (var size in Sizes) images.Add((size, Render(size)));

        var png = Path.Combine(outputDirectory, "app.png");
        var ico = Path.Combine(outputDirectory, "app.ico");

        File.WriteAllBytes(png, images[^1].Data); // 256 的 PNG 给 Window.Icon 用
        File.WriteAllBytes(ico, BuildIco(images));

        Console.WriteLine($"icon: {png} ({png.Length} bytes path)");
        Console.WriteLine($"icon: {ico} ({images.Count} sizes: {string.Join(", ", Sizes)})");
    }

    private static byte[] Render(int size)
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(size, size), new Vector(96, 96));

        using (var context = bitmap.CreateDrawingContext())
        {
            Draw(context, size);
        }

        using var stream = new MemoryStream();
        bitmap.Save(stream, new PngBitmapEncoderOptions());
        return stream.ToArray();
    }

    private static void Draw(DrawingContext context, int size)
    {
        // 底：品牌蓝渐变圆角方块
        var background = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.Parse("#5AA8FF"), 0),
                new GradientStop(Color.Parse("#2456B8"), 1),
            },
        };

        var corner = size * 0.22;
        context.DrawRectangle(background, null, new RoundedRect(new Rect(0, 0, size, size), corner));

        // 内容设计在 20 x 16 的空间里，按尺寸整体缩放（线宽随缩放一起变，小尺寸才看得清）
        const double designWidth = 20;
        const double designHeight = 16;
        var scale = size / designWidth;
        var offsetX = (size - designWidth * scale) / 2;
        var offsetY = (size - designHeight * scale) / 2;

        using (context.PushTransform(Matrix.CreateScale(scale, scale) *
                                     Matrix.CreateTranslation(offsetX, offsetY)))
        {
            // 扬声器（实心，小尺寸下比线稿清楚）
            context.DrawGeometry(
                Brushes.White,
                null,
                Geometry.Parse("M1,5.6 L4.1,5.6 L8.1,1.7 L8.1,14.3 L4.1,10.4 L1,10.4 Z"));

            // 路由箭头
            var pen = new Pen(Brushes.White, 1.8)
            {
                LineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round,
            };

            context.DrawGeometry(
                null,
                pen,
                Geometry.Parse("M10.6,8 L18.4,8 M15.2,4.6 L18.6,8 L15.2,11.4"));
        }
    }

    /// <summary>
    /// 手写 ICO 容器：ICONDIR(6B) + ICONDIRENTRY(16B/项) + 各尺寸负载。
    /// Vista 之后允许项负载直接是 PNG，因此不必再编码 BMP + 掩码。
    /// </summary>
    private static byte[] BuildIco(List<(int Size, byte[] Data)> images)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write((ushort)0);            // reserved
        writer.Write((ushort)1);            // type: icon
        writer.Write((ushort)images.Count); // image count

        var offset = 6 + 16 * images.Count;

        foreach (var (size, data) in images)
        {
            writer.Write((byte)(size >= 256 ? 0 : size)); // width  (0 表示 256)
            writer.Write((byte)(size >= 256 ? 0 : size)); // height
            writer.Write((byte)0);                        // palette colors
            writer.Write((byte)0);                        // reserved
            writer.Write((ushort)1);                      // color planes
            writer.Write((ushort)32);                     // bits per pixel
            writer.Write(data.Length);                    // payload size
            writer.Write(offset);                         // payload offset

            offset += data.Length;
        }

        foreach (var (_, data) in images) writer.Write(data);

        writer.Flush();
        return stream.ToArray();
    }
}
