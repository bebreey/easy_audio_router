using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace AudioRouter.Core.Backends.Windows;

/// <summary>一次注入尝试的结果。失败时 <see cref="Message"/> 必须能让人判断下一步该做什么。</summary>
internal readonly record struct NativeInjectionResult(bool Ok, string Message)
{
    internal static NativeInjectionResult Success() => new(true, "ok");

    internal static NativeInjectionResult Failure(string message) => new(false, message);
}

/// <summary>
/// 把路由意图**真正下发**给注入式原生核心。
///
/// 链路（全部来自上游 C++ 源码，不是猜的）：
///   1. 往共享内存 <c>Local\audio-router-file</c> 写一段 blob（见 <see cref="NativeRoutingBlob"/>）；
///   2. 启动 <c>do.exe</c> / <c>do64.exe</c>，把"目标 pid + 本程序目录 + tid + flags"交给它；
///   3. do 自己会打开同一个映射、把 blob 拷进目标进程，并让目标进程加载
///      <c>audio-router.dll</c> / <c>audio-router64.dll</c>；
///   4. 注入后的 DLL 读这个 blob，按 <c>session_guid_and_flag</c> 把该会话的音频改到 device_id 上。
///
/// 用途之二：**撤销**路由不是"不注入"，而是再注入一次、把 flag 置 0（上游的 unload 语义）。
///
/// 权限：<c>do</c> 用 OpenProcess(PROCESS_ALL_ACCESS) 打开目标进程，
/// 所以对**同权限等级**的进程（普通用户自己的应用）不需要管理员；
/// 只有注入提权进程才需要以管理员身份运行本程序。
/// </summary>
internal static class NativeInjector
{
    private const uint PageReadWrite = 0x04;
    private const uint FileMapAllAccess = 0x000F001F;
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const uint WaitObject0 = 0x00000000;
    private const uint WaitTimeout = 0x00000102;
    private const uint CreateNoWindow = 0x08000000;
    private const uint DoExeTimeoutMs = 5000;   // 上游 release 版同样是 5000ms

    private const string MappingName = "Local\\audio-router-file";

    /// <summary>
    /// 最近一次注入的结果说明（成功为 "ok"）。
    /// 存在的理由：<c>RoutingOutcome</c> 只带枚举、带不了原因，而"注入失败"必须能告诉用户
    /// 是缺目标位数的工具链、还是权限不足、还是 do.exe 返回了错误码。
    /// </summary>
    internal static string LastMessage { get; private set; } = string.Empty;

    /// <summary>按目标进程位数选注入器与 DLL：x86 → do.exe + audio-router.dll；x64 → do64.exe + audio-router64.dll。</summary>
    private static (string DoExe, string Dll) ToolsFor(bool x86)
        => x86
            ? ("do.exe", "audio-router.dll")
            : ("do64.exe", "audio-router64.dll");

    /// <summary>检查某个位数的工具链是否齐全（缺哪个要说清楚，而不是笼统"不支持"）。</summary>
    internal static bool IsToolchainPresent(string? nativeDirectory, bool x86, out string missing)
    {
        missing = string.Empty;

        if (string.IsNullOrEmpty(nativeDirectory) || !Directory.Exists(nativeDirectory))
        {
            missing = "原生核心目录不存在";
            return false;
        }

        var (doExe, dll) = ToolsFor(x86);

        if (!File.Exists(Path.Combine(nativeDirectory, doExe)))
        {
            missing = doExe;
            return false;
        }

        if (!File.Exists(Path.Combine(nativeDirectory, dll)))
        {
            missing = dll;
            return false;
        }

        return true;
    }

