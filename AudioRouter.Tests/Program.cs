using System.Text;
using AudioRouter.Core.Backends;
using AudioRouter.Core.Backends.Linux;
using AudioRouter.Core.Backends.Windows;
using AudioRouter.Core.Formatting;
using AudioRouter.Core.Localization;
using AudioRouter.Core.Models;
using AudioRouter.Core.Routing;

namespace AudioRouter.Tests;

/// <summary>
/// 零依赖测试运行器。
///
/// 存在理由：项目里最危险的两块逻辑 —— **pactl 输出解析**与**「已生效 vs 仅记录」的区分** ——
/// 都不该靠"看起来对"来交付；而 Linux 后端在当前机器上跑不了。
/// 所以：解析器做成纯函数 + 用录制的真实输出在本机验证；路由语义用测试替身验证。
///
/// 退出码 0 = 全部通过，1 = 有失败（可直接接进 CI）。
/// </summary>
internal static class Program
{
    private static int _passed;
    private static int _failed;

    private static int Main(string[] args)
    {
        // 手工诊断入口：在真实进程上验证注入链路。
        // 不放进单元测试的理由很直接 —— 它会真的往别人的进程里注入 DLL。
        if (args.Length > 0 && args[0] == "--inject-spike")
        {
            return RunInjectSpike(args);
        }

        try
        {
            Console.OutputEncoding = Encoding.UTF8;
        }
        catch
        {
            // 忽略
        }

        // 测试进程不写应用日志：否则测试产生的 reconcile/route 记录会混进真实诊断
        AudioRouter.Core.Diagnostics.StartupLog.SuppressFileOutput();

        Console.WriteLine("Audio Router — core logic tests");
        Console.WriteLine();

        TestSinkParsing();
        TestAudioFormat();
        TestSinkInputParsing();
        TestVolumeAveraging();
        TestDefaultSinkParsing();
        TestLanguagePackParsing();
        TestLanguagePackEncoding();
        TestRouteStorePersistence();
        TestRouteStoreDurability();
        TestNativeRoutingBlob();
        TestUnrouteReappliesRemaining();
        TestRouteKey();
        TestRouteReconciler();
        TestDeviceRouteNotifications();
        TestRoutingHonesty();
        TestBackendCapabilityDeclaration();
        TestLinuxBackendCommands();
        TestProcessRunner();
        TestTextWidth();

        Console.WriteLine();
        Console.WriteLine($"{_passed} passed, {_failed} failed");
        return _failed == 0 ? 0 : 1;
    }

    // ======================================================================
    //  pactl 解析（用录制的真实输出）
    // ======================================================================

    /// <summary>
    /// 注入可行性验证（spike）。
    ///
    /// 用法：<c>AudioRouter.Tests.exe --inject-spike &lt;native目录&gt; &lt;pid&gt; &lt;deviceId|unload&gt; [duplicate]</c>
    ///
    /// 为什么单列一个入口：验证"参数能不能被原生核心接受"必须在**真实进程**上做，
    /// 而单元测试不该去注入别人的进程。所以做成需要显式参数才触发的手工工具。
    /// </summary>
    private static int RunInjectSpike(string[] args)
    {
        if (args.Length < 4)
        {
            Console.WriteLine("usage: --inject-spike <nativeDir> <pid> <deviceId|unload> [duplicate] [baseDeviceId]");
            return 2;
        }

        var nativeDirectory = args[1];
        var pid = int.Parse(args[2]);
        var deviceId = args[3] == "unload" ? null : args[3];
        var duplicate = args.Length > 4 && args[4] == "duplicate";

        // 复制模式下第一次下发必须先给基准设备（见 NativeInjector 里的说明）
        var baseDeviceId = args.Length > 5 ? args[5] : null;

        // spike 里手工指定原生核心目录（正常运行时由 NativeCoreProbe 自己找）
        NativeCoreProbe.OverrideForTests(nativeDirectory, hasX86: true, hasX64: true);

        var result = NativeRouterApply(nativeDirectory, pid, deviceId, duplicate, baseDeviceId);

        Console.WriteLine("toolchain : " + nativeDirectory);
        Console.WriteLine("pid       : " + pid);
        Console.WriteLine("device    : " + (deviceId ?? "(unload)"));
        Console.WriteLine("duplicate : " + duplicate);
        Console.WriteLine("baseDevice: " + (baseDeviceId ?? "(none)"));
        Console.WriteLine(result.Ok ? "INJECT OK" : "INJECT FAIL: " + result.Message);

        return result.Ok ? 0 : 1;
    }

    private static AudioRouter.Core.Backends.Windows.NativeInjectionResult NativeRouterApply(
        string nativeDirectory, int pid, string? deviceId, bool duplicate, string? baseDeviceId)
        => AudioRouter.Core.Backends.Windows.NativeInjector.Apply(
            nativeDirectory, pid, deviceId, duplicate, baseDeviceId);

