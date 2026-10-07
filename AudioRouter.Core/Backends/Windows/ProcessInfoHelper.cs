using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace AudioRouter.Core.Backends.Windows;

/// <summary>进程信息读取：优先用 Win32（跨位数、低权限可用），非 Windows 退回 BCL。</summary>
internal static class ProcessInfoHelper
{
    private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inheritHandle, uint processId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageName(IntPtr process, uint flags,
                                                        StringBuilder exeName, ref uint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    public static string? TryGetExecutablePath(uint pid)
    {
        if (!OperatingSystem.IsWindows()) return null;

        IntPtr handle = IntPtr.Zero;
        try
        {
            handle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (handle == IntPtr.Zero) return null;

            var buffer = new StringBuilder(1024);
            uint size = (uint)buffer.Capacity;
            if (!QueryFullProcessImageName(handle, 0, buffer, ref size)) return null;
            return buffer.ToString(0, (int)size);
        }
        catch
        {
            return null;
        }
        finally
        {
            if (handle != IntPtr.Zero) CloseHandle(handle);
        }
    }

    public static string TryGetProcessName(uint pid)
    {
        try
        {
            using var process = Process.GetProcessById((int)pid);
            return process.ProcessName;
        }
        catch
        {
            return "unknown";
        }
    }

    /// <summary>优先 FileDescription（面向用户的名字），回退进程名。</summary>
    public static string ResolveDisplayName(string? exePath, string processName)
    {
        if (!string.IsNullOrWhiteSpace(exePath))
        {
            try
            {
                var info = FileVersionInfo.GetVersionInfo(exePath);
                if (!string.IsNullOrWhiteSpace(info.FileDescription)) return info.FileDescription!.Trim();
                if (!string.IsNullOrWhiteSpace(info.ProductName)) return info.ProductName!.Trim();
            }
            catch
            {
                // 忽略：读不到版本信息就回退
            }
        }

        if (processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            processName = processName[..^4];

        return string.IsNullOrWhiteSpace(processName)
            ? Localization.Loc.T("app.unknown")
            : processName;
    }
}
