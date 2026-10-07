using AudioRouter.Core.Models;
using AudioRouter.Core.Routing;

namespace AudioRouter.Core.Backends;

/// <summary>
/// 音频后端抽象 —— 跨平台的关键接缝。
///
/// 三种实现的现实差异（决定了这个接口的形状）：
///   • WASAPI（Windows）：注入式核心才能改道，本机 C++ 核心尚不可编译 ⇒ SupportsRouting = false（暂时）
///   • PipeWire / PulseAudio（Linux）：音频服务器原生支持逐应用路由与多设备复制，纯 IPC ⇒ 可直接改道
///   • CoreAudio（macOS）：需虚拟音频设备 / HAL 插件，工程量最大
///
/// 因此接口把「能不能改道」显式暴露出来，让上层如实报告能力，
/// 而不是假装三个平台一样 —— 这一点对 CLI 尤其重要（脚本要能判断能力边界）。
/// </summary>
public interface IAudioBackend
{
    /// <summary>后端名称，例如 "WASAPI" / "PipeWire" / "unsupported"。</summary>
    string Name { get; }

    /// <summary>当前平台是否真的能用（false 表示仅占位，能力查询应给出原因）。</summary>
    bool IsAvailable { get; }

    /// <summary>不可用时/能力受限时的原因说明。</summary>
    string? Limitation { get; }

    /// <summary>是否能在**不修改目标进程**的前提下把音频改道。</summary>
    bool SupportsRouting { get; }

    /// <summary>是否支持同一应用同时输出到多个设备。</summary>
    bool SupportsDuplication { get; }

    IReadOnlyList<AudioDevice> EnumerateDevices();

    IReadOnlyList<AppSession> EnumerateSessions();

    /// <summary>对某进程在默认设备上的所有会话设置静音；返回是否有会话被改动。</summary>
    bool SetProcessMute(int pid, bool muted);

    /// <summary>
    /// 把某进程的音频改道到指定设备。
    /// 默认实现返回 NotImplemented —— 哪个平台先实现，就由哪个后端覆盖。
    /// （Linux 计划用 PipeWire/PulseAudio 的 IPC；Windows 需要注入式原生核心。）
    /// </summary>
    RoutingOutcome ApplyRoute(int pid, string deviceId, RouteMode mode) => RoutingOutcome.NotImplemented;

    /// <summary>解除某进程在指定设备上的改道。</summary>
    RoutingOutcome RemoveRoute(int pid, string deviceId) => RoutingOutcome.NotImplemented;

    /// <summary>
    /// 仅对「某进程在指定设备上的那条流」静音（不影响该应用在其它设备上的输出）。
    /// Linux 可用 `set-sink-input-mute` 精确实现；Windows 需要核心按路由单独控流。
    /// </summary>
    RoutingOutcome SetRouteMute(int pid, string deviceId, bool muted) => RoutingOutcome.NotImplemented;
}

/// <summary>
/// 尚未实现的后端占位：
/// 让程序在 macOS / 未支持的平台上仍能启动并给出明确说明，而不是崩在启动路径上。
/// </summary>
public sealed class UnsupportedAudioBackend : IAudioBackend
{
    public UnsupportedAudioBackend(string name, string limitation)
    {
        Name = name;
        Limitation = limitation;
    }

    public string Name { get; }

    public bool IsAvailable => false;

    public string? Limitation { get; }

    public bool SupportsRouting => false;

    public bool SupportsDuplication => false;

    public IReadOnlyList<AudioDevice> EnumerateDevices() => Array.Empty<AudioDevice>();

    public IReadOnlyList<AppSession> EnumerateSessions() => Array.Empty<AppSession>();

    public bool SetProcessMute(int pid, bool muted) => false;
}

public static class AudioBackendFactory
{
    private static IAudioBackend? _current;

    public static IAudioBackend Current => _current ??= Create();

    /// <summary>便于测试注入。</summary>
    public static void Override(IAudioBackend backend) => _current = backend;

    private static IAudioBackend Create()
    {
        if (OperatingSystem.IsWindows()) return new Windows.WasapiAudioBackend();

        if (OperatingSystem.IsLinux())
        {
            // Linux：音频服务器原生支持逐应用路由与静音，纯 IPC，无需注入
            return new Linux.LinuxAudioBackend();
        }

        if (OperatingSystem.IsMacOS())
        {
            return new UnsupportedAudioBackend("CoreAudio",
                "macOS backend not implemented yet (requires a virtual audio device / HAL plugin)");
        }

        return new UnsupportedAudioBackend("unknown", $"unsupported platform: {Environment.OSVersion.Platform}");
    }
}