    /// <summary>目标进程是不是 32 位（决定用哪套注入器）。</summary>
    internal static bool? IsTargetX86(int pid)
    {
        var handle = OpenProcess(ProcessQueryLimitedInformation, false, (uint)pid);
        if (handle == IntPtr.Zero) return null;

        try
        {
            return IsWow64Process(handle, out var wow64) ? wow64 : null;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    /// <summary>
    /// 下发一次路由（<paramref name="duplicate"/> 为 true 时是"复制到该设备"）。
    /// <paramref name="endpointId"/> 传 null 表示撤销这条路由。
    /// </summary>
    internal static NativeInjectionResult Apply(
        string? nativeDirectory,
        int pid,
        string? endpointId,
        bool duplicate)
    {
        var result = ApplyCore(nativeDirectory, pid, endpointId, duplicate);
        LastMessage = result.Ok ? "ok" : result.Message;
        return result;
    }

    private static NativeInjectionResult ApplyCore(
        string? nativeDirectory,
        int pid,
        string? endpointId,
        bool duplicate)
    {
        if (pid <= 0) return NativeInjectionResult.Failure("invalid pid");
        if (string.IsNullOrEmpty(nativeDirectory)) return NativeInjectionResult.Failure("native core directory not found");

        var unloading = string.IsNullOrEmpty(endpointId);

        var x86 = IsTargetX86(pid);
        if (x86 is null)
        {
            return NativeInjectionResult.Failure(
                $"cannot query process {pid} (access denied?) — elevated targets need Audio Router to run as administrator");
        }

        if (!IsToolchainPresent(nativeDirectory, x86.Value, out var missing))
        {
            return NativeInjectionResult.Failure(
                $"native toolchain incomplete for {(x86.Value ? "x86" : "x64")} target: missing {missing}");
        }

        var flag = unloading
            ? NativeRoutingBlob.FlagUnload
            : NativeRoutingBlob.BuildFlag(duplicate);

        var sessionGuidAndFlag = unloading
            ? 0u
            : NativeRoutingBlob.MakeSessionGuidAndFlag(NativeRoutingBlob.NextSessionGuid(), flag);

        var blob = NativeRoutingBlob.Build((uint)pid, endpointId, sessionGuidAndFlag);

        // 关键（踩过的坑）：映射句柄必须活到 do.exe 跑完为止。
        // 文件映射对象在**最后一个句柄关闭时即被销毁** —— 如果先关闭句柄再启动 do.exe，
        // do 会打不开映射、拿不到 blob 大小，目标进程里加载的 DLL 也就找不到参数，
        // DllMain 返回 FALSE，最终表现为 1114「DLL 初始化例程失败」。
        // 上游用 CHandle 的作用域保证同一件事：inject_dll 调用发生在句柄释放之前。
        var mapping = IntPtr.Zero;
        var view = IntPtr.Zero;

        try
        {
            if (!TryWriteMapping(blob, out mapping, out view, out var mapError))
            {
                return NativeInjectionResult.Failure($"shared memory failed: {mapError}");
            }

            var (doExe, _) = ToolsFor(x86.Value);
            return RunDelegator(Path.Combine(nativeDirectory, doExe), nativeDirectory, pid, x86.Value);
        }
        finally
        {
            if (view != IntPtr.Zero) UnmapViewOfFile(view);
            if (mapping != IntPtr.Zero) CloseHandle(mapping);
        }
    }

    /// <summary>
    /// 把 blob 写进共享内存映射。**句柄通过 out 返回、由调用方负责关闭** ——
    /// 必须保持映射存活直到 do.exe 跑完（见 <see cref="ApplyCore"/> 里的说明）。
    /// </summary>
    private static bool TryWriteMapping(byte[] blob, out IntPtr mapping, out IntPtr view, out string error)
    {
        mapping = IntPtr.Zero;
        view = IntPtr.Zero;
        error = string.Empty;

        try
        {
            mapping = CreateFileMappingW(
                new IntPtr(-1), IntPtr.Zero, PageReadWrite, 0, (uint)blob.Length, MappingName);

            if (mapping == IntPtr.Zero)
            {
                error = "CreateFileMapping failed, win32 error " + Marshal.GetLastWin32Error();
                return false;
            }

            view = MapViewOfFile(mapping, FileMapAllAccess, 0, 0, UIntPtr.Zero);
            if (view == IntPtr.Zero)
            {
                error = "MapViewOfFile failed, win32 error " + Marshal.GetLastWin32Error();
                return false;
            }

            Marshal.Copy(blob, 0, view, blob.Length);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// 以 <c>"do[64].exe" &lt;pid&gt; "&lt;程序目录&gt;" 0 0</c> 的形式启动委托进程。
    ///
    /// 参数含义（上游 do/main.cpp 要求恰好 5 个 argv）：argv[1]=pid、argv[2]=DLL 所在目录、
    /// argv[3]=tid、argv[4]=flags。flags=0 表示"直接加载 audio-router 本体"——
    /// 这是发布包唯一可用的路径，因为包里没有 bootstrapper64.dll（flags=3 才需要它）。
    /// </summary>
    private static NativeInjectionResult RunDelegator(string doExePath, string nativeDirectory, int pid, bool x86)
    {
        if (!File.Exists(doExePath))
        {
            return NativeInjectionResult.Failure($"delegator not found: {Path.GetFileName(doExePath)}");
        }

        var commandLine = $"\"{doExePath}\" {pid} \"{nativeDirectory}\" 0 0";
        var startupInfo = new StartupInfo();
        startupInfo.cb = Marshal.SizeOf<StartupInfo>();

        if (!CreateProcessW(
                null, commandLine, IntPtr.Zero, IntPtr.Zero, false,
                CreateNoWindow, IntPtr.Zero, nativeDirectory, ref startupInfo, out var processInfo))
        {
            return NativeInjectionResult.Failure(
                $"CreateProcess({Path.GetFileName(doExePath)}) failed, win32 error {Marshal.GetLastWin32Error()}");
        }

        try
        {
            var wait = WaitForSingleObject(processInfo.hProcess, DoExeTimeoutMs);

            if (wait == WaitTimeout)
            {
                return NativeInjectionResult.Failure(
                    $"delegator did not respond within {DoExeTimeoutMs}ms (target {(x86 ? "x86" : "x64")} pid {pid})");
            }

            if (wait != WaitObject0)
            {
                return NativeInjectionResult.Failure($"wait failed, win32 error {Marshal.GetLastWin32Error()}");
            }

            if (!GetExitCodeProcess(processInfo.hProcess, out var exitCode))
            {
                return NativeInjectionResult.Failure($"GetExitCodeProcess failed, win32 error {Marshal.GetLastWin32Error()}");
            }

            if (exitCode != 0)
            {
                // do.exe 把失败原因放在退出码里（上游用 CUSTOM_ERR(GetLastError())）
                return NativeInjectionResult.Failure(
                    $"delegator exited with 0x{exitCode:X8} (pid {pid}{DescribeExitCode(exitCode)})");
            }

            return NativeInjectionResult.Success();
        }
        finally
        {
            if (processInfo.hThread != IntPtr.Zero) CloseHandle(processInfo.hThread);
            if (processInfo.hProcess != IntPtr.Zero) CloseHandle(processInfo.hProcess);
        }
    }

    /// <summary>把上游 do/main.cpp 里的自定义错误码翻成人话。</summary>
    private static string DescribeExitCode(uint exitCode) => exitCode switch
    {
        0x80000000 => " = argument count mismatch",
        0x80000001 => " = invalid arguments (treat as 0x80000000 here; upstream CUSTOM_ERR)",
        5 => " = access denied (run as administrator)",
        87 => " = invalid parameter",
        _ => string.Empty,
    };

    // ---------------------------------------------------------------- P/Invoke

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfo
    {
        public int cb;
        public IntPtr lpReserved;
        public IntPtr lpDesktop;
        public IntPtr lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "CreateFileMappingW")]
    private static extern IntPtr CreateFileMappingW(
        IntPtr hFile, IntPtr lpFileMappingAttributes, uint flProtect,
        uint dwMaximumSizeHigh, uint dwMaximumSizeLow, string lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr MapViewOfFile(
        IntPtr hFileMappingObject, uint dwDesiredAccess,
        uint dwFileOffsetHigh, uint dwFileOffsetLow, UIntPtr dwNumberOfBytesToMap);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool UnmapViewOfFile(IntPtr lpBaseAddress);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool IsWow64Process(IntPtr hProcess, out bool wow64Process);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "CreateProcessW")]
    private static extern bool CreateProcessW(
        string? lpApplicationName, string lpCommandLine,
        IntPtr lpProcessAttributes, IntPtr lpThreadAttributes, bool bInheritHandles,
        uint dwCreationFlags, IntPtr lpEnvironment, string? lpCurrentDirectory,
        ref StartupInfo lpStartupInfo, out ProcessInformation lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);
}
