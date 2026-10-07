using System;
using System.Runtime.InteropServices;

namespace AudioRouter.Core.Backends.Windows;

/// <summary>
/// 注入式原生核心期望的内存布局（<c>Local\audio-router-file</c> 映射的内容）。
///
/// 这份实现是**逐字节复刻**上游 C++ 的：
///   audio-router-gui/routing_params.h   → 结构体定义
///   audio-router-gui/routing_params.cpp → global_size / serialize（指针存"相对 blob 起始的偏移"）
///   audio-router-gui/app_inject.cpp     → session_guid_and_flag 的算法（高 2 位是标志）
///
/// 上游用 <c>assert(pointer == headers_size &amp;&amp; names_pointer == full_size)</c> 自我校验，
/// 而这两个值都能由下面的常量算出来 —— 所以布局是可以推导验证的，不是猜的：
///   结构体数组（N × sizeof）+ 名字区（每个字符串 (length+1)×2 字节）
/// </summary>
internal static class NativeRoutingBlob
{
    // ---- 与 C++ __declspec 默认对齐（/Zp8）一致的结构体布局 ----
    //
    // struct global_routing_params {          // sizeof == 40
    //     BYTE   version;                     // @0   （后跟 7 字节填充，因为下一个成员要 8 字节对齐）
    //     uint64 module_name_ptr;             // @8
    //     local_routing_params local;         // @16
    //         DWORD  pid;                     //     @16
    //         DWORD  session_guid_and_flag;    //    @20
    //         uint64 device_id_ptr;           //    @24
    //     uint64 next_global_ptr;             // @32
    // };
    internal const int StructSize = 40;
    internal const int OffsetVersion = 0;
    internal const int OffsetModuleNamePtr = 8;
    internal const int OffsetPid = 16;
    internal const int OffsetSessionGuidAndFlag = 20;
    internal const int OffsetDeviceIdPtr = 24;
    internal const int OffsetNextGlobalPtr = 32;

    // ---- session_guid_and_flag：高 2 位是标志，其余是会话 GUID ----
    internal const uint FlagUnload = 0;
    internal const uint FlagRoute = 1;
    internal const uint FlagDuplicate = 2;

    private const int FlagShift = 30;               // sizeof(DWORD) * 8 - 2
    private const uint FlagMask = 3u << FlagShift;

    /// <summary>会话 GUID 从 1&lt;&lt;5 起步，每次注入自增（与上游 app_inject.cpp 一致）。</summary>
    private static uint _sessionGuid = 1 << 5;

    /// <summary>对应上游的 MAKE_SESSION_GUID_AND_FLAG 宏。</summary>
    internal static uint MakeSessionGuidAndFlag(uint guid, uint flag)
        => (flag << FlagShift) | (guid & ~FlagMask);

    /// <summary>取下一条会话 GUID 并自增（每次注入都要一个新的，否则 DLL 会把两次当成同一次）。</summary>
    internal static uint NextSessionGuid() => _sessionGuid++;

    /// <summary>仅为测试注入初始值，避免用例之间互相影响。</summary>
    internal static void ResetSessionGuidForTests(uint value = 1 << 5) => _sessionGuid = value;

    internal static uint BuildFlag(bool duplicate) => duplicate ? FlagDuplicate : FlagRoute;

    /// <summary>blob 的总字节数：结构体数组 + 设备 ID 字符串（UTF-16 + 结束符）。</summary>
    internal static int Size(uint pid, string? endpointId)
        => StructSize + (string.IsNullOrEmpty(endpointId) ? 0 : (endpointId!.Length + 1) * 2);

    /// <summary>
    /// 生成 blob。
    ///
    /// <paramref name="sessionGuidAndFlag"/> 传 <see cref="FlagUnload"/> 时是"卸载"语义
    /// （上游用 device_id_ptr = NULL + flag = 0 表示撤销路由）。
    /// </summary>
    internal static byte[] Build(uint pid, string? endpointId, uint sessionGuidAndFlag)
    {
        var useDevice = sessionGuidAndFlag != FlagUnload && !string.IsNullOrEmpty(endpointId);
        var size = StructSize + (useDevice ? (endpointId!.Length + 1) * 2 : 0);
        var blob = new byte[size];

        // version = 0（上游 routing_params.version = 0）
        blob[OffsetVersion] = 0;

        // module_name_ptr = NULL：只有 bootstrapper 那条路才会用到（而发布包里没有 bootstrapper*dll）
        WriteUInt64(blob, OffsetModuleNamePtr, 0);

        WriteUInt32(blob, OffsetPid, pid);
        WriteUInt32(blob, OffsetSessionGuidAndFlag, sessionGuidAndFlag);

        // device_id_ptr 存的是**偏移**，不是真实指针（读取方 rebase() 时再加上基址）
        WriteUInt64(blob, OffsetDeviceIdPtr, (ulong)(useDevice ? StructSize : 0));

        // next_global_ptr = NULL：单条记录（上游此路径也只写一条）
        WriteUInt64(blob, OffsetNextGlobalPtr, 0);

        if (useDevice)
        {
            var bytes = System.Text.Encoding.Unicode.GetBytes(endpointId!);
            Buffer.BlockCopy(bytes, 0, blob, StructSize, bytes.Length);
            // 末尾两字节保持 0 = UTF-16 的字符串结束符
        }

        return blob;
    }

    private static void WriteUInt32(byte[] buffer, int offset, uint value)
        => MemoryMarshal.Write(buffer.AsSpan(offset, sizeof(uint)), in value);

    private static void WriteUInt64(byte[] buffer, int offset, ulong value)
        => MemoryMarshal.Write(buffer.AsSpan(offset, sizeof(ulong)), in value);
}
