using System.IO;

namespace AudioRouter.Core.Diagnostics;

/// <summary>
/// 极简诊断日志（跨平台）。
/// 存在的理由：核心可以在无桌面环境、无 stdout 常驻的场景下运行，
/// 出错时必须落到文件才有据可查。
/// </summary>
public static class StartupLog
{
    private static readonly object Gate = new();

    private static readonly string DefaultFilePath = BuildPath();

    private static bool _suppressed;

    public static string LogFilePath => DefaultFilePath;

    /// <summary>是否把日志同时写到标准输出（CLI 的 --verbose 打开）。</summary>
    public static bool EchoToConsole { get; set; }

    /// <summary>
    /// 关闭文件输出。
    /// 给测试进程用：它们跑的是同一份 Core，若不关掉就会把自己的
    /// reconcile/route 记录写进**应用日志**，直接毁掉线上问题的可诊断性。
    /// </summary>
    public static void SuppressFileOutput() => _suppressed = true;

    private static string BuildPath()
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AudioRouter", "logs");
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, "audio-router.log");
        }
        catch
        {
            return Path.Combine(Path.GetTempPath(), "audio-router.log");
        }
    }

    public static void Write(string message)
    {
        if (!_suppressed)
        {
            try
            {
                lock (Gate)
                {
                    File.AppendAllText(DefaultFilePath,
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  {message}{Environment.NewLine}");
                }
            }
            catch
            {
                // 日志失败绝不能影响主流程
            }
        }

        if (!EchoToConsole) return;

        try
        {
            Console.Error.WriteLine($"[log] {message}");
        }
        catch
        {
            // 无 stdout 环境忽略
        }
    }
}
