using System.Diagnostics;
using System.IO;
using System.Windows;
using AudioRouter.Core.Localization;

namespace AudioRouter.Gui;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // 必须早于主窗口 XAML 的加载：所有文案都通过 LocalizationService 取
        LocalizationService.Instance.Initialize();

        base.OnStartup(e);

        Core.Diagnostics.StartupLog.Write("=== app starting ===");
        Core.Diagnostics.StartupLog.Write(
            $"os build={Environment.OSVersion.Version.Build} x64={Environment.Is64BitProcess} " +
            $"backend={Core.Backends.AudioBackendFactory.Current.Name}");

        EnableBindingTraceIfRequested();

        // 注意：AccessViolationException 这类损坏状态异常无法被托管代码捕获，
        // 这里的兜底只覆盖普通托管异常。
        DispatcherUnhandledException += (_, args) =>
        {
            Core.Diagnostics.StartupLog.Write($"dispatcher exception: {args.Exception}");
            MessageBox.Show(
                "The UI hit an unhandled exception (the app was kept alive):\n\n" + args.Exception.Message,
                "Audio Router",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Core.Diagnostics.StartupLog.Write($"appdomain exception (fatal): {args.ExceptionObject}");
    }

    /// <summary>
    /// 打开 XAML 数据绑定错误追踪（设 AUDIOROUTER_TRACE_BINDINGS=1）。
    ///
    /// 为什么需要：绑定路径写错**不会**让编译或运行失败，只会静默显示空白 ——
    /// 重构视图模型时这是最容易漏的一类缺陷，必须能把它变成可查的日志。
    /// </summary>
    private static void EnableBindingTraceIfRequested()
    {
        if (Environment.GetEnvironmentVariable("AUDIOROUTER_TRACE_BINDINGS") != "1") return;

        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AudioRouter", "logs");
            Directory.CreateDirectory(directory);

            var listener = new TextWriterTraceListener(Path.Combine(directory, "binding-trace.log"));
            PresentationTraceSources.Refresh();
            PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error | SourceLevels.Warning;
            Trace.AutoFlush = true;

            Core.Diagnostics.StartupLog.Write("binding trace enabled → logs/binding-trace.log");
        }
        catch (Exception ex)
        {
            Core.Diagnostics.StartupLog.Write($"binding trace setup failed: {ex.Message}");
        }
    }
}
