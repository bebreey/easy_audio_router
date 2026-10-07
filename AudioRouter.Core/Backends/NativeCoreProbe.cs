using System.IO;

namespace AudioRouter.Core.Backends;

/// <summary>
/// 探测注入式原生核心（audio-router.dll / do.exe）是否存在。
///
/// 为什么需要探测而不是写死：
/// 「能不能真正改道」取决于这些二进制在不在 —— 核心是 C++ 工程，很多环境编不出来。
/// 把能力做成**运行时探测**，CLI 与 GUI 就能如实报告边界，而不是假装功能可用。
/// </summary>
public static class NativeCoreProbe
{
    private static bool? _present;
    private static string? _directory;

    /// <summary>原生核心所在目录（找到时）。</summary>
    public static string? Directory
    {
        get
        {
            if (_present is null) Probe();
            return _directory;
        }
    }

    public static bool IsPresent
    {
        get
        {
            if (_present is null) Probe();
            return _present == true;
        }
    }

    private static void Probe()
    {
        foreach (var candidate in CandidateDirectories())
        {
            try
            {
                if (File.Exists(Path.Combine(candidate, "audio-router.dll")) &&
                    File.Exists(Path.Combine(candidate, "do.exe")))
                {
                    _present = true;
                    _directory = candidate;
                    return;
                }
            }
            catch
            {
                // 目录不可读，跳过
            }
        }

        _present = false;
    }

    private static IEnumerable<string> CandidateDirectories()
    {
        var baseDirectory = AppContext.BaseDirectory;
        yield return baseDirectory;
        yield return Path.Combine(baseDirectory, "native");
        yield return Path.Combine(baseDirectory, "x64");
    }
}
