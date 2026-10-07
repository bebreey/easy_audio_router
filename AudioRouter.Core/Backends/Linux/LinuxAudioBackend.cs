using System.Runtime.Versioning;
using AudioRouter.Core.Formatting;
using AudioRouter.Core.Models;
using AudioRouter.Core.Routing;

namespace AudioRouter.Core.Backends.Linux;

/// <summary>
/// Linux 音频后端（PipeWire / PulseAudio，通过 pactl 的 pulse 兼容接口）。
///
/// 【这是本项目里第一个"真的能改道"的实现】
/// Windows 上要靠往目标进程注入 DLL + 改 COM 虚表才能做到的事，
/// 在 Linux 上是音频服务器的一等公民能力：
///   • 逐应用路由：`pactl move-sink-input &lt;index&gt; &lt;sink&gt;`
///   • 逐应用静音：`pactl set-sink-input-mute &lt;index&gt; 1`
/// 全程只是 IPC，不碰目标进程。
///
/// 能力边界（如实声明）：
///   • 路由：支持（把流移到另一个 sink）；
///   • **复制（同一应用同时输出到多个设备）：不支持** —— pulse 层一个流只能属于一个 sink，
///     需要先加载 module-combine-sink / 建虚拟 sink，属于系统级改动，规划中。
/// </summary>
[SupportedOSPlatform("linux")]
public sealed class LinuxAudioBackend : IAudioBackend
{
    private const string Tool = "pactl";

    /// <summary>PulseAudio 的「默认 sink」占位名，用于把流移回默认设备。</summary>
    private const string DefaultSinkAlias = "@DEFAULT_SINK@";

    public string Name => "PipeWire/PulseAudio (pactl)";

    /// <summary>
    /// 进程边界与平台判定都做成可注入的：
    /// 这样后端逻辑（**实际发出的 pactl 命令行**、解析、路由决策）可以在没有 Linux 内核的机器上被验证。
    /// 缺了这层，唯一没被验过的就正好是最容易出错的部分 —— 命令拼装。
    /// </summary>
    private readonly Func<string, string[], (int ExitCode, string StdOut, string StdErr)> _run;
    private readonly Func<bool> _isSupportedPlatform;
    private readonly Func<string, bool> _toolExists;

    public LinuxAudioBackend()
        : this(ProcessRunner.Run, () => OperatingSystem.IsLinux(), ProcessRunner.Exists)
    {
    }

    internal LinuxAudioBackend(
        Func<string, string[], (int ExitCode, string StdOut, string StdErr)> run,
        Func<bool> isSupportedPlatform,
        Func<string, bool> toolExists)
    {
        _run = run;
        _isSupportedPlatform = isSupportedPlatform;
        _toolExists = toolExists;
    }

    public bool IsAvailable => _isSupportedPlatform() && _toolExists(Tool);

    public bool SupportsRouting => IsAvailable;

    /// <summary>复制需要 module-combine-sink / 虚拟 sink（系统级改动），暂不支持。</summary>
    public bool SupportsDuplication => false;

    public string? Limitation
    {
        get
        {
            if (!_isSupportedPlatform()) return "not a Linux platform";
            if (!_toolExists(Tool)) return $"'{Tool}' not found in PATH (install pulseaudio-utils / pipewire-pulse)";
            return "duplicating one app to several devices needs module-combine-sink (planned)";
        }
    }

    // ======================================================================
    //  枚举
    // ======================================================================

    public IReadOnlyList<AudioDevice> EnumerateDevices()
    {
        if (!IsAvailable) return Array.Empty<AudioDevice>();

        var defaultSink = PactlParser.ParseDefaultSink(Run("get-default-sink").StdOut);
        var sinks = PactlParser.ParseSinks(RunJson("list", "sinks"), defaultSink);

        return sinks.Select(sink => new AudioDevice
        {
            // 用 sink 名做稳定 ID：index 会随加载顺序变化，不能持久化
            Id = sink.Name,
            FriendlyName = sink.Description,
            Kind = ClassifyKind(sink.Name, sink.Description),
            IsActive = true,
            IsEnabled = true,
            IsDefault = string.Equals(sink.Name, defaultSink, StringComparison.Ordinal),
            FormatText = FormatOf(sink),
        }).ToList();
    }

