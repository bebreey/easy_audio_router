using System.IO;

namespace AudioRouter.Core.Backends;

/// <summary>
/// 探测注入式原生核心（上游 0.10.2 发布包里的四个文件）是否就位。
///
/// 为什么需要探测而不是写死：「能不能真正改道」取决于这些二进制在不在 ——
/// 核心是 C++ 工程，很多环境编不出来（本机就没有 MSVC）。把能力做成**运行时探测**，
/// CLI 与 GUI 就能如实报告边界，而不是假装功能可用。
///
/// 位数必须分开判断：32 位目标要用 <c>do.exe + audio-router.dll</c>，
/// 64 位目标要用 <c>do64.exe + audio-router64.dll</c>，两者不可互替。
/// （上游发布包里**没有** bootstrapper*.dll，所以只能走 flags=0 的直接加载路径。）
/// </summary>
public static class NativeCoreProbe
{
    /// <summary>32 位目标需要的文件。</summary>
    private static readonly string[] X86Files = { "do.exe", "audio-router.dll" };

    /// <summary>64 位目标需要的文件。</summary>
    private static readonly string[] X64Files = { "do64.exe", "audio-router64.dll" };

    private static bool _probed;
    private static string? _directory;
    private static bool _hasX86;
    private static bool _hasX64;

    /// <summary>原生核心所在目录（找到任一套时）。</summary>
    public static string? Directory
    {
        get
        {
            EnsureProbed();
            return _directory;
        }
    }

    /// <summary>是否至少有一套完整工具链。</summary>
    public static bool IsPresent
    {
        get
        {
            EnsureProbed();
            return _hasX86 || _hasX64;
        }
    }

    /// <summary>32 位工具链是否齐全。</summary>
    public static bool HasX86
    {
        get
        {
            EnsureProbed();
            return _hasX86;
        }
    }

    /// <summary>64 位工具链是否齐全。</summary>
    public static bool HasX64
    {
        get
        {
            EnsureProbed();
            return _hasX64;
        }
    }

    /// <summary>人话描述当前状态（CLI doctor 与 Limitation 共用）。</summary>
    public static string Describe()
    {
        if (!IsPresent) return "not found";

        var parts = new List<string>();
        if (_hasX64) parts.Add("x64");
        if (_hasX86) parts.Add("x86");

        return $"found ({string.Join(" + ", parts)}) in {_directory}";
    }

    /// <summary>便于测试注入（测试里用临时目录模拟不同组合）。</summary>
    internal static void OverrideForTests(string? directory, bool hasX86, bool hasX64)
    {
        _probed = true;
        _directory = directory;
        _hasX86 = hasX86;
        _hasX64 = hasX64;
    }

    internal static void ResetForTests()
    {
        _probed = false;
        _directory = null;
        _hasX86 = false;
        _hasX64 = false;
    }

    private static void EnsureProbed()
    {
        if (_probed) return;

        foreach (var candidate in CandidateDirectories())
        {
            try
            {
                if (!System.IO.Directory.Exists(candidate)) continue;

                var x86 = HasAll(candidate, X86Files);
                var x64 = HasAll(candidate, X64Files);

                if (!x86 && !x64) continue;

                _directory = candidate;
                _hasX86 = x86;
                _hasX64 = x64;
                break;
            }
            catch
            {
                // 目录不可读，跳过
            }
        }

        _probed = true;
    }

    private static bool HasAll(string directory, string[] files)
    {
        foreach (var file in files)
        {
            if (!File.Exists(Path.Combine(directory, file))) return false;
        }

        return true;
    }

    private static IEnumerable<string> CandidateDirectories()
    {
        var baseDirectory = AppContext.BaseDirectory;
        yield return baseDirectory;
        yield return Path.Combine(baseDirectory, "native");
        yield return Path.Combine(baseDirectory, "x64");
        yield return Path.Combine(baseDirectory, "x86");
    }
}
