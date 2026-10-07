using AudioRouter.Core.Backends;
using AudioRouter.Core.Diagnostics;
using AudioRouter.Core.Models;

namespace AudioRouter.Core.Routing;

/// <summary>一条被自动套用的路由。</summary>
public sealed record RouteMatch(RouteRecord Record, int Pid, AudioDevice Device, RoutingOutcome Outcome);

/// <summary>
/// 把持久化路由**自动套用到活着的会话上**。
///
/// 这是"按 exe 路径存"换来的核心价值：
/// 用户不需要记得重新拖一次 —— 应用一出现（哪怕是重启后的新 PID），路由自动生效。
///
/// 两条纪律：
///   1. 同一次运行里，同一 (身份键, 设备, PID) 只下发一次 —— 否则在 Linux 上会每秒重复
///      执行 move-sink-input，等于和用户手动调整打架；
///   2. 应用重启（PID 变了）要重新下发 —— 否则"重启后自动恢复"就不成立。
/// </summary>
public sealed class RouteReconciler
{
    private readonly RouteStore _store;
    private readonly IAudioBackend _backend;

    /// <summary>已下发过的 (键|设备|PID)。PID 会缓慢累积，桌面级用量可忽略。</summary>
    private readonly HashSet<string> _applied = new(StringComparer.Ordinal);

    public RouteReconciler(RouteStore store, IAudioBackend backend)
    {
        _store = store;
        _backend = backend;
    }

    /// <summary>已经下发过的组合数（诊断用）。</summary>
    public int AppliedCount => _applied.Count;

    public IReadOnlyList<RouteMatch> Reconcile(
        IReadOnlyList<AppSession> sessions,
        IReadOnlyList<AudioDevice> devices)
    {
        var matches = new List<RouteMatch>();
        var pidChanged = false;

        foreach (var record in _store.All)
        {
            // 旧格式（按 PID 存、没有身份键）无法按路径匹配 —— 跳过，不假装能匹配上
            if (record.IsLegacy) continue;

            var device = devices.FirstOrDefault(d =>
                string.Equals(d.Id, record.DeviceId, StringComparison.OrdinalIgnoreCase));
            if (device is null) continue; // 设备不在（未插 / 已禁用）

            // 同一可执行文件可能同时跑多个实例（例如两个 QQ、多个浏览器进程），
            // 按路径存的意义就是"这个应用"，因此**所有实例都要下发**，不能只挑第一个。
            foreach (var session in sessions.Where(s => Matches(s, record.Key)))
            {
                var stamp = $"{record.Key}|{record.DeviceId}|{session.Pid}";
                if (!_applied.Add(stamp)) continue;

                var outcome = _backend.ApplyRoute(session.Pid, record.DeviceId, record.Mode);

                if (record.LastPid != session.Pid)
                {
                    record.LastPid = session.Pid;
                    pidChanged = true;
                }

                StartupLog.Write(
                    $"reconcile: '{RouteKey.Describe(record.Key)}' → '{device.FriendlyName}' pid={session.Pid} " +
                    $"mode={record.Mode} outcome={outcome}");

                matches.Add(new RouteMatch(record, session.Pid, device, outcome));
            }
        }

        if (pidChanged) _store.Save();
        return matches;
    }

    private static bool Matches(AppSession session, string key)
        => RouteKey.For(session.ExePath, session.ProcessName) == key;
}