    /// <summary>
    /// 原生路由 blob 的字节布局。
    ///
    /// 这些断言是照着**已经被真实 DLL 接受**的那份实现钉下来的，不是照文档抄的：
    /// 用这份布局注入一个真实播放进程后，指定默认设备 → 会话出现在默认设备；
    /// 指定虚拟声卡 → 会话从默认设备消失。所以布局正确性有外部证据支撑。
    ///
    /// 上游在 serialize() 里用 assert(pointer == headers_size &amp;&amp; names_pointer == full_size)
    /// 自我校验，而这两个值都能由下面的常量算出来 —— 布局因此是可推导、可验证的。
    /// </summary>
    private static void TestNativeRoutingBlob()
    {
        const string device = "{0.0.0.00000000}.{8d5b2917-2555-4a1c-80dd-b74a7f2ce929}";

        Check("blob: struct size is 40 (x64 padding included)", NativeRoutingBlob.StructSize == 40);
        Check("blob: offsets match routing_params.h",
            NativeRoutingBlob.OffsetVersion == 0 &&
            NativeRoutingBlob.OffsetModuleNamePtr == 8 &&
            NativeRoutingBlob.OffsetPid == 16 &&
            NativeRoutingBlob.OffsetSessionGuidAndFlag == 20 &&
            NativeRoutingBlob.OffsetDeviceIdPtr == 24 &&
            NativeRoutingBlob.OffsetNextGlobalPtr == 32);

        var blob = NativeRoutingBlob.Build(4242, device, NativeRoutingBlob.MakeSessionGuidAndFlag(32, 1));

        Check("blob: total size = struct + UTF-16 name + NUL",
            blob.Length == 40 + (device.Length + 1) * 2, blob.Length.ToString());
        Check("blob: version is 0", blob[0] == 0);
        Check("blob: module_name_ptr is NULL", BitConverter.ToUInt64(blob, 8) == 0);
        Check("blob: pid at offset 16", BitConverter.ToUInt32(blob, 16) == 4242);
        Check("blob: session flag at offset 20", BitConverter.ToUInt32(blob, 20) == 0x40000020);
        Check("blob: device_id_ptr is an OFFSET (struct size), not a real pointer",
            BitConverter.ToUInt64(blob, 24) == 40);
        Check("blob: next_global_ptr is NULL", BitConverter.ToUInt64(blob, 32) == 0);
        Check("blob: device id stored as UTF-16 right after the struct",
            Encoding.Unicode.GetString(blob, 40, device.Length * 2) == device);
        Check("blob: name is NUL terminated", blob[^1] == 0);

        // 标志位编码：高 2 位是标志（与上游 MAKE_SESSION_GUID_AND_FLAG 逐位一致）
        Check("blob: flag 1 (route) lives in the top 2 bits",
            NativeRoutingBlob.MakeSessionGuidAndFlag(32, 1) == 0x40000020);
        Check("blob: flag 2 (duplicate)",
            NativeRoutingBlob.MakeSessionGuidAndFlag(32, 2) == 0x80000020);
        Check("blob: guid is masked out of the flag bits",
            NativeRoutingBlob.MakeSessionGuidAndFlag(0xFFFFFFFF, 1) == 0x7FFFFFFF);

        // 卸载：flag=0 且不带设备 ID（上游用这个表示 revert）
        var unload = NativeRoutingBlob.Build(4242, device, NativeRoutingBlob.FlagUnload);
        Check("blob: unload has no device id", unload.Length == 40);
        Check("blob: unload sets device_id_ptr to NULL", BitConverter.ToUInt64(unload, 24) == 0);

        // 会话 GUID 必须每次递增：同一个 GUID 重复注入会被 DLL 当成同一次
        NativeRoutingBlob.ResetSessionGuidForTests();
        Check("blob: session guid starts at 1<<5", NativeRoutingBlob.NextSessionGuid() == 32);
        Check("blob: session guid increments", NativeRoutingBlob.NextSessionGuid() == 33);

        Check("blob: route mode maps to flag 1", NativeRoutingBlob.BuildFlag(false) == 1);
        Check("blob: duplicate mode maps to flag 2", NativeRoutingBlob.BuildFlag(true) == 2);
    }

