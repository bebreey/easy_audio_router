using System.Runtime.InteropServices;
using System.Runtime.Versioning;

// ============================================================================
// WASAPI / Core Audio 互操作声明（Windows 后端专用）
// 接口方法顺序 = COM vtable 槽位顺序，必须与 audiopolicy.h / mmdeviceapi.h 一致。
// ============================================================================

namespace AudioRouter.Core.Backends.Windows;

internal enum EDataFlow
{
    eRender = 0,
    eCapture = 1,
    eAll = 2,
}

internal enum ERole
{
    eConsole = 0,
    eMultimedia = 1,
    eCommunications = 2,
}

[Flags]
internal enum DeviceState : uint
{
    Active = 0x00000001,
    Disabled = 0x00000002,
    NotPresent = 0x00000004,
    Unplugged = 0x00000008,
    All = 0x0000000F,
}

internal enum AudioSessionState
{
    Inactive = 0,
    Active = 1,
    Expired = 2,
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal struct PropertyKey
{
    public Guid FormatId;
    public uint PropertyId;

    public PropertyKey(Guid formatId, uint propertyId)
    {
        FormatId = formatId;
        PropertyId = propertyId;
    }

    public static PropertyKey DeviceFriendlyName =>
        new(new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), 14);

    public static PropertyKey DeviceDesc =>
        new(new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), 2);

    public static PropertyKey AudioEndpointFormFactor =>
        new(new Guid("1DA5D803-D492-4EDD-8C23-E0C0FFEE7F0E"), 0);
}

/// <summary>
/// PROPVARIANT：vt(2) + 3 个保留 WORD = 8 字节头部，其后是 union。
///
/// 【关键】union 内含 DECIMAL（16 字节），因此原生 sizeof(PROPVARIANT) 在 x64 上为 24。
/// 只声明到 IntPtr（16 字节）会让 IPropertyStore::GetValue 写回时溢出 8 字节，
/// 直接触发不可捕获的 AccessViolationException。
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 24)]
internal struct PropVariant
{
    public const ushort VT_LPWSTR = 31;
    public const ushort VT_UI4 = 19;

    [FieldOffset(0)] public ushort Vt;
    [FieldOffset(8)] public IntPtr PointerValue;
    [FieldOffset(8)] public uint UInt32Value;
}

[ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    [PreserveSig]
    int EnumAudioEndpoints(EDataFlow dataFlow, DeviceState stateMask, out IMMDeviceCollection devices);

    [PreserveSig]
    int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice endpoint);

    [PreserveSig]
    int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);

    [PreserveSig]
    int RegisterEndpointNotificationCallback(IntPtr client);

    [PreserveSig]
    int UnregisterEndpointNotificationCallback(IntPtr client);
}

[ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceCollection
{
    [PreserveSig]
    int GetCount(out uint count);

    [PreserveSig]
    int Item(uint index, out IMMDevice device);
}

[ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    [PreserveSig]
    int Activate(ref Guid iid, uint clsCtx, IntPtr activationParams,
                 [MarshalAs(UnmanagedType.IUnknown)] out object instance);

    [PreserveSig]
    int OpenPropertyStore(uint stgmAccess, out IPropertyStore properties);

    [PreserveSig]
    int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);

    [PreserveSig]
    int GetState(out DeviceState state);
}

[ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPropertyStore
{
    [PreserveSig]
    int GetCount(out uint count);

    [PreserveSig]
    int GetAt(uint index, out PropertyKey key);

    [PreserveSig]
    int GetValue(ref PropertyKey key, out PropVariant value);

    [PreserveSig]
    int SetValue(ref PropertyKey key, ref PropVariant value);

    [PreserveSig]
    int Commit();
}

[ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioSessionManager2
{
    // ---- IAudioSessionManager ----
    [PreserveSig]
    int GetAudioSessionControl(IntPtr audioSessionGuid, uint streamFlags, out IAudioSessionControl sessionControl);

    [PreserveSig]
    int GetSimpleAudioVolume(IntPtr audioSessionGuid, uint streamFlags, out ISimpleAudioVolume audioVolume);

    // ---- IAudioSessionManager2 ----
    [PreserveSig]
    int GetSessionEnumerator(out IAudioSessionEnumerator sessionEnumerator);

    [PreserveSig]
    int RegisterSessionNotification(IntPtr sessionNotification);

    [PreserveSig]
    int UnregisterSessionNotification(IntPtr sessionNotification);

    [PreserveSig]
    int RegisterDuckNotification([MarshalAs(UnmanagedType.LPWStr)] string sessionId, IntPtr duckNotification);

    [PreserveSig]
    int UnregisterDuckNotification(IntPtr duckNotification);
}

[ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioSessionEnumerator
{
    [PreserveSig]
    int GetCount(out int sessionCount);

    [PreserveSig]
    int GetSession(int index, out IAudioSessionControl session);
}

[ComImport, Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioSessionControl
{
    [PreserveSig]
    int GetState(out AudioSessionState state);

    [PreserveSig]
    int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string displayName);

    [PreserveSig]
    int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string value, IntPtr eventContext);

    [PreserveSig]
    int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string iconPath);

    [PreserveSig]
    int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string value, IntPtr eventContext);

    [PreserveSig]
    int GetGroupingParam(out Guid groupingParam);

    [PreserveSig]
    int SetGroupingParam(IntPtr groupingParam, IntPtr eventContext);

    [PreserveSig]
    int RegisterAudioSessionNotification(IntPtr newNotifications);

    [PreserveSig]
    int UnregisterAudioSessionNotification(IntPtr newNotifications);
}

[ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioSessionControl2
{
    // ---- IAudioSessionControl ----
    [PreserveSig]
    int GetState(out AudioSessionState state);

    [PreserveSig]
    int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string displayName);

    [PreserveSig]
    int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string value, IntPtr eventContext);

    [PreserveSig]
    int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string iconPath);

    [PreserveSig]
    int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string value, IntPtr eventContext);

    [PreserveSig]
    int GetGroupingParam(out Guid groupingParam);

    [PreserveSig]
    int SetGroupingParam(IntPtr groupingParam, IntPtr eventContext);

    [PreserveSig]
    int RegisterAudioSessionNotification(IntPtr newNotifications);

    [PreserveSig]
    int UnregisterAudioSessionNotification(IntPtr newNotifications);

    // ---- IAudioSessionControl2 ----
    [PreserveSig]
    int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string sessionIdentifier);

    [PreserveSig]
    int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string sessionInstanceIdentifier);

    [PreserveSig]
    int GetProcessId(out uint processId);

    /// <summary>S_OK(0) = 系统声音会话。</summary>
    [PreserveSig]
    int IsSystemSoundsSession();

    [PreserveSig]
    int SetDuckingPreference([MarshalAs(UnmanagedType.Bool)] bool optOut);
}

[ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ISimpleAudioVolume
{
    [PreserveSig]
    int SetMasterVolume(float level, IntPtr eventContext);

    [PreserveSig]
    int GetMasterVolume(out float level);

    [PreserveSig]
    int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, IntPtr eventContext);

    [PreserveSig]
    int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct WaveFormatEx
{
    public ushort FormatTag;
    public ushort Channels;
    public uint SamplesPerSec;
    public uint AvgBytesPerSec;
    public ushort BlockAlign;
    public ushort BitsPerSample;
    public ushort ExtraSize;
}

/// <summary>
/// IAudioClient —— 只用 <c>GetMixFormat</c>（vtable 第 8 槽，即 IUnknown 之后的第 6 个方法），
/// 因此前面 5 个方法的声明顺序不能变动。
/// 注意：Activate 一个共享模式的 IAudioClient **不启动音频流**，也不需要管理员权限。
/// </summary>
[ComImport, Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioClient
{
    [PreserveSig]
    int Initialize(int shareMode, int streamFlags, long bufferDuration, long periodicity,
                   IntPtr format, IntPtr sessionGuid);

    [PreserveSig]
    int GetBufferSize(out uint bufferFrameCount);

    [PreserveSig]
    int GetStreamLatency(out long latency);

    [PreserveSig]
    int GetCurrentPadding(out uint padding);

    [PreserveSig]
    int IsFormatSupported(int shareMode, IntPtr format, out IntPtr closestMatch);

    [PreserveSig]
    int GetMixFormat(out IntPtr deviceFormat);
}

/// <summary>
/// Windows 专有：显式声明平台约束，让分析器（CA1416）在跨平台的 Core 里守住边界，
/// 而不是靠屏蔽警告糊过去。调用方必须先做 OperatingSystem.IsWindows() 判定。
/// </summary>
[SupportedOSPlatform("windows")]
internal static class WasapiNative
{
    public const uint CLSCTX_ALL = 0x17;
    public const uint STGM_READ = 0x00000000;

    public static readonly Guid CLSID_MMDeviceEnumerator = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
    public static readonly Guid IID_IAudioSessionManager2 = new("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F");
    public static readonly Guid IID_IAudioMeterInformation = new("C02216F6-8C67-4B5B-9D00-D008E73E0064");
    public static readonly Guid IID_IAudioClient = new("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2");

    [DllImport("ole32.dll")]
    public static extern int PropVariantClear(ref PropVariant pv);

    public static IMMDeviceEnumerator CreateEnumerator()
    {
        var type = Type.GetTypeFromCLSID(CLSID_MMDeviceEnumerator, throwOnError: true)!;
        return (IMMDeviceEnumerator)Activator.CreateInstance(type)!;
    }

    public static string? ReadString(IPropertyStore store, PropertyKey key)
    {
        PropVariant pv = default;
        try
        {
            if (store.GetValue(ref key, out pv) != 0) return null;
            if (pv.Vt != PropVariant.VT_LPWSTR || pv.PointerValue == IntPtr.Zero) return null;
            return Marshal.PtrToStringUni(pv.PointerValue);
        }
        catch
        {
            return null;
        }
        finally
        {
            if (pv.Vt != 0) PropVariantClear(ref pv);
        }
    }

    public static uint? ReadUInt32(IPropertyStore store, PropertyKey key)
    {
        PropVariant pv = default;
        try
        {
            if (store.GetValue(ref key, out pv) != 0) return null;
            return pv.Vt == PropVariant.VT_UI4 ? pv.UInt32Value : null;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (pv.Vt != 0) PropVariantClear(ref pv);
        }
    }

    /// <summary>按会话维度访问默认渲染设备，body 执行完统一释放 COM 对象。</summary>
    /// <summary>
    /// 遍历**所有在用的渲染设备**，对每个设备激活会话管理器并执行 <paramref name="body"/>。
    ///
    /// 为什么必须有它：<see cref="WithSessionManager"/> 只看默认设备（GetDefaultAudioEndpoint），
    /// 但"哪些应用在出声"与"谁是默认设备"无关。插上耳机时 Windows 会把默认设备切过去，
    /// 那些音频仍在原设备上的应用就会整片从会话列表里消失（用户实测过）。
    ///
    /// 只枚举 ACTIVE 端点：失效/已拔出的端点拿不到会话管理器，逐个 Activate 纯属浪费。
    /// </summary>
    public static void ForEachRenderSessionManager(Func<IAudioSessionManager2, bool> body)
    {
        const uint DeviceStateActive = 0x1;

        IMMDeviceEnumerator? enumerator = null;
        IMMDeviceCollection? devices = null;

        try
        {
            enumerator = CreateEnumerator();

            if (enumerator.EnumAudioEndpoints(EDataFlow.eRender, (DeviceState)DeviceStateActive, out devices) != 0 ||
                devices is null)
            {
                return;
            }

            if (devices.GetCount(out var count) != 0) return;

            for (uint i = 0; i < count; i++)
            {
                IMMDevice? device = null;
                object? managerObject = null;

                try
                {
                    if (devices.Item(i, out device) != 0 || device is null) continue;

                    var iid = IID_IAudioSessionManager2;
                    if (device.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out managerObject) != 0 || managerObject is null)
                    {
                        continue;
                    }

                    body((IAudioSessionManager2)managerObject);
                }
                catch
                {
                    // 单个设备失败不影响其他设备
                }
                finally
                {
                    if (managerObject is not null) Marshal.ReleaseComObject(managerObject);
                    if (device is not null) Marshal.ReleaseComObject(device);
                }
            }
        }
        catch
        {
            // 交给上层
        }
        finally
        {
            if (devices is not null) Marshal.ReleaseComObject(devices);
            if (enumerator is not null) Marshal.ReleaseComObject(enumerator);
        }
    }
    public static T WithSessionManager<T>(Func<IAudioSessionManager2, T> body, T fallback)
    {
        IMMDeviceEnumerator? enumerator = null;
        IMMDevice? device = null;
        object? managerObject = null;

        try
        {
            enumerator = CreateEnumerator();

            if (enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eConsole, out device) != 0 ||
                device is null)
            {
                return fallback;
            }

            var iid = IID_IAudioSessionManager2;
            if (device.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out managerObject) != 0 || managerObject is null)
            {
                return fallback;
            }

            return body((IAudioSessionManager2)managerObject);
        }
        catch
        {
            return fallback;
        }
        finally
        {
            if (managerObject is not null) Marshal.ReleaseComObject(managerObject);
            if (device is not null) Marshal.ReleaseComObject(device);
            if (enumerator is not null) Marshal.ReleaseComObject(enumerator);
        }
    }
}
