using System.Text;
using System.Text.Json;
using AudioRouter.Core.Backends;
using AudioRouter.Core.Diagnostics;
using AudioRouter.Core.Formatting;
using AudioRouter.Core.Localization;
using AudioRouter.Core.Models;
using AudioRouter.Core.Routing;

namespace AudioRouter.Cli;

/// <summary>
/// Audio Router 命令行入口。
///
/// 设计意图：**没有桌面环境也必须能用**（服务器 / SSH / WSL / 脚本），
/// 同时它也是「核心与 UI 解耦」的强制手段 —— CLI 跑得通，就证明核心确实平台无关。
///
/// 退出码：0 成功 / 1 运行错误 / 2 用法错误（脚本可判）。
/// </summary>
internal static class Program
{
    private static bool _json;
    private static bool _verbose;

    private static IAudioBackend Backend => AudioBackendFactory.Current;

    private static int Main(string[] args)
    {
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
        }
        catch
        {
            // 某些重定向环境不支持，忽略
        }

        LocalizationService.Instance.Initialize();

        // 全局开关先提取，再分发命令
        var rest = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--json":
                    _json = true;
                    break;
                case "--verbose":
                case "-v":
                    _verbose = true;
                    StartupLog.EchoToConsole = true;
                    break;
                case "--lang" when i + 1 < args.Length:
                    LocalizationService.Instance.ChangeLanguage(args[++i]);
                    break;
                case "--version":
                    Console.WriteLine(Version());
                    return 0;
                case "--help":
                case "-h":
                    PrintUsage();
                    return 0;
                default:
                    rest.Add(arg);
                    break;
            }
        }

        if (rest.Count == 0 || rest[0] is "help")
        {
            PrintUsage();
            return 0;
        }

        try
        {
            return rest[0] switch
            {
                "devices" => Devices(rest.Count > 1 ? rest[1] : null),
                "apps" or "sessions" => Apps(),
                "routes" => Routes(),
                "apply" => Apply(),
                "route" => Route(rest),
                "unroute" => Unroute(rest),
                "mute" => Mute(rest),
                "lang" or "language" => Lang(rest),
                "doctor" => Doctor(),
                _ => Unknown(rest[0]),
            };
        }
        catch (Exception ex)
        {
            Error($"{ex.GetType().Name}: {ex.Message}");
            if (_verbose) Error(ex.ToString());
            return 1;
        }
    }

    // ======================================================================
    //  命令实现
    // ======================================================================

    private static int Devices(string? filter)
    {
        var devices = Backend.EnumerateDevices()
            // 与 GUI 一致：默认设备优先，其次可用设备，最后才是失效端点 ——
            // 30 个端点里失效的常占大多数，不排序就等于把可用设备埋在噪音里
            .OrderByDescending(d => d.IsDefault)
            .ThenByDescending(d => d.IsEnabled)
            .ThenBy(d => d.FriendlyName, StringComparer.CurrentCulture)
            .ToList();

        if (!string.IsNullOrWhiteSpace(filter))
        {
            devices = devices
                .Where(d => d.FriendlyName.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (_json)
        {
            WriteJson(devices.Select(d => new
            {
                id = d.Id,
                name = d.FriendlyName,
                kind = d.Kind.ToString(),
                kindLabel = d.KindText,
                format = d.FormatText,
                isDefault = d.IsDefault,
                isEnabled = d.IsEnabled,
                state = d.StateText,
                routes = d.RouteCount,
            }));
            return 0;
        }

        if (devices.Count == 0)
        {
            Console.WriteLine(Backend.IsAvailable ? "No audio devices found." : Unavailable());
            return Backend.IsAvailable ? 0 : 1;
        }

        var rows = devices.Select(d => new[]
        {
            d.IsDefault ? "*" : " ",
            d.IsEnabled ? "on" : "-",
            d.KindText,
            d.FriendlyName,
            d.FormatText ?? string.Empty,
            d.RouteCount > 0 ? d.RouteCount.ToString() : string.Empty,
        }).ToList();

        Table(new[] { "", "  ", "Kind", "Device", "Format", "routes" }, rows);
        Console.WriteLine();
        Console.WriteLine($"* = default    on = active    - = disabled/unavailable    total {devices.Count}");
        Console.WriteLine("device ids are visible with --json (needed by `route`)");
        return 0;
    }

    private static int Apps()
    {
        var sessions = Backend.EnumerateSessions();

        if (_json)
        {
            WriteJson(sessions.Select(s => new
            {
                pid = s.Pid,
                process = s.ProcessName,
                name = s.DisplayName,
                exe = s.ExePath,
                volume = s.VolumePercent,
                muted = s.IsMuted,
                playing = s.IsPlaying,
                routes = s.RouteCount,
            }));
            return 0;
        }

        if (sessions.Count == 0)
        {
            Console.WriteLine(Backend.IsAvailable
                ? "No application is playing audio right now."
                : Unavailable());
            return Backend.IsAvailable ? 0 : 1;
        }

        var rows = sessions
            .OrderByDescending(s => s.IsPlaying)
            .ThenBy(s => s.DisplayName, StringComparer.CurrentCulture)
            .Select(s => new[]
            {
                s.Pid.ToString(),
                s.IsPlaying ? ">" : " ",
                s.IsMuted ? "mute" : " ",
                $"{s.VolumePercent}%",
                s.DisplayName,
                s.ProcessName,
            })
            .ToList();

        Table(new[] { "PID", "", "", "Vol", "Application", "Process" }, rows);
        Console.WriteLine();
        Console.WriteLine($"> = currently playing    mute = muted    total {sessions.Count}");
        return 0;
    }

    private static int Routes()
    {
        var store = RouteStore.Load();

        if (_json)
        {
            WriteJson(store.All.Select(r => new
            {
                key = r.Key,
                exe = r.ExePath,
                process = r.ProcessName,
                app = r.DisplayName,
                deviceId = r.DeviceId,
                device = r.DeviceName,
                mode = r.Mode.ToString(),
                lastPid = r.LastPid,
                weakIdentity = RouteKey.IsWeak(r.Key),
                legacy = r.IsLegacy,
                createdAt = r.CreatedAt,
            }));
            return 0;
        }

        if (store.All.Count == 0)
        {
            Console.WriteLine("No routes recorded.");
            Console.WriteLine(RoutingHint());
            return 0;
        }

        var rows = store.All
            .OrderBy(r => r.DisplayName, StringComparer.CurrentCulture)
            .Select(r => new[]
            {
                r.IsLegacy ? "(legacy: no identity key)" : RouteKey.Describe(r.Key),
                string.IsNullOrEmpty(r.DisplayName) ? "-" : r.DisplayName,
                r.Mode.ToString(),
                string.IsNullOrEmpty(r.DeviceName) ? r.DeviceId : r.DeviceName,
                r.LastPid > 0 ? r.LastPid.ToString() : "-",
            })
            .ToList();

        Table(new[] { "Application (identity key)", "Name", "Mode", "Device", "LastPID" }, rows);
        Console.WriteLine();
        Console.WriteLine("routes are stored per executable path, so they survive restarts and");
        Console.WriteLine("are re-applied automatically when the application shows up again.");
        Console.WriteLine(RoutingHint());
        Console.WriteLine($"state file: {RouteStore.DefaultPath}");
        return 0;
    }

    /// <summary>把保存的路由立刻套用到当前正在运行的应用上（无头场景下的"恢复路由"）。</summary>
    private static int Apply()
    {
        var store = RouteStore.Load();

        if (store.All.Count == 0)
        {
            Console.WriteLine("No saved routes to apply.");
            return 0;
        }

        var sessions = Backend.EnumerateSessions();
        var devices = Backend.EnumerateDevices();
        var matches = new RouteReconciler(store, Backend).Reconcile(sessions, devices);

        if (matches.Count == 0)
        {
            Console.WriteLine("No saved route matches a running application and present device right now.");
            Console.WriteLine(RoutingHint());
            return 0;
        }

        var rows = matches.Select(m => new[]
        {
            m.Pid.ToString(),
            m.Record.DisplayName.Length > 0 ? m.Record.DisplayName : RouteKey.Describe(m.Record.Key),
            m.Device.FriendlyName,
            m.Record.Mode.ToString(),
            m.Outcome == RoutingOutcome.Applied ? "applied" : "recorded only",
        }).ToList();

        Table(new[] { "PID", "Application", "Device", "Mode", "Result" }, rows);
        Console.WriteLine();
        Console.WriteLine(RoutingHint());
        return 0;
    }

    private static int Route(List<string> args)
    {
        if (args.Count < 3)
        {
            Error("usage: audio-router route <pid> <deviceId> [--duplicate]");
            Error("hint : get device ids from `audio-router devices --json`");
            return 2;
        }

        if (!int.TryParse(args[1], out var pid))
        {
            Error($"invalid pid: {args[1]}");
            return 2;
        }

        var deviceId = args[2];
        var mode = args.Contains("--duplicate") ? RouteMode.Duplicate : RouteMode.Route;

        var device = Backend.EnumerateDevices()
            .FirstOrDefault(d => string.Equals(d.Id, deviceId, StringComparison.OrdinalIgnoreCase));

        if (device is null)
        {
            Error($"device not found: {deviceId}");
            return 1;
        }

        // 路由的身份是「可执行文件路径」，不是「此刻有没有在播」。
        //
        // 而原生核心的补丁挂钩的是 IMMDevice::Activate —— 只对**注入之后新建**的音频流生效
        // （真机验证过）。所以正确用法恰恰是"先注入、后启动应用"，
        // 要求目标此刻已有会话会把这条正确路径挡在门外。
        // 会话拿不到时，退回用进程本身的可执行文件路径。
        var session = Backend.EnumerateSessions().FirstOrDefault(s => s.Pid == pid);

        var exePath = session?.ExePath;
        var processName = session?.ProcessName ?? string.Empty;

        if (string.IsNullOrEmpty(exePath))
        {
            (exePath, processName) = ResolveProcessIdentity(pid, processName);
        }

        if (string.IsNullOrEmpty(exePath) && string.IsNullOrEmpty(processName))
        {
            Error($"pid {pid} not found — cannot tell which executable to route");
            return 1;
        }

        var key = RouteKey.For(exePath, processName);

        // 会话拿不到时的兜底：Windows 看进程模块，Linux 看 /proc/<pid>/exe
        static (string? ExePath, string ProcessName) ResolveProcessIdentity(int targetPid, string fallbackName)
        {
            try
            {
                var process = System.Diagnostics.Process.GetProcessById(targetPid);
                var name = string.IsNullOrEmpty(fallbackName) ? process.ProcessName : fallbackName;

                if (OperatingSystem.IsWindows())
                {
                    return (process.MainModule?.FileName, name);
                }

                var target = File.ResolveLinkTarget($"/proc/{targetPid}/exe", returnFinalTarget: true);
                return (target?.FullName, name);
            }
            catch
            {
                return (null, fallbackName);
            }
        }

        var routing = new RoutingService(RouteStore.Load(), Backend);
        var outcome = routing.Route(new RouteRecord
        {
            Key = key,
            ExePath = exePath ?? string.Empty,
            ProcessName = processName,
            DisplayName = session?.DisplayName ?? processName,
            DeviceId = deviceId,
            DeviceName = device.FriendlyName,
            Mode = mode,
            LastPid = pid,
            CreatedAt = DateTime.Now,
        });

        var identity = RouteKey.IsWeak(key)
            ? $"{RouteKey.Describe(key)} [weak: matched by process name only]"
            : RouteKey.Describe(key);

        if (outcome == RoutingOutcome.AlreadyRouted)
        {
            Console.WriteLine($"already routed: {identity} -> {device.FriendlyName}");
            return 0;
        }

        Console.WriteLine(outcome switch
        {
            // 口径必须精确：注入成功 ≠ 立刻听到效果。
            // 原生核心只影响**注入之后新建**的音频流，所以正在播放的应用要等它重建音频流
            // （通常就是重启该应用）才会真的换设备。
            RoutingOutcome.Applied =>
                $"routed   {identity} -> {device.FriendlyName} ({mode}) — dispatched; takes effect once the app (re)creates its audio stream (restart it if it is already playing)",

            RoutingOutcome.Failed =>
                $"FAILED   {identity} -> {device.FriendlyName} ({mode}) — dispatch failed; see the log (elevated targets need Audio Router to run as administrator)",

            RoutingOutcome.DeviceUnavailable =>
                $"FAILED   {identity} -> {device.FriendlyName} ({mode}) — device unavailable",

            RoutingOutcome.NotImplemented =>
                $"recorded {identity} -> {device.FriendlyName} ({mode}) — this backend cannot dispatch yet",

            _ =>
                $"recorded {identity} -> {device.FriendlyName} ({mode}) — audio NOT redirected on this platform",
        });
        Console.WriteLine(RoutingHint());
        return 0;
    }

    private static int Unroute(List<string> args)
    {
        if (args.Count < 3)
        {
            Error("usage: audio-router unroute <pid|exePath> <deviceId>");
            Error("note : works even if the application has already exited (keyed by path)");
            return 2;
        }

        var store = RouteStore.Load();
        var key = ResolveKey(store, args[1]);

        if (key is null)
        {
            Error($"no recorded route matches '{args[1]}'");
            return 1;
        }

        // 必须走服务层，不能直接改 store：
        // 原生核心的补丁长在目标进程里，只删记录等于"记录没了、补丁还在"，
        // 设备一拔一换，那个程序的音频就会指向一个不存在的设备。
        var routing = new RoutingService(RouteStore.Load(), Backend);
        var outcome = routing.Unroute(key, args[2]);

        if (outcome == RoutingOutcome.NotFound)
        {
            Error($"no route for '{RouteKey.Describe(key)}' on device {args[2]}");
            return 1;
        }

        Console.WriteLine($"unrouted {RouteKey.Describe(key)} from device {args[2]}");

        if (outcome == RoutingOutcome.Applied)
        {
            Console.WriteLine("the running program was told to stop using that device");
            Console.WriteLine("(a program that is already playing may need a restart before you hear the change).");
        }
        else
        {
            Error("the route was removed, but the unload could not be dispatched - see the log for the reason");
        }

        Console.WriteLine("it will not be re-applied next time the application starts.");
        return outcome == RoutingOutcome.Applied ? 0 : 1;
    }

    /// <summary>
    /// 把用户的输入解析成身份键：PID（应用在跑）→ 记录里的 LastPID（应用已退出）→ 路径/进程名。
    /// 这是"按路径存"的直接好处：应用关了也能解除路由。
    /// </summary>
    private static string? ResolveKey(RouteStore store, string target)
    {
        if (int.TryParse(target, out var pid))
        {
            var session = Backend.EnumerateSessions().FirstOrDefault(s => s.Pid == pid);
            if (session is not null)
            {
                var live = RouteKey.For(session.ExePath, session.ProcessName);
                if (store.Find(live).Count > 0) return live;
            }

            return store.All.FirstOrDefault(r => r.LastPid == pid)?.Key;
        }

        var normalized = RouteKey.Normalize(target);

        foreach (var candidate in new[]
                 {
                     normalized,
                     RouteKey.PathPrefix + normalized,
                     RouteKey.NamePrefix + normalized,
                 })
        {
            if (store.Find(candidate).Count > 0) return candidate;
        }

        // 最后按进程名兜底（用户可能只记得 exe 名）
        return store.All.FirstOrDefault(r =>
            string.Equals(r.ProcessName, target, StringComparison.OrdinalIgnoreCase))?.Key;
    }

    private static int Mute(List<string> args)
    {
        if (args.Count < 2 || !int.TryParse(args[1], out var pid))
        {
            Error("usage: audio-router mute <pid> [--off]");
            return 2;
        }

        var muted = !args.Contains("--off");

        if (!Backend.IsAvailable)
        {
            Error(Unavailable());
            return 1;
        }

        if (!Backend.SetProcessMute(pid, muted))
        {
            Error($"pid {pid} has no audio session to {(muted ? "mute" : "unmute")}");
            return 1;
        }

        Console.WriteLine($"pid {pid} {(muted ? "muted" : "unmuted")} (effective)");
        return 0;
    }

    private static int Lang(List<string> args)
    {
        var localization = LocalizationService.Instance;

        if (args.Count >= 3 && args[1] == "set")
        {
            if (!localization.ChangeLanguage(args[2]))
            {
                Error($"unknown language: {args[2]}");
                Error($"available: {string.Join(", ", localization.AvailableLanguages.Select(p => p.Code))}");
                return 1;
            }

            Console.WriteLine($"language set to {args[2]}");
            return 0;
        }

        if (args.Count >= 3 && args[1] == "import")
        {
            if (!localization.Import(args[2], out var code, out var error))
            {
                Error($"import failed: {error}");
                return 1;
            }

            localization.ChangeLanguage(code);
            Console.WriteLine($"imported language pack: {code}");
            return 0;
        }

        var rows = localization.AvailableLanguages
            .Select(p => new[]
            {
                string.Equals(p.Code, localization.Current?.Code, StringComparison.OrdinalIgnoreCase) ? "*" : " ",
                p.Code,
                p.Name,
                p.Strings.Count.ToString(),
                p.SourcePath ?? string.Empty,
            })
            .ToList();

        Table(new[] { "", "Code", "Name", "Keys", "File" }, rows);
        Console.WriteLine();
        Console.WriteLine($"* = current    user language dir: {LocalizationService.UserDirectory}");
        Console.WriteLine("commands: audio-router lang set <code> | lang import <path>");
        return 0;
    }

    /// <summary>环境与能力自检 —— 一条命令回答「这台机器到底能干什么」。</summary>
    private static int Doctor()
    {
        var backend = Backend;
        var devices = backend.EnumerateDevices();
        var sessions = backend.EnumerateSessions();
        var localization = LocalizationService.Instance;

        var rows = new List<string[]>
        {
            new[] { "Platform", $"{Environment.OSVersion.Platform} {Environment.OSVersion.Version}" },
            new[] { "Runtime", System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription },
            new[] { "Architecture", System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString() },
            new[] { "Audio backend", backend.Name },
            new[] { "Backend available", backend.IsAvailable ? "yes" : "NO" },
            new[] { "Can redirect audio", backend.SupportsRouting ? "yes" : "NO (records only)" },
            new[] { "Can duplicate", backend.SupportsDuplication ? "yes" : "NO" },
            new[] { "Limitation", backend.Limitation ?? "-" },
            new[] { "Native core", NativeCoreProbe.IsPresent ? NativeCoreProbe.Directory! : "not found" },
            new[] { "Devices", $"{devices.Count} ({(devices.Count(d => d.IsEnabled))} active)" },
            new[] { "Sessions", sessions.Count.ToString() },
            new[] { "Languages", string.Join(", ", localization.AvailableLanguages.Select(p => p.Code)) },
            new[] { "Current language", localization.Current?.Code ?? "-" },
            new[] { "State dir", Path.GetDirectoryName(RouteStore.DefaultPath)! },
            new[] { "Log file", StartupLog.LogFilePath },
        };

        if (_json)
        {
            WriteJson(new
            {
                platform = Environment.OSVersion.ToString(),
                backend = backend.Name,
                available = backend.IsAvailable,
                supportsRouting = backend.SupportsRouting,
                supportsDuplication = backend.SupportsDuplication,
                limitation = backend.Limitation,
                nativeCore = NativeCoreProbe.Directory,
                devices = devices.Count,
                sessions = sessions.Count,
                language = localization.Current?.Code,
            });
            return 0;
        }

        Table(new[] { "Check", "Value" }, rows);
        return backend.IsAvailable ? 0 : 1;
    }

    // ======================================================================
    //  输出工具
    // ======================================================================

    private static string Version() => "audio-router (AudioRouter.Cli) 0.1.0";

    private static string Unavailable()
        => $"audio backend '{Backend.Name}' is not available on this platform: {Backend.Limitation}";

    /// <summary>CLI 里同时给出本地化短句与技术原因 —— 脚本/人都能看懂。</summary>
    private static string RoutingHint()
    {
        var routing = new RoutingService(RouteStore.Load(), Backend);
        return $"routing: {routing.Describe()} — {routing.Detail}";
    }

    private static void PrintUsage()
    {
        Console.WriteLine(Version());
        Console.WriteLine();
        Console.WriteLine("usage: audio-router <command> [options]");
        Console.WriteLine();
        Console.WriteLine("commands:");
        Console.WriteLine("  devices [filter]              list output devices");
        Console.WriteLine("  apps                          list applications with audio sessions");
        Console.WriteLine("  routes                        list saved routes (keyed by executable path)");
        Console.WriteLine("  apply                         re-apply saved routes to running applications");
        Console.WriteLine("  route <pid> <deviceId>        route an application to a device");
        Console.WriteLine("         [--duplicate]            add as a duplicate (multi-device) route");
        Console.WriteLine("  unroute <pid|exePath> <deviceId>   remove a route (works after the app exited)");
        Console.WriteLine("  mute <pid> [--off]            mute / unmute an application (effective)");
        Console.WriteLine("  lang [list|set <code>|import <path>]   manage language packs");
        Console.WriteLine("  doctor                        show platform and capability report");
        Console.WriteLine();
        Console.WriteLine("options:");
        Console.WriteLine("  --json        machine readable output");
        Console.WriteLine("  --lang <code> override output language");
        Console.WriteLine("  -v, --verbose echo diagnostics to stderr");
        Console.WriteLine("  --version, --help");
        Console.WriteLine();
        Console.WriteLine("note: the CLI runs headless; no desktop environment is required.");
    }

    private static void Table(string[] headers, IEnumerable<string[]> rows)
    {
        var all = new List<string[]> { headers };
        all.AddRange(rows);

        var widths = new int[headers.Length];
        foreach (var row in all)
        {
            for (var i = 0; i < row.Length && i < widths.Length; i++)
            {
                widths[i] = Math.Max(widths[i], TextWidth.Of(row[i]));
            }
        }

        var headerLine = string.Join("  ", headers.Select((h, i) => TextWidth.Pad(h, widths[i]))).TrimEnd();
        var rule = new string('-', TextWidth.Of(headerLine));

        Console.WriteLine(headerLine);
        Console.WriteLine(rule);

        foreach (var row in rows)
        {
            Console.WriteLine(string.Join("  ", row.Select((c, i) => TextWidth.Pad(c, widths[i]))).TrimEnd());
        }
    }

    private static void WriteJson(object value)
        => Console.WriteLine(JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));

    private static int Unknown(string command)
    {
        Error($"unknown command: {command}");
        Error("run `audio-router --help` for usage");
        return 2;
    }

    private static void Error(string message) => Console.Error.WriteLine($"error: {message}");
}