    /// <summary>
    /// 卸载是整条撤销（flag=0 撤掉该进程所有补丁），所以移除一条路由后必须把**剩下的**重新下发，
    /// 否则它们会静默失效直到应用重启。这条断言钉住那个行为 —— 它不是推测，
    /// 对应真机日志里的 `unroute: ... reapplied=1`。
    /// </summary>
    private static void TestUnrouteReappliesRemaining()
    {
        var path = Path.Combine(Path.GetTempPath(), $"audio-router-reapply-{Guid.NewGuid():N}.json");

        try
        {
            var store = new RouteStore(path);
            var backend = new RecordingRouteBackend();
            var service = new RoutingService(store, backend);

            const string exe = @"C:\Apps\Music.exe";
            var key = RouteKey.For(exe, "Music");

            service.Route(new RouteRecord { ExePath = exe, ProcessName = "Music", DeviceId = "sink-a", LastPid = 4242 });
            service.Route(new RouteRecord
            {
                ExePath = exe, ProcessName = "Music", DeviceId = "sink-b",
                Mode = RouteMode.Duplicate, LastPid = 4242,
            });

            backend.Applied.Clear();
            backend.Removed.Clear();

            var outcome = service.Unroute(key, "sink-a");

            Check("reapply: unroute reports applied", outcome == RoutingOutcome.Applied, outcome.ToString());
            Check("reapply: unloaded the live instance", backend.Removed.Contains(4242), string.Join(",", backend.Removed));
            Check("reapply: remaining route was re-dispatched", backend.Applied.Count == 1, backend.Applied.Count.ToString());
            Check("reapply: and it is the other device",
                backend.Applied.Count == 1 && backend.Applied[0].EndsWith("sink-b"), string.Join(",", backend.Applied));
            Check("reapply: the removed one is gone from the store", store.Find(key).Count == 1);
        }
        finally
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { /* 忽略 */ }
        }
    }

    /// <summary>记录 ApplyRoute / RemoveRoute 调用的后端替身（供上面那条断言使用）。</summary>
    private sealed class RecordingRouteBackend : IAudioBackend
    {
        public List<string> Applied { get; } = new();
        public List<int> Removed { get; } = new();

        public string Name => "recording";
        public bool IsAvailable => true;
        public string? Limitation => null;
        public bool SupportsRouting => true;
        public bool SupportsDuplication => true;

        public IReadOnlyList<AudioDevice> EnumerateDevices() => Array.Empty<AudioDevice>();

        /// <summary>让服务层能按 exe 路径找到"活着的实例"。</summary>
        public IReadOnlyList<AppSession> EnumerateSessions() => new[]
        {
            new AppSession { Pid = 4242, ExePath = @"C:\Apps\Music.exe", ProcessName = "Music" },
        };

        public bool SetProcessMute(int pid, bool muted) => false;

        public RoutingOutcome ApplyRoute(int pid, string deviceId, RouteMode mode)
        {
            Applied.Add($"{pid}:{deviceId}");
            return RoutingOutcome.Applied;
        }

        public RoutingOutcome RemoveRoute(int pid, string deviceId)
        {
            Removed.Add(pid);
            return RoutingOutcome.Applied;
        }
    }
    private static void TestSinkParsing()
    {
        var sinks = PactlParser.ParseSinks(SampleData.Sinks, "alsa_output.pci-0000_00_1f.3.analog-stereo");

        Check("sinks: count", sinks.Count == 2, $"got {sinks.Count}");
        Check("sinks: name", sinks[0].Name == "alsa_output.pci-0000_00_1f.3.analog-stereo", sinks[0].Name);
        Check("sinks: description", sinks[0].Description == "Built-in Audio Analog Stereo", sinks[0].Description);
        Check("sinks: state", sinks[0].State == "RUNNING", sinks[0].State);
        Check("sinks: not muted", !sinks[0].Muted);
        Check("sinks: bluetooth muted", sinks[1].Muted);
        Check("sinks: index parsed", sinks[1].Index == 5, sinks[1].Index.ToString());

        // sample_spec → 格式摘要（设备卡与 CLI 的 Format 列都靠这条链路）
        Check("sinks: sample_spec format parsed", sinks[0].SampleFormat == "s16le", sinks[0].SampleFormat);
        Check("sinks: sample_spec rate parsed", sinks[0].SampleRate == 48000, sinks[0].SampleRate.ToString());
        Check("sinks: sample_spec channels parsed", sinks[0].Channels == 2, sinks[0].Channels.ToString());
        Check("sinks: format summary composed",
            AudioFormat.Describe(
                AudioFormat.BitsFromSampleFormat(sinks[0].SampleFormat),
                sinks[0].SampleRate,
                sinks[0].Channels,
                AudioFormat.IsFloatSampleFormat(sinks[0].SampleFormat)) == "16 bit · 48 kHz · 2ch");
        Check("sinks: float format flagged", AudioFormat.IsFloatSampleFormat(sinks[1].SampleFormat));
    }

    private static void TestAudioFormat()
    {
        Check("format: 24bit/48k/2ch", AudioFormat.Describe(24, 48000, 2) == "24 bit · 48 kHz · 2ch",
            AudioFormat.Describe(24, 48000, 2));
        Check("format: 44.1 kHz keeps one decimal",
            AudioFormat.Describe(16, 44100, 2) == "16 bit · 44.1 kHz · 2ch",
            AudioFormat.Describe(16, 44100, 2));
        Check("format: float labelled",
            AudioFormat.Describe(32, 48000, 1, isFloat: true) == "32 bit float · 48 kHz · 1ch",
            AudioFormat.Describe(32, 48000, 1, isFloat: true));
        Check("format: unknown bits omitted", AudioFormat.Describe(0, 48000, 2) == "48 kHz · 2ch",
            AudioFormat.Describe(0, 48000, 2));
        Check("format: nothing known → empty (no made-up numbers)",
            AudioFormat.Describe(0, 0, 0) == string.Empty);
        Check("format: s24_3le → 24", AudioFormat.BitsFromSampleFormat("s24_3le") == 24);
        Check("format: float32le → 32", AudioFormat.BitsFromSampleFormat("float32le") == 32);
        Check("format: unknown sample format → 0", AudioFormat.BitsFromSampleFormat("nonsense") == 0);
        Check("format: null sample format → 0", AudioFormat.BitsFromSampleFormat(null) == 0);
    }

    private static void TestSinkInputParsing()
    {
        var inputs = PactlParser.ParseSinkInputs(SampleData.SinkInputs);

        // 第三条没有 application.process.id（系统流）→ 必须被跳过
        Check("sink-inputs: count (pid-less skipped)", inputs.Count == 2, $"got {inputs.Count}");

        var firefox = inputs.FirstOrDefault(i => i.Name == "Firefox");
        var spotify = inputs.FirstOrDefault(i => i.Name == "Spotify");

        Check("sink-inputs: string pid parsed", firefox?.Pid == 12345, firefox?.Pid.ToString());
        Check("sink-inputs: numeric pid parsed", spotify?.Pid == 6789, spotify?.Pid.ToString());
        Check("sink-inputs: binary", firefox?.Binary == "firefox", firefox?.Binary);
        Check("sink-inputs: sink index", firefox?.Sink == 0 && spotify?.Sink == 5);
        Check("sink-inputs: muted flag", spotify?.Muted == true);
        Check("sink-inputs: corked flag", firefox?.Corked == false);
        Check("sink-inputs: index", firefox?.Index == 42, firefox?.Index.ToString());
    }

    private static void TestVolumeAveraging()
    {
        var sinks = PactlParser.ParseSinks(SampleData.Sinks, null);

        // 100% / 100% → 1.0
        Check("volume: full", Math.Abs(sinks[0].Volume - 1.0f) < 0.001f, sinks[0].Volume.ToString("0.###"));

        // 50% / 100% → 0.75（按声道平均，而不是取第一个声道）
        Check("volume: averaged across channels",
            Math.Abs(sinks[1].Volume - 0.75f) < 0.001f,
            sinks[1].Volume.ToString("0.###"));
    }

    private static void TestDefaultSinkParsing()
    {
        Check("default sink: trims newline",
            PactlParser.ParseDefaultSink("alsa_output.speaker\n") == "alsa_output.speaker");
        Check("default sink: empty → null", PactlParser.ParseDefaultSink("  ") is null);
    }

    // ======================================================================
    //  语言包
    // ======================================================================

    private static void TestLanguagePackParsing()
    {
        var ok = LanguagePack.Parse(SampleData.ValidLanguage, "test", out var error);
        Check("language: valid parses", ok is not null, error);
        Check("language: code", ok?.Code == "xx-XX", ok?.Code);
        Check("language: name", ok?.Name == "Test Language", ok?.Name);
        Check("language: key count", ok?.Strings.Count == 2, ok?.Strings.Count.ToString());

        Check("language: missing code rejected",
            LanguagePack.Parse("""{"strings":{"a":"b"}}""", null, out _) is null);

        Check("language: missing strings rejected",
            LanguagePack.Parse("""{"code":"xx"}""", null, out _) is null);

        Check("language: empty strings rejected",
            LanguagePack.Parse("""{"code":"xx","strings":{}}""", null, out _) is null);
    }

    private static void TestLanguagePackEncoding()
    {
        var directory = Path.Combine(Path.GetTempPath(), "audio-router-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            // 1) 含非法 UTF-8 字节序列（模拟「记事本存成 ANSI」）—— 必须被明确拒绝，而不是变成乱码
            var gbkPath = Path.Combine(directory, "invalid-utf8.json");
            File.WriteAllBytes(gbkPath, SampleData.InvalidUtf8LanguageFile);

            var gbk = LanguagePack.Load(gbkPath, out var gbkError);
            Check("language: non-UTF8 rejected", gbk is null);
            Check("language: non-UTF8 error mentions UTF-8",
                gbkError?.Contains("UTF-8", StringComparison.OrdinalIgnoreCase) == true, gbkError);

            // 2) 带 BOM 的 UTF-8 —— 必须能读（BOM 要剥掉，否则 JsonDocument 会解析失败）
            var bomPath = Path.Combine(directory, "bom.json");
            File.WriteAllText(bomPath,
                """{"code":"bom-XX","name":"BOM Test","strings":{"a":"b"}}""",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            var bom = LanguagePack.Load(bomPath, out var bomError);
            Check("language: UTF-8 BOM accepted", bom is not null, bomError);
            Check("language: BOM stripped from code", bom?.Code == "bom-XX", bom?.Code);
        }
        finally
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch
            {
                // 忽略
            }
        }
    }

    // ======================================================================
    //  路由状态
    // ======================================================================

    private static void TestRouteStorePersistence()
    {
        var path = Path.Combine(Path.GetTempPath(), $"audio-router-routes-{Guid.NewGuid():N}.json");

        try
        {
            var store = new RouteStore(path);
            var record = new RouteRecord
            {
                ExePath = @"C:\Apps\Test.exe",
                ProcessName = "Test",
                DeviceId = "sink-a",
                Mode = RouteMode.Route,
            };

            Check("routes: add", store.Add(record));

            // Add() 负责补齐身份键：路由的身份是路径，不是 PID
            var key = record.Key;
            Check("routes: key derived from exe path", key == "path:c:/apps/test.exe", key);
            Check("routes: exists by key", store.Exists(key, "sink-a"));
            Check("routes: idempotent (same key+device not added twice)", !store.Add(record));
            Check("routes: count", store.All.Count == 1);

            // 重新加载：CLI 每次是独立进程，状态必须落盘
            var reloaded = RouteStore.Load(path);
            Check("routes: persisted across load", reloaded.All.Count == 1);
            Check("routes: persisted mode", reloaded.All[0].Mode == RouteMode.Route);
            Check("routes: persisted key survives round-trip", reloaded.All[0].Key == key, reloaded.All[0].Key);

            // 大小写 / 斜杠不同也必须命中同一条（Windows 路径大小写不敏感）
            Check("routes: key match is case and slash insensitive",
                reloaded.Exists(@"PATH:C:/apps/test.exe", "sink-a"));

            Check("routes: remove", reloaded.Remove(key, "sink-a"));
            Check("routes: removed not found", !RouteStore.Load(path).Exists(key, "sink-a"));
        }
        finally
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
                // 忽略
            }
        }
    }

    /// <summary>
    /// 落盘健壮性：写入必须原子，读取失败必须留证据。
    ///
    /// 背景（真实失败链）：非原子写入被强杀会留下半截 json → 下次读取解析失败 →
    /// 被当成"没有路由" → 随后一次保存就把用户记录**永久覆盖**。
    /// </summary>
    private static void TestRouteStoreDurability()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"audio-router-durability-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "routes.json");

        try
        {
            var store = new RouteStore(path);
            store.Add(new RouteRecord
            {
                ExePath = @"C:\Apps\A.exe",
                ProcessName = "A",
                DeviceId = "sink-a",
            });

            Check("durability: no .tmp left behind",
                Directory.GetFiles(dir, "routes.json.tmp").Length == 0);

            for (var i = 0; i < 20; i++) store.Save();
            Check("durability: survives repeated saves", RouteStore.Load(path).All.Count == 1);
            Check("durability: previous good generation kept in .bak",
                File.Exists(path + ".bak") && File.ReadAllText(path + ".bak").Contains("A.exe"));

            // 主文件被外部改坏
            const string broken = "{ this is not json";
            File.WriteAllText(path, broken);

            // 关键：不能"静默当没有路由"，必须能从上一代好文件恢复
            var afterCorrupt = RouteStore.Load(path);
            Check("durability: recovers the record from .bak",
                afterCorrupt.All.Count == 1, afterCorrupt.All.Count.ToString());

            var scenes = Directory.GetFiles(dir, "routes.corrupt-*.json");
            Check("durability: corrupt file kept for forensics", scenes.Length == 1, string.Join(", ", scenes));
            Check("durability: forensic copy keeps the broken bytes",
                scenes.Length == 1 && File.ReadAllText(scenes[0]) == broken);

            // 恢复之后再新增：坏文件**不能**把好备份覆盖掉
            afterCorrupt.Add(new RouteRecord
            {
                ExePath = @"C:\Apps\B.exe",
                ProcessName = "B",
                DeviceId = "sink-b",
            });

            Check("durability: recovered record and the new one are both persisted",
                RouteStore.Load(path).All.Count == 2);
            Check("durability: good generation in .bak is not clobbered by the broken file",
                File.ReadAllText(path + ".bak").Contains("A.exe"));
        }
        finally
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch
            {
                // 忽略
            }
        }
    }

    private static void TestRouteKey()
    {
        Check("key: path form", RouteKey.For(@"C:\Apps\Test.exe", "Test") == "path:c:/apps/test.exe");
        Check("key: same file, different case/slashes → same key",
            RouteKey.For(@"c:/APPS/test.EXE", "Test") == RouteKey.For(@"C:\apps\Test.exe", "Test"));
        Check("key: no path falls back to process name",
            RouteKey.For(null, "Firefox") == "name:firefox");
        Check("key: empty path falls back to process name",
            RouteKey.For("   ", "Firefox") == "name:firefox");
        Check("key: weak identity is flagged", RouteKey.IsWeak("name:firefox"));
        Check("key: path identity is not weak", !RouteKey.IsWeak("path:c:/apps/test.exe"));
        Check("key: describe strips prefix",
            RouteKey.Describe("path:c:/apps/test.exe") == "c:/apps/test.exe",
            RouteKey.Describe("path:c:/apps/test.exe"));
        Check("key: describe marks weak identity",
            RouteKey.Describe("name:firefox").Contains("name only", StringComparison.Ordinal),
            RouteKey.Describe("name:firefox"));
    }

    /// <summary>
    /// 自动套用器：这是"按 exe 路径存"的核心价值所在，必须钉死两条纪律。
    /// </summary>
    private static void TestRouteReconciler()
    {
        var path = Path.Combine(Path.GetTempPath(), $"audio-router-reconcile-{Guid.NewGuid():N}.json");

        try
        {
            var store = new RouteStore(path);
            store.Add(new RouteRecord
            {
                ExePath = @"C:\Apps\Test.exe",
                ProcessName = "Test",
                DisplayName = "Test App",
                DeviceId = "sink-a",
                Mode = RouteMode.Route,
            });

            var backend = new CountingRoutingBackend();
            var reconciler = new RouteReconciler(store, backend);

            var device = new AudioDevice { Id = "sink-a", FriendlyName = "Sink A", IsEnabled = true };
            var target = new AppSession
            {
                Pid = 4242, ExePath = @"C:\Apps\Test.exe", ProcessName = "Test", DisplayName = "Test App",
            };
            var unrelated = new AppSession
            {
                Pid = 9, ExePath = @"C:\Other\Thing.exe", ProcessName = "Thing", DisplayName = "Other",
            };

            // 1) 应用在跑且设备在 → 下发一次
            var first = reconciler.Reconcile(new[] { target, unrelated }, new[] { device });
            Check("reconcile: matches by exe path", first.Count == 1, first.Count.ToString());
            Check("reconcile: dispatch called once", backend.Applied.Count == 1, backend.Applied.Count.ToString());
            Check("reconcile: dispatched with live pid", backend.Applied[0].Pid == 4242);
            Check("reconcile: records last pid", store.All[0].LastPid == 4242, store.All[0].LastPid.ToString());

            // 2) 同一次运行内不重复下发（否则 Linux 上会每秒和用户手动调整打架）
            var second = reconciler.Reconcile(new[] { target, unrelated }, new[] { device });
            Check("reconcile: no repeated dispatch for same pid", second.Count == 0 && backend.Applied.Count == 1,
                $"matches={second.Count} applied={backend.Applied.Count}");

            // 3) 应用重启（PID 变了）→ 必须重新下发，否则"重启后自动恢复"不成立
            var restarted = new AppSession
            {
                Pid = 5150, ExePath = @"C:\Apps\Test.exe", ProcessName = "Test", DisplayName = "Test App",
            };
            var third = reconciler.Reconcile(new[] { restarted }, new[] { device });
            Check("reconcile: re-applies after app restart (new pid)", third.Count == 1 && backend.Applied.Count == 2,
                $"matches={third.Count} applied={backend.Applied.Count}");
            Check("reconcile: last pid updated", store.All[0].LastPid == 5150, store.All[0].LastPid.ToString());

            // 4) 应用没在跑 → 不下发
            var idle = reconciler.Reconcile(new[] { unrelated }, new[] { device });
            Check("reconcile: skips when app is not running", idle.Count == 0 && backend.Applied.Count == 2);

            // 5) 设备不在（未插/已禁用）→ 不下发，不假装成功
            var noDevice = reconciler.Reconcile(new[] { target }, Array.Empty<AudioDevice>());
            Check("reconcile: skips when device is absent", noDevice.Count == 0 && backend.Applied.Count == 2);

            // 6) 同一可执行文件的多个实例（真机上 QQ 就是两个进程）→ 每个实例都要下发
            var second5 = new AppSession
            {
                Pid = 6001, ExePath = @"C:\Apps\Test.exe", ProcessName = "Test", DisplayName = "Test App",
            };
            var third5 = new AppSession
            {
                Pid = 6002, ExePath = @"C:\Apps\Test.exe", ProcessName = "Test", DisplayName = "Test App",
            };
            var multi = reconciler.Reconcile(new[] { second5, third5 }, new[] { device });
            Check("reconcile: dispatches to every instance of the same executable",
                multi.Count == 2 && backend.Applied.Count == 4,
                $"matches={multi.Count} applied={backend.Applied.Count}");
        }
        finally
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
                // 忽略
            }
        }
    }

    /// <summary>
    /// 回归测试：设备卡里的 chip 靠「派生属性通知」才会出现。
    /// 刚踩过一次 —— 集合变了但 HasRoutes 不发通知，界面上计数正确、chip 却空白。
    /// </summary>
    private static void TestDeviceRouteNotifications()
    {
        var device = new AudioDevice { Id = "d1", FriendlyName = "Test Device" };
        var raised = new List<string?>();
        device.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        device.Routes.Add(new RouteChip { Pid = 7, DisplayName = "Some App" });

        Check("device: adding a route raises HasRoutes", raised.Contains("HasRoutes"),
            string.Join(", ", raised));
        Check("device: HasRoutes becomes true", device.HasRoutes);
        Check("device: RouteCount becomes 1", device.RouteCount == 1, device.RouteCount.ToString());

        raised.Clear();
        device.Routes.RemoveAt(0);

        Check("device: removing a route raises HasRoutes", raised.Contains("HasRoutes"),
            string.Join(", ", raised));
        Check("device: HasRoutes becomes false", !device.HasRoutes);
    }

    /// <summary>
    /// 全项目最重要的一条不变量：**「已生效」绝不能和「仅记录」混为一谈**。
    /// 记录一条路由和真正改道，在 UI 上看起来可以一模一样 —— 那正是要防的。
    /// </summary>
    private static void TestRoutingHonesty()
    {
        var path = Path.Combine(Path.GetTempPath(), $"audio-router-honesty-{Guid.NewGuid():N}.json");

        try
        {
            // 后端能改道 → Applied
            var applied = new RoutingService(new RouteStore(path), new AlwaysRoutingBackend())
                .Route(new RouteRecord
                {
                    ExePath = @"C:\Apps\One.exe", ProcessName = "One", DeviceId = "sink-x",
                });
            Check("routing: capable backend → Applied", applied == RoutingOutcome.Applied, applied.ToString());

            // 后端不能改道 → RecordedOnly（绝不能报 Applied）
            File.Delete(path);
            var recorded = new RoutingService(new RouteStore(path), new RecordingOnlyBackend())
                .Route(new RouteRecord
                {
                    ExePath = @"C:\Apps\Two.exe", ProcessName = "Two", DeviceId = "sink-y",
                });
            Check("routing: incapable backend → RecordedOnly", recorded == RoutingOutcome.RecordedOnly,
                recorded.ToString());

            // 且即便如此，用户意图仍然被记录
            Check("routing: intent still persisted", RouteStore.Load(path).All.Count == 1);

            // 描述文案必须点明「仅记录」：Describe 是本地化的短句，
            // Detail 才是承载技术原因的那一段（不翻译，供诊断）。
            var service = new RoutingService(new RouteStore(path), new RecordingOnlyBackend());
            Check("routing: describe is non-empty", !string.IsNullOrWhiteSpace(service.Describe()), service.Describe());
            Check("routing: detail carries the reason",
                service.Detail.Contains("no dispatch", StringComparison.OrdinalIgnoreCase), service.Detail);
        }
        finally
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
                // 忽略
            }
        }
    }

    private static void TestBackendCapabilityDeclaration()
    {
        // 刻意在任意平台构造 Linux 后端：这里验证的是**能力声明**（纯数据），
        // 不调用任何 Linux 专有 API —— IsAvailable 只查 PATH，构造与属性读取都不会执行外部命令。
        // 因此在此处显式且局部地关闭平台分析器，而不是全局忽略。
#pragma warning disable CA1416
        var linux = new LinuxAudioBackend();
        var declaresNoDuplication = !linux.SupportsDuplication;
        var limitation = linux.Limitation;
#pragma warning restore CA1416

        Check("capability: linux backend declares no duplication", declaresNoDuplication);
        Check("capability: linux backend names a limitation", !string.IsNullOrWhiteSpace(limitation), limitation);

        var unsupported = new UnsupportedAudioBackend("test", "nothing here");
        Check("capability: unsupported backend reports unavailable", !unsupported.IsAvailable);
        Check("capability: unsupported backend cannot route", !unsupported.SupportsRouting);
    }

    // ======================================================================
    //  子进程调用（Linux 后端完全依赖它：读 pactl 输出）
    // ======================================================================

    private static void TestProcessRunner()
    {
        var existing = OperatingSystem.IsWindows() ? "cmd" : "sh";
        Check("process: finds an existing command on PATH", ProcessRunner.Exists(existing));
        Check("process: rejects a bogus command", !ProcessRunner.Exists("definitely-not-a-real-command-42"));

        // 关键：验证「捕获子进程 stdout」这条链路真的通 —— Linux 后端的全部数据都从这里来
        var (exitCode, stdOut, stdErr) = OperatingSystem.IsWindows()
            ? ProcessRunner.Run("cmd", "/c", "echo", "audio-router-stdout-probe")
            : ProcessRunner.Run("sh", "-c", "echo audio-router-stdout-probe");

        Check("process: captures stdout from a child process",
            stdOut.Contains("audio-router-stdout-probe", StringComparison.Ordinal),
            $"exit={exitCode} stdout='{stdOut.Trim()}' stderr='{stdErr.Trim()}'");
    }

    /// <summary>
    /// Linux 后端的**命令行**验证：注入假 pactl，跑完整后端逻辑。
    ///
    /// 真实 PipeWire 环境仍未验证（本机没有 Linux 内核），但"命令拼装"——
    /// 也就是最容易错、之前完全没被覆盖的那部分 —— 在这里被钉死了：
    /// move-sink-input 的下标与目标、set-sink-input-mute、把 sink 名映射成 index、移回默认 sink。
    /// </summary>
    private static void TestLinuxBackendCommands()
    {
        var calls = new List<string>();

        (int, string, string) Runner(string file, string[] args)
        {
            calls.Add($"{file} {string.Join(' ', args)}");

            if (args.Contains("get-default-sink"))
                return (0, "alsa_output.pci-0000_00_1f.3.analog-stereo\n", string.Empty);
            if (args.Contains("sinks")) return (0, SampleData.Sinks, string.Empty);
            if (args.Contains("sink-inputs")) return (0, SampleData.SinkInputs, string.Empty);

            return (0, string.Empty, string.Empty);
        }

#pragma warning disable CA1416 // 本用例刻意在任意平台驱动 Linux 后端：只验证命令拼装，不触碰任何 Linux 专有 API
        var backend = new LinuxAudioBackend(Runner, () => true, _ => true);

        // ---------- 枚举 ----------
        var devices = backend.EnumerateDevices();
        Check("linux: two sinks parsed", devices.Count == 2, devices.Count.ToString());

        var defaultDevice = devices.FirstOrDefault(d => d.IsDefault);
        Check("linux: default sink marked", defaultDevice is not null,
            string.Join(",", devices.Select(d => $"{d.Id}={d.IsDefault}")));
        Check("linux: format built from sample_spec",
            defaultDevice?.FormatText == "16 bit · 48 kHz · 2ch", defaultDevice?.FormatText);
        Check("linux: id is the stable sink name (not an index)",
            devices.All(d => d.Id.StartsWith("alsa") || d.Id.StartsWith("bluez")));

        var sessions = backend.EnumerateSessions();
        Check("linux: sessions grouped by pid (pid-less skipped)", sessions.Count == 2, sessions.Count.ToString());

        var firefox = sessions.FirstOrDefault(s => s.Pid == 12345);
        Check("linux: session state parsed",
            firefox is { IsMuted: false } && firefox.Volume > 0.99f,
            $"{firefox?.IsMuted} / {firefox?.Volume}");

        // ---------- 路由：真正的 move-sink-input ----------
        calls.Clear();
        var routed = backend.ApplyRoute(12345, "bluez_output.AC_80_0A_1B_2C_3D.1", RouteMode.Route);
        Check("linux: route issues move-sink-input with correct index and sink",
            calls.Any(c => c.Contains("move-sink-input 42 bluez_output.AC_80_0A_1B_2C_3D.1")),
            string.Join(" | ", calls));
        Check("linux: route reports Applied", routed == RoutingOutcome.Applied, routed.ToString());

        // ---------- 复制：pulse 层做不到，必须明确拒绝且不发命令 ----------
        calls.Clear();
        var duplicated = backend.ApplyRoute(12345, "bluez_output.AC_80_0A_1B_2C_3D.1", RouteMode.Duplicate);
        Check("linux: duplicate is refused (declared unsupported)",
            duplicated == RoutingOutcome.NotImplemented, duplicated.ToString());
        Check("linux: duplicate issues no command", calls.Count == 0, string.Join(" | ", calls));

        Check("linux: unknown pid → NotFound",
            backend.ApplyRoute(999999, "whatever", RouteMode.Route) == RoutingOutcome.NotFound);

        // ---------- 静音 ----------
        calls.Clear();
        var muted = backend.SetProcessMute(12345, true);
        Check("linux: mute issues set-sink-input-mute 42 1",
            muted && calls.Any(c => c.Contains("set-sink-input-mute 42 1")), string.Join(" | ", calls));

        // ---------- 按路由静音：必须把 sink 名映射成 index ----------
        calls.Clear();
        var routeMute = backend.SetRouteMute(6789, "bluez_output.AC_80_0A_1B_2C_3D.1", true);
        Check("linux: route mute maps sink name → index",
            routeMute == RoutingOutcome.Applied && calls.Any(c => c.Contains("set-sink-input-mute 43 1")),
            string.Join(" | ", calls));

        // ---------- 解除：移回默认 sink ----------
        calls.Clear();
        var removed = backend.RemoveRoute(12345, "bluez_output.AC_80_0A_1B_2C_3D.1");
        Check("linux: remove moves the stream back to @DEFAULT_SINK@",
            removed == RoutingOutcome.Applied && calls.Any(c => c.Contains("move-sink-input 42 @DEFAULT_SINK@")),
            string.Join(" | ", calls));
#pragma warning restore CA1416
    }

    // ======================================================================
    //  显示宽度
    // ======================================================================

    private static void TestTextWidth()
    {
        Check("width: ascii", TextWidth.Of("abc") == 3);
        Check("width: CJK counts as 2", TextWidth.Of("中文") == 4);
        Check("width: mixed", TextWidth.Of("a中") == 3);
        Check("width: pad CJK to width", TextWidth.Pad("中文", 6) == "中文  ");
        Check("width: pad ascii", TextWidth.Pad("ab", 4) == "ab  ");
        Check("width: no negative padding", TextWidth.Pad("abcdef", 2) == "abcdef");
    }

    // ======================================================================
    //  运行器
    // ======================================================================

    private static void Check(string name, bool condition, string? detail = null)
    {
        if (condition)
        {
            _passed++;
            Console.WriteLine($"  PASS  {name}");
            return;
        }

        _failed++;
        Console.WriteLine($"  FAIL  {name}{(detail is null ? string.Empty : $"  →  {detail}")}");
    }

    // ======================================================================
    //  测试替身：验证「能改道 / 不能改道」两条路径
    // ======================================================================

    private sealed class AlwaysRoutingBackend : IAudioBackend
    {
        public string Name => "fake-capable";
        public bool IsAvailable => true;
        public string? Limitation => null;
        public bool SupportsRouting => true;
        public bool SupportsDuplication => true;
        public IReadOnlyList<AudioDevice> EnumerateDevices() => Array.Empty<AudioDevice>();
        public IReadOnlyList<AppSession> EnumerateSessions() => Array.Empty<AppSession>();
        public bool SetProcessMute(int pid, bool muted) => true;
        public RoutingOutcome ApplyRoute(int pid, string deviceId, RouteMode mode) => RoutingOutcome.Applied;
        public RoutingOutcome RemoveRoute(int pid, string deviceId) => RoutingOutcome.Applied;
    }

    private sealed class RecordingOnlyBackend : IAudioBackend
    {
        public string Name => "fake-recording-only";
        public bool IsAvailable => true;
        public string? Limitation => "no dispatch on this platform";
        public bool SupportsRouting => false;
        public bool SupportsDuplication => false;
        public IReadOnlyList<AudioDevice> EnumerateDevices() => Array.Empty<AudioDevice>();
        public IReadOnlyList<AppSession> EnumerateSessions() => Array.Empty<AppSession>();
        public bool SetProcessMute(int pid, bool muted) => false;

        // ApplyRoute 使用接口默认实现 → NotImplemented
    }

    /// <summary>能改道，并记录每次下发 —— 用来验证自动套用器的"只下发一次 / 重启后重新下发"。</summary>
    private sealed class CountingRoutingBackend : IAudioBackend
    {
        public List<(int Pid, string DeviceId, RouteMode Mode)> Applied { get; } = new();

        public string Name => "fake-counting";
        public bool IsAvailable => true;
        public string? Limitation => null;
        public bool SupportsRouting => true;
        public bool SupportsDuplication => true;
        public IReadOnlyList<AudioDevice> EnumerateDevices() => Array.Empty<AudioDevice>();
        public IReadOnlyList<AppSession> EnumerateSessions() => Array.Empty<AppSession>();
        public bool SetProcessMute(int pid, bool muted) => true;

        public RoutingOutcome ApplyRoute(int pid, string deviceId, RouteMode mode)
        {
            Applied.Add((pid, deviceId, mode));
            return RoutingOutcome.Applied;
        }

        public RoutingOutcome RemoveRoute(int pid, string deviceId) => RoutingOutcome.Applied;
    }
}
