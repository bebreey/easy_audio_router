using Avalonia;

namespace AudioRouter.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // 图标生成模式：不出窗口，渲染完即退出
        var exportIndex = Array.IndexOf(args, "--export-icon");
        if (exportIndex >= 0 && exportIndex + 1 < args.Length)
        {
            IconGenerator.Run(args[exportIndex + 1]);
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
