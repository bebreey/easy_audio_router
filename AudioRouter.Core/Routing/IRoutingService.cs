using AudioRouter.Core.Backends;
using AudioRouter.Core.Diagnostics;
using AudioRouter.Core.Models;

namespace AudioRouter.Core.Routing;

public enum RoutingOutcome
{
    /// <summary>已下发，真正生效。</summary>
    Applied,

    /// <summary>已记录，但当前平台后端还不能下发（音频未改道）。</summary>
    RecordedOnly,

    /// <summary>尝试下发了但失败（权限不足、工具链缺目标位数、委托进程报错等）。</summary>
    Failed,

    AlreadyRouted,

    NotFound,

    DeviceUnavailable,

    /// <summary>后端尚未实现下发能力。</summary>
    NotImplemented,
}

/// <summary>
/// 路由门面：把「记录状态」与「真正下发」两件事分开，并如实报告差异。
///
/// 这是全项目最容易被粉饰的地方 —— 记录一条路由看起来和"路由成功"一模一样。
/// 所以接口用不同的枚举值把二者严格区分，CLI / GUI 都不得把它们混为一谈。
/// </summary>
public interface IRoutingService
{
    RoutingOutcome Route(RouteRecord record);

    /// <summary>按**身份键**（exe 路径）解除，因此应用没在运行时也能解除。</summary>
    RoutingOutcome Unroute(string key, string deviceId);

    /// <summary>
    /// 当前下发通道的**简短**状态（走语言文件，可直接显示）。
    /// 平台细节（为什么不能改道）用 <see cref="Detail"/>，留给 Tooltip / doctor。
    /// </summary>
    string Describe();

    /// <summary>技术细节：后端名 + 能力受限的原因（不翻译，供诊断用）。</summary>
    string Detail { get; }
}

public sealed class RoutingService : IRoutingService
{
    private readonly IAudioBackend _backend;
    private readonly RouteStore _store;

    public RoutingService(RouteStore store, IAudioBackend backend)
    {
        _store = store;
        _backend = backend;
    }

    public RoutingOutcome Route(RouteRecord record)
    {
        if (string.IsNullOrEmpty(record.Key))
        {
            record.Key = RouteKey.For(record.ExePath, record.ProcessName);
        }

        if (_store.Exists(record.Key, record.DeviceId)) return RoutingOutcome.AlreadyRouted;

        var dispatch = _backend.ApplyRoute(record.LastPid, record.DeviceId, record.Mode);

        // 无论能否下发都先记录：把「用户意图」与「平台能力」分开保存
        _store.Add(record);

        StartupLog.Write(
            $"route: key='{record.Key}' device='{record.DeviceId}' mode={record.Mode} dispatch={dispatch}");

        return dispatch == RoutingOutcome.Applied
            ? RoutingOutcome.Applied
            : RoutingOutcome.RecordedOnly;
    }

    /// <summary>
    /// 解除一条路由。**卸载必须作用在活着的那个进程上** —— 原生核心的补丁长在目标进程里，
    /// 目标进程不在了，就没有补丁需要撤销。
    ///
    /// 这里曾经硬编码 pid=0，于是卸载从未真正下发过（日志表现为 `inject: remove pid=0 → invalid pid`）。
    /// 后果不是"少删了一条记录"，而是**目标进程里的补丁一直留着**：
    /// 设备被拔掉或换掉之后，音频仍指向那个已经不存在的设备。
    /// </summary>
    public RoutingOutcome Unroute(string key, string deviceId)
    {
        if (!_store.Exists(key, deviceId)) return RoutingOutcome.NotFound;

        // 同一个可执行文件可能有多个实例，每个实例的进程里各有一份补丁 —— 全都要撤销
        var livePids = _backend.EnumerateSessions()
            .Where(s => RouteKey.For(s.ExePath, s.ProcessName) == key)
            .Select(s => s.Pid)
            .Distinct()
            .ToList();

        var failed = 0;
        foreach (var livePid in livePids)
        {
            if (_backend.RemoveRoute(livePid, deviceId) != RoutingOutcome.Applied) failed++;
        }

        _store.Remove(key, deviceId);

        // 原生核心的"卸载"是整条撤销（flag=0 撤掉该进程里所有补丁），
        // 所以删掉一条之后，这个应用**剩下的路由必须重新下发** ——
        // 否则它们会静默失效（RouteReconciler 不会重新下发已经记过的组合）。
        var reapplied = 0;
        if (failed == 0 && livePids.Count > 0)
        {
            foreach (var livePid in livePids)
            {
                foreach (var remaining in _store.Find(key))
                {
                    if (_backend.ApplyRoute(livePid, remaining.DeviceId, remaining.Mode) == RoutingOutcome.Applied)
                    {
                        reapplied++;
                    }
                }
            }
        }

        StartupLog.Write(
            $"unroute: key='{key}' device='{deviceId}' livePids=[{string.Join(",", livePids)}] " +
            $"failed={failed} reapplied={reapplied}");

        return failed == 0 ? RoutingOutcome.Applied : RoutingOutcome.Failed;
    }

    public string Describe() => _backend.SupportsRouting
        ? Localization.Loc.F("status.routing.active", _backend.Name)
        : Localization.Loc.T("status.routing.recordedOnly");

    public string Detail => _backend.Limitation is { Length: > 0 } limitation
        ? $"{_backend.Name} · {limitation}"
        : _backend.Name;
}