    /// <summary>sample_spec → 「24 bit · 48 kHz · 2ch」。认不出来就返回 null，不猜。</summary>
    private static string? FormatOf(PactlSink sink)
    {
        var text = AudioFormat.Describe(
            AudioFormat.BitsFromSampleFormat(sink.SampleFormat),
            sink.SampleRate,
            sink.Channels,
            AudioFormat.IsFloatSampleFormat(sink.SampleFormat));

        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    public IReadOnlyList<AppSession> EnumerateSessions()
    {
        if (!IsAvailable) return Array.Empty<AppSession>();

        var inputs = PactlParser.ParseSinkInputs(RunJson("list", "sink-inputs"));

        return inputs
            .GroupBy(i => i.Pid)
            .Select(group => new AppSession
            {
                Pid = group.Key,
                ExePath = TryReadProcExe(group.Key),
                ProcessName = group.Select(i => i.Binary).FirstOrDefault(b => !string.IsNullOrEmpty(b)) ?? "unknown",
                DisplayName = group.Select(i => i.Name).FirstOrDefault(n => !string.IsNullOrEmpty(n)) ?? "unknown",
                Volume = group.Max(i => i.Volume),
                IsMuted = group.All(i => i.Muted),
                IsPlaying = group.Any(i => !i.Corked),
            })
            .ToList();
    }

    /// <summary>
    /// 从 /proc/&lt;pid&gt;/exe 读出真实可执行文件路径。
    /// 有了它，Linux 上的路由也能按路径持久化（和 Windows 同一套身份键），
    /// 而不是退化到"按进程名匹配"的弱身份。
    /// </summary>
    private static string TryReadProcExe(int pid)
    {
        try
        {
            var link = File.ResolveLinkTarget($"/proc/{pid}/exe", returnFinalTarget: true);
            var path = link?.FullName;

            // 目标进程可能是 "<path> (deleted)"，剥掉这个后缀
            const string deleted = " (deleted)";
            if (path is not null && path.EndsWith(deleted, StringComparison.Ordinal))
            {
                path = path[..^deleted.Length];
            }

            return path ?? string.Empty;
        }
        catch
        {
            // 权限不足或进程已退出 —— 交给上层退回按进程名匹配
            return string.Empty;
        }
    }

    // ======================================================================
    //  操作
    // ======================================================================

    public bool SetProcessMute(int pid, bool muted)
    {
        if (!IsAvailable) return false;

        var changed = false;
        foreach (var input in InputsForPid(pid))
        {
            if (Run("set-sink-input-mute", input.Index.ToString(), muted ? "1" : "0").ExitCode == 0)
            {
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>把该进程的播放流移到目标 sink —— 这就是真正的逐应用路由。</summary>
    public RoutingOutcome ApplyRoute(int pid, string deviceId, RouteMode mode)
    {
        if (!IsAvailable) return RoutingOutcome.NotImplemented;

        // pulse 层一个流只能属于一个 sink，做不到"同时输出到多个设备"
        if (mode == RouteMode.Duplicate) return RoutingOutcome.NotImplemented;

        var inputs = InputsForPid(pid);
        if (inputs.Count == 0) return RoutingOutcome.NotFound;

        var moved = 0;
        foreach (var input in inputs)
        {
            if (Run("move-sink-input", input.Index.ToString(), deviceId).ExitCode == 0) moved++;
        }

        return moved > 0 ? RoutingOutcome.Applied : RoutingOutcome.NotImplemented;
    }

    /// <summary>把该进程的流移回默认 sink。</summary>
    public RoutingOutcome RemoveRoute(int pid, string deviceId)
    {
        if (!IsAvailable) return RoutingOutcome.NotImplemented;

        var inputs = InputsForPid(pid);
        if (inputs.Count == 0) return RoutingOutcome.NotFound;

        var moved = 0;
        foreach (var input in inputs)
        {
            if (Run("move-sink-input", input.Index.ToString(), DefaultSinkAlias).ExitCode == 0) moved++;
        }

        return moved > 0 ? RoutingOutcome.Applied : RoutingOutcome.NotImplemented;
    }

    /// <summary>
    /// 只对「该应用当前停在指定设备上的那条流」静音 —— 这是 Linux 上能精确做到的事，
    /// Windows 侧必须靠注入式核心才能按路由控流。
    /// </summary>
    public RoutingOutcome SetRouteMute(int pid, string deviceId, bool muted)
    {
        if (!IsAvailable) return RoutingOutcome.NotImplemented;

        var sinkIndex = IndexOfSink(deviceId);
        if (sinkIndex is null) return RoutingOutcome.DeviceUnavailable;

        var targets = InputsForPid(pid).Where(i => i.Sink == sinkIndex.Value).ToList();
        if (targets.Count == 0) return RoutingOutcome.NotFound;

        var changed = 0;
        foreach (var input in targets)
        {
            if (Run("set-sink-input-mute", input.Index.ToString(), muted ? "1" : "0").ExitCode == 0) changed++;
        }

        return changed > 0 ? RoutingOutcome.Applied : RoutingOutcome.NotImplemented;
    }

    private int? IndexOfSink(string sinkName)
    {
        var sinks = PactlParser.ParseSinks(RunJson("list", "sinks"), null);
        return sinks.FirstOrDefault(s => string.Equals(s.Name, sinkName, StringComparison.Ordinal))?.Index;
    }

    // ======================================================================
    //  内部
    // ======================================================================

    private List<PactlSinkInput> InputsForPid(int pid)
        => PactlParser.ParseSinkInputs(RunJson("list", "sink-inputs"))
            .Where(i => i.Pid == pid)
            .ToList();

    private (int ExitCode, string StdOut, string StdErr) Run(params string[] args)
        => _run(Tool, args);

    /// <summary>带 -f json 的调用；失败时返回空数组字面量，让解析器安全返回空集合。</summary>
    private string RunJson(params string[] args)
    {
        var full = new List<string> { "-f", "json" };
        full.AddRange(args);

        var result = _run(Tool, full.ToArray());
        return string.IsNullOrWhiteSpace(result.StdOut) ? "[]" : result.StdOut;
    }

    private static DeviceKind ClassifyKind(string name, string description)
    {
        var text = $"{name} {description}";

        if (Contains(text, "bluez") || Contains(text, "bluetooth")) return DeviceKind.Bluetooth;
        if (Contains(text, "usb")) return DeviceKind.Usb;
        if (Contains(text, "hdmi") || Contains(text, "displayport")) return DeviceKind.Hdmi;
        if (Contains(text, "headphone") || Contains(text, "headset") || Contains(text, "耳机")) return DeviceKind.Headphones;
        if (Contains(text, "null") || Contains(text, "virtual") || Contains(text, "loopback") ||
            Contains(text, "monitor") || Contains(text, "cable"))
            return DeviceKind.Virtual;

        return DeviceKind.Speakers;
    }

    private static bool Contains(string source, string value)
        => source.Contains(value, StringComparison.OrdinalIgnoreCase);
}
