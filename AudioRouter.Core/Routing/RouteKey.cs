namespace AudioRouter.Core.Routing;

/// <summary>
/// 路由的**身份键** —— 这是"路由能跨重启存活"的关键。
///
/// 【为什么不按 PID】
/// PID 每次进程启动都会变（Windows 上还会复用），按 PID 存下来的路由重启后必然失效。
/// 真正稳定的身份是**可执行文件路径**。
///
/// 【拿不到路径时】
/// 退回进程名并显式加 <c>name:</c> 前缀 —— 不去假装它和路径一样可靠：
/// 同名进程（例如两个不同目录的 python.exe）会被视作同一条路由，
/// 这个精度差异必须能被上层如实报告（<see cref="IsWeak"/>）。
/// </summary>
public static class RouteKey
{
    public const string PathPrefix = "path:";
    public const string NamePrefix = "name:";

    public static string For(string? exePath, string processName)
        => string.IsNullOrWhiteSpace(exePath)
            ? NamePrefix + Normalize(processName)
            : PathPrefix + Normalize(exePath);

    /// <summary>弱身份（只靠进程名匹配）：精度有限，必须让用户知道。</summary>
    public static bool IsWeak(string? key)
        => key is not null && key.StartsWith(NamePrefix, StringComparison.Ordinal);

    /// <summary>统一大小写与斜杠：Windows 路径大小写不敏感，同一路径必须落到同一个键。</summary>
    public static string Normalize(string value)
        => value.Trim().Replace('\\', '/').ToLowerInvariant();

    /// <summary>供界面显示：去掉前缀，弱身份加个标记。</summary>
    public static string Describe(string? key)
    {
        if (string.IsNullOrEmpty(key)) return string.Empty;
        if (key.StartsWith(PathPrefix, StringComparison.Ordinal)) return key[PathPrefix.Length..];
        if (key.StartsWith(NamePrefix, StringComparison.Ordinal)) return $"{key[NamePrefix.Length..]} (name only)";
        return key;
    }
}
