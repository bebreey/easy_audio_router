using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using AudioRouter.Core.Diagnostics;
using AudioRouter.Core.Formatting;
using AudioRouter.Core.Models;
using AudioRouter.Core.Routing;

namespace AudioRouter.Core.Backends.Windows;

/// <summary>
/// Windows 音频后端（WASAPI）。
///
/// 能力边界（如实声明，不粉饰）：
///   • 枚举设备 / 枚举会话 / 静音：公开 WASAPI 即可，已可用；
///   • **改道（把某应用的音频送到指定设备）：需要注入式原生核心**（audio-router.dll + do.exe，
///     通过 patch IAudioClient::Activate 实现）。该核心是 C++ 工程，本机无 MSVC 时不可编译，
///     因此 SupportsRouting 取决于原生核心是否存在（NativeCoreProbe）。
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WasapiAudioBackend : IAudioBackend
{
    public string Name => "WASAPI";

    public bool IsAvailable => OperatingSystem.IsWindows();

    /// <summary>
    /// 能否改道 = 注入工具链是否齐全（位数分开判断，见 <see cref="NativeCoreProbe"/>）。
    /// 任一套齐全就先报 true；若某次注入缺少**目标位数**的那一套，
    /// <see cref="ApplyRoute"/> 会返回失败并给出原因 —— 比笼统说"不支持"有用。
    /// </summary>
    public bool SupportsRouting => NativeCoreProbe.IsPresent;

    /// <summary>复制到多设备走的是同一条注入链路（flag=2），能力与改道一致。</summary>
    public bool SupportsDuplication => NativeCoreProbe.IsPresent;

    public string? Limitation
    {
        get
        {
            if (!NativeCoreProbe.IsPresent)
            {
                return "routing needs the injected native core (do.exe/do64.exe + audio-router.dll/audio-router64.dll) next to the app";
            }

            if (NativeCoreProbe.HasX64 && NativeCoreProbe.HasX86) return null;

            return NativeCoreProbe.HasX64
                ? "only the x64 toolchain is present; 32-bit target apps cannot be routed"
                : "only the x86 toolchain is present; 64-bit target apps cannot be routed";
        }
    }

    /// <summary>
    /// 真正下发：往 <c>Local\audio-router-file</c> 写路由 blob，再让 do[64].exe 把它注入目标进程。
    ///
    /// 失败时把原因留在日志与 <see cref="NativeInjector.LastMessage"/> 里 ——
    /// 只说"失败"等于让用户自己猜是权限问题还是位数问题。
    /// </summary>
    public RoutingOutcome ApplyRoute(int pid, string deviceId, RouteMode mode)
    {
        if (!OperatingSystem.IsWindows()) return RoutingOutcome.NotImplemented;

        var duplicate = mode == RouteMode.Duplicate;

        // "复制到设备 X"的语义是"保留当前输出 + 加上 X"。
        // 原生核心的 flag=2 只负责"追加"，所以第一次下发复制时必须先给它一个基准设备
        // —— 也就是当前默认设备（实测：少了这一步，默认设备上的输出会丢）。
        string? baseDeviceId = null;
        if (duplicate)
        {
            baseDeviceId = EnumerateDevices().FirstOrDefault(d => d.IsDefault)?.Id;
            if (string.IsNullOrEmpty(baseDeviceId)) return RoutingOutcome.DeviceUnavailable;
        }

        var result = NativeInjector.Apply(NativeCoreProbe.Directory, pid, deviceId, duplicate, baseDeviceId);

        StartupLog.Write($"inject: apply pid={pid} device='{deviceId}' duplicate={duplicate} → {(result.Ok ? "ok" : result.Message)}");

        return result.Ok ? RoutingOutcome.Applied : RoutingOutcome.Failed;
    }

    /// <summary>解除改道 = 再注入一次并把标志置 0（上游的 unload 语义），而不是"什么都不做"。</summary>
    public RoutingOutcome RemoveRoute(int pid, string deviceId)
    {
        if (!OperatingSystem.IsWindows()) return RoutingOutcome.NotImplemented;

        var result = NativeInjector.Apply(NativeCoreProbe.Directory, pid, null, false);

        StartupLog.Write($"inject: remove pid={pid} → {(result.Ok ? "ok" : result.Message)}");

        return result.Ok ? RoutingOutcome.Applied : RoutingOutcome.Failed;
    }

    public IReadOnlyList<AudioDevice> EnumerateDevices()
    {
        if (!OperatingSystem.IsWindows()) return Array.Empty<AudioDevice>();

        var result = new List<AudioDevice>();

        IMMDeviceEnumerator? enumerator = null;
        IMMDeviceCollection? collection = null;
        IMMDevice? defaultDevice = null;

        try
        {
            enumerator = WasapiNative.CreateEnumerator();

            string? defaultId = null;
            if (enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eConsole, out defaultDevice) == 0 &&
                defaultDevice is not null)
            {
                defaultDevice.GetId(out defaultId);
            }

            if (enumerator.EnumAudioEndpoints(EDataFlow.eRender, DeviceState.All, out collection) != 0 ||
                collection is null)
            {
                return result;
            }

            if (collection.GetCount(out var count) != 0) return result;

            for (uint i = 0; i < count; i++)
            {
                IMMDevice? device = null;
                IPropertyStore? store = null;
                try
                {
                    if (collection.Item(i, out device) != 0 || device is null) continue;
                    if (device.GetId(out var id) != 0) continue;
                    if (device.GetState(out var state) != 0) continue;

                    string? name = null;
                    uint? formFactor = null;
                    if (device.OpenPropertyStore(WasapiNative.STGM_READ, out store) == 0 && store is not null)
                    {
                        var friendly = WasapiNative.ReadString(store, PropertyKey.DeviceFriendlyName);
                        var description = WasapiNative.ReadString(store, PropertyKey.DeviceDesc);
                        name = !string.IsNullOrWhiteSpace(friendly) ? friendly : description;
                        formFactor = WasapiNative.ReadUInt32(store, PropertyKey.AudioEndpointFormFactor);
                    }

                    var isActive = (state & DeviceState.Active) != 0;

                    result.Add(new AudioDevice
                    {
                        Id = id ?? string.Empty,
                        FriendlyName = string.IsNullOrWhiteSpace(name)
                            ? Localization.Loc.T("device.unnamed")
                            : name!.Trim(),
                        Kind = ClassifyKind(name, formFactor),
                        IsActive = isActive,
                        IsEnabled = isActive,
                        IsDefault = defaultId is not null &&
                                    string.Equals(defaultId, id, StringComparison.OrdinalIgnoreCase),
                        // 只对在用端点取格式：26 个失效端点逐个 Activate 纯属浪费，且多半会失败
                        FormatText = isActive ? TryGetMixFormat(device) : null,
                    });
                }
                catch
                {
                    // 单个设备失败不影响整体
                }
                finally
                {
                    if (store is not null) Marshal.ReleaseComObject(store);
                    if (device is not null) Marshal.ReleaseComObject(device);
                }
            }
        }
        catch
        {
            // 交给上层处理
        }
        finally
        {
            if (defaultDevice is not null) Marshal.ReleaseComObject(defaultDevice);
            if (collection is not null) Marshal.ReleaseComObject(collection);
            if (enumerator is not null) Marshal.ReleaseComObject(enumerator);
        }

        return result;
    }

    public IReadOnlyList<AppSession> EnumerateSessions()
    {
        if (!OperatingSystem.IsWindows()) return Array.Empty<AppSession>();

        var buckets = new Dictionary<uint, Bucket>();
        var selfPid = (uint)Environment.ProcessId;

        WasapiNative.WithSessionManager(manager =>
        {
            IAudioSessionEnumerator? sessions = null;
            try
            {
                if (manager.GetSessionEnumerator(out sessions) != 0 || sessions is null) return true;
                if (sessions.GetCount(out var count) != 0) return true;

                for (var i = 0; i < count; i++)
                {
                    IAudioSessionControl? control = null;
                    try
                    {
                        if (sessions.GetSession(i, out control) != 0 || control is null) continue;
                        if (control is not IAudioSessionControl2 control2) continue;

                        // IsSystemSoundsSession: S_OK(0) 表示系统声音会话
                        if (control2.IsSystemSoundsSession() == 0) continue;
                        if (control2.GetProcessId(out var pid) != 0 || pid == 0 || pid == selfPid) continue;

                        control2.GetState(out var state);

                        var volume = 1f;
                        var muted = false;
                        if (control is ISimpleAudioVolume simpleVolume)
                        {
                            simpleVolume.GetMasterVolume(out volume);
                            simpleVolume.GetMute(out muted);
                        }

                        if (!buckets.TryGetValue(pid, out var bucket)) buckets[pid] = bucket = new Bucket();

                        bucket.Volume = Math.Max(bucket.Volume, Math.Clamp(volume, 0f, 1f));
                        bucket.AllMuted &= muted;
                        bucket.AnyActive |= state == AudioSessionState.Active;
                    }
                    catch
                    {
                        // 单个会话失败不影响整体
                    }
                    finally
                    {
                        if (control is not null) Marshal.ReleaseComObject(control);
                    }
                }
            }
            catch
            {
                // 忽略
            }
            finally
            {
                if (sessions is not null) Marshal.ReleaseComObject(sessions);
            }

            return true;
        }, false);

        return buckets.Select(pair => Build(pair.Key, pair.Value)).ToList();
    }

    public bool SetProcessMute(int pid, bool muted)
    {
        if (!OperatingSystem.IsWindows()) return false;

        return WasapiNative.WithSessionManager(manager =>
        {
            var changed = false;
            IAudioSessionEnumerator? sessions = null;
            try
            {
                if (manager.GetSessionEnumerator(out sessions) != 0 || sessions is null) return false;
                if (sessions.GetCount(out var count) != 0) return false;

                for (var i = 0; i < count; i++)
                {
                    IAudioSessionControl? control = null;
                    try
                    {
                        if (sessions.GetSession(i, out control) != 0 || control is null) continue;
                        if (control is not IAudioSessionControl2 control2) continue;
                        if (control2.GetProcessId(out var sessionPid) != 0 || sessionPid != (uint)pid) continue;

                        if (control is ISimpleAudioVolume volume && volume.SetMute(muted, IntPtr.Zero) == 0)
                        {
                            changed = true;
                        }
                    }
                    catch
                    {
                        // 单个会话失败不影响其他
                    }
                    finally
                    {
                        if (control is not null) Marshal.ReleaseComObject(control);
                    }
                }
            }
            catch
            {
                // 忽略
            }
            finally
            {
                if (sessions is not null) Marshal.ReleaseComObject(sessions);
            }

            return changed;
        }, false);
    }

    private sealed class Bucket
    {
        public float Volume;
        public bool AllMuted = true;
        public bool AnyActive;
    }

    private static AppSession Build(uint pid, Bucket bucket)
    {
        var exePath = ProcessInfoHelper.TryGetExecutablePath(pid);
        var processName = ProcessInfoHelper.TryGetProcessName(pid);
        var displayName = ProcessInfoHelper.ResolveDisplayName(exePath, processName);

        return new AppSession
        {
            Pid = (int)pid,
            ExePath = exePath ?? string.Empty,
            ProcessName = processName,
            DisplayName = displayName,
            Volume = bucket.Volume,
            IsMuted = bucket.AllMuted,
            IsPlaying = bucket.AnyActive,
        };
    }

    // EndpointFormFactor: 1 Speakers, 2 LineLevel, 3 Headphones, 5 Headset,
    // 7 DigitalPassthrough, 8 SPDIF, 9 DigitalAudioDisplayDevice(HDMI), 10 Unknown
    private static DeviceKind ClassifyKind(string? name, uint? formFactor)    {
        var n = name ?? string.Empty;

        if (Contains(n, "VB-Audio") || Contains(n, "CABLE") || Contains(n, "Voicemeeter") ||
            Contains(n, "Virtual") || Contains(n, "虚拟"))
            return DeviceKind.Virtual;

        if (Contains(n, "Bluetooth") || Contains(n, "蓝牙")) return DeviceKind.Bluetooth;
        if (Contains(n, "USB")) return DeviceKind.Usb;
        if (Contains(n, "HDMI") || Contains(n, "Display") || Contains(n, "显示器")) return DeviceKind.Hdmi;
        if (Contains(n, "Headphone") || Contains(n, "Headset") || Contains(n, "耳机")) return DeviceKind.Headphones;

        return formFactor switch
        {
            1 or 2 => DeviceKind.Speakers,
            3 or 5 => DeviceKind.Headphones,
            6 => DeviceKind.Headphones,
            8 or 7 => DeviceKind.Spdif,
            9 => DeviceKind.Hdmi,
            _ => DeviceKind.Other,
        };
    }

    private static bool Contains(string source, string value)
        => source.Contains(value, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 取共享模式混音格式（位深/采样率/声道数）。
    /// 取不到就返回 null —— 界面上宁可不显示，也不编一个数字。
    /// </summary>
    private static string? TryGetMixFormat(IMMDevice device)
    {
        object? clientObject = null;
        var format = IntPtr.Zero;

        try
        {
            var iid = WasapiNative.IID_IAudioClient;
            if (device.Activate(ref iid, WasapiNative.CLSCTX_ALL, IntPtr.Zero, out clientObject) != 0 ||
                clientObject is null)
            {
                return null;
            }

            if (((IAudioClient)clientObject).GetMixFormat(out format) != 0 || format == IntPtr.Zero)
            {
                return null;
            }

            var waveFormat = Marshal.PtrToStructure<WaveFormatEx>(format);

            var text = AudioFormat.Describe(
                waveFormat.BitsPerSample,
                (int)waveFormat.SamplesPerSec,
                waveFormat.Channels,
                isFloat: IsFloatFormat(waveFormat, format));

            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch
        {
            return null;
        }
        finally
        {
            // GetMixFormat 用 CoTaskMem 分配，必须释放
            if (format != IntPtr.Zero) Marshal.FreeCoTaskMem(format);
            if (clientObject is not null) Marshal.ReleaseComObject(clientObject);
        }
    }

    private const ushort WaveFormatExtensible = 0xFFFE;
    private const ushort WaveFormatIeeeFloat = 3;

    private static readonly Guid IeeeFloatSubFormat = new("00000003-0000-0010-8000-00AA00389B71");
    private static readonly Guid PcmSubFormat = new("00000001-0000-0010-8000-00AA00389B71");

    /// <summary>
    /// 判断是不是浮点格式。
    ///
    /// 【为什么不能只看 FormatTag】共享模式下端点普遍报 WAVE_FORMAT_EXTENSIBLE(0xFFFE)，
    /// 真实的 PCM/浮点在 WAVEFORMATEXTENSIBLE.SubFormat 里 —— 只看 FormatTag 会
    /// 把「32 bit float」显示成「32 bit」，这是看着对其实误导的数据。
    /// SubFormat 的偏移：WAVEFORMATEX(18) + wValidBitsPerSample(2) + dwChannelMask(4) = 24。
    /// </summary>
    private static bool IsFloatFormat(WaveFormatEx waveFormat, IntPtr formatPointer)
    {
        if (waveFormat.FormatTag == WaveFormatIeeeFloat) return true;
        if (waveFormat.FormatTag != WaveFormatExtensible) return false;

        try
        {
            var subFormat = Marshal.PtrToStructure<Guid>(IntPtr.Add(formatPointer, 24));
            if (subFormat == IeeeFloatSubFormat) return true;
            if (subFormat == PcmSubFormat) return false;

            // 非标准子格式：不做猜测
            return false;
        }
        catch
        {
            return false;
        }
    }
}
