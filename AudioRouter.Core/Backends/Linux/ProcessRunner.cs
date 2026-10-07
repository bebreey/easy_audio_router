using System.Diagnostics;
using System.IO;

namespace AudioRouter.Core.Backends.Linux;

/// <summary>
/// 外部命令调用（Linux 后端用）。
///
/// 设计取舍：
///   • 加超时 —— 音频服务器的命令卡住时绝不能把整个程序带住；
///   • 只依赖 PATH 查找，不 spawn 进程做存在性判断（省一次进程创建）。
/// </summary>
internal static class ProcessRunner
{
    public static bool Exists(string command)
    {
        try
        {
            var path = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(path)) return false;

            foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                foreach (var candidate in new[] { command, command + ".exe" })
                {
                    var full = Path.Combine(directory, candidate);
                    if (File.Exists(full)) return true;
                }
            }
        }
        catch
        {
            // 忽略
        }

        return false;
    }

    public static (int ExitCode, string StdOut, string StdErr) Run(string file, params string[] args)
    {
        try
        {
            var info = new ProcessStartInfo
            {
                FileName = file,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            foreach (var arg in args) info.ArgumentList.Add(arg);

            using var process = Process.Start(info);
            if (process is null) return (-1, string.Empty, $"failed to start {file}");

            var stdOut = process.StandardOutput.ReadToEnd();
            var stdErr = process.StandardError.ReadToEnd();

            if (!process.WaitForExit(5000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // 忽略
                }

                return (-1, stdOut, $"timeout: {file} {string.Join(' ', args)}");
            }

            return (process.ExitCode, stdOut, stdErr);
        }
        catch (Exception ex)
        {
            return (-1, string.Empty, ex.Message);
        }
    }
}
