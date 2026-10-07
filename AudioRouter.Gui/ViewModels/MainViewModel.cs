using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Threading;
using AudioRouter.Core.Backends;
using AudioRouter.Core.Diagnostics;
using AudioRouter.Core.Localization;
using AudioRouter.Core.Models;
using AudioRouter.Core.Mvvm;
using AudioRouter.Core.Routing;
using AudioRouter.Gui.Models;
using AudioRouter.Gui.Services;

namespace AudioRouter.Gui.ViewModels;

/// <summary>
/// WPF 视图模型：只负责表现层与交互编排。
///
/// 音频枚举、静音、路由、语言、设置全部来自跨平台核心库（AudioRouter.Core），
/// 本层保留的 WPF 依赖只有三类：<see cref="ICollectionView"/>（排序/分组/过滤）、
/// <see cref="DispatcherTimer"/>（刷新节拍）、以及通过 <c>object?</c> 槽位放进核心模型的
/// 图标与画刷（<see cref="AppIconService"/> / <see cref="AvatarPalette"/>）。
/// </summary>
internal sealed class MainViewModel : ObservableObject
{
    /// <summary>右下角同时最多堆叠的提示数量（设计文档 7）。</summary>
    private const int MaxToasts = 3;

    private readonly ObservableCollection<AppSession> _sessions = new();
    private readonly Dictionary<string, AudioDevice> _deviceIndex = new(StringComparer.OrdinalIgnoreCase);
    private readonly DispatcherTimer _timer;

    /// <summary>与 CLI 共用的持久化路由表（按可执行文件路径存，跨重启、跨进程）。</summary>
    private readonly RouteStore _routeStore = RouteStore.Load();

    private readonly IRoutingService _routing;
    private readonly RouteReconciler _reconciler;

    private int _tick;
    private bool _firstRefreshLogged;
    private bool _restoreNotified;
    private string _searchText = string.Empty;
    private string _statusText = string.Empty;
    private string _engineText = string.Empty;
    private bool _isDemo;
    private bool _hasSessions;
    private bool _hasVisibleSessions;
    private bool _showDisabled;
    private bool _isRemoveTarget;

    public MainViewModel()
    {
        _routing = new RoutingService(_routeStore, AudioBackendFactory.Current);
        _reconciler = new RouteReconciler(_routeStore, AudioBackendFactory.Current);

        SessionsView = CollectionViewSource.GetDefaultView(_sessions);

        // 排序：正在播放优先 → 名称升序。
        // 刻意不用「音量降序」：音量每秒变化会让条目互相穿越跳动（抖动感）。
        SessionsView.SortDescriptions.Add(
            new SortDescription(nameof(AppSession.IsPlaying), ListSortDirection.Descending));
        SessionsView.SortDescriptions.Add(
            new SortDescription(nameof(AppSession.DisplayName), ListSortDirection.Ascending));

        SessionsView.GroupDescriptions.Add(
            new PropertyGroupDescription(nameof(AppSession.GroupTitle)));

        SessionsView.Filter = FilterSession;

        RefreshCommand = new RelayCommand(RefreshFromUser);
        ToggleDisabledCommand = new RelayCommand(() => ShowDisabled = !ShowDisabled);
        RemoveRouteCommand = new RelayCommand<RouteChip>(RemoveRoute);
        ToastActionCommand = new RelayCommand<ToastModel>(ExecuteToastAction);
        DismissToastCommand = new RelayCommand<ToastModel>(DismissToast);

        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _timer.Tick += (_, _) => OnTick();
        _timer.Start();

        // 切换语言后，模型里由代码拼出来的文案（设备类型 / 拖放提示 / 分组名）需要重算
        LocalizationService.Instance.LanguageChanged += (_, _) => OnLanguageChanged();

        RefreshLive();
    }

    private static IAudioBackend Backend => AudioBackendFactory.Current;

    public ICollectionView SessionsView { get; }

    public ObservableCollection<AudioDevice> AvailableDevices { get; } = new();

    /// <summary>已禁用 / 已断开 / 未插入的输出设备，默认折叠。</summary>
    public ObservableCollection<AudioDevice> DisabledDevices { get; } = new();

    public ObservableCollection<ToastModel> Toasts { get; } = new();

    public RelayCommand RefreshCommand { get; }

    public RelayCommand ToggleDisabledCommand { get; }

    public RelayCommand<RouteChip> RemoveRouteCommand { get; }

    public RelayCommand<ToastModel> ToastActionCommand { get; }

    public RelayCommand<ToastModel> DismissToastCommand { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!Set(ref _searchText, value)) return;
            SessionsView.Refresh();
            HasVisibleSessions = SessionsView.Cast<object>().Any();
            NotifyEmptyStates();
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => Set(ref _statusText, value);
    }

    public string EngineText
    {
        get => _engineText;
        private set => Set(ref _engineText, value);
    }

    public bool IsDemo
    {
        get => _isDemo;
        private set => Set(ref _isDemo, value);
    }

    public bool HasSessions
    {
        get => _hasSessions;
        private set => Set(ref _hasSessions, value);
    }

    public bool HasVisibleSessions
    {
        get => _hasVisibleSessions;
        private set => Set(ref _hasVisibleSessions, value);
    }

    public bool ShowDisabled
    {
        get => _showDisabled;
        set
        {
            if (!Set(ref _showDisabled, value)) return;
            OnPropertyChanged(nameof(DisabledHeaderText));
        }
    }

    /// <summary>拖拽 chip 到左侧应用区：显示「释放以移除路由」落点提示。</summary>
    public bool IsRemoveTarget
    {
        get => _isRemoveTarget;
        set => Set(ref _isRemoveTarget, value);
    }

    public bool HasSearchText => !string.IsNullOrWhiteSpace(_searchText);

    public bool ShowEmptyState => !_hasSessions;

    public bool ShowNoMatchState => _hasSessions && !_hasVisibleSessions;

    public int SessionCount => _sessions.Count;

    public int DeviceCount => _deviceIndex.Count;

    public string SessionCountText => _sessions.Count.ToString();

    /// <summary>
    /// 栏头徽章只显示「可用设备数」——与「应用程序」栏头保持一致。
    /// 刻意不再显示「N 默认 / X/Y 在线」：默认设备卡片上已有 ★ 徽章，
    /// 失效端点数在折叠分组标题里已经写了，重复展示就是噪音。
    /// </summary>
    public string DeviceCountText => AvailableDevices.Count.ToString();

    public string DisabledHeaderText =>
        $"{(ShowDisabled ? "▾" : "▸")} {Loc.T("device.disabledGroup")} ({DisabledDevices.Count})";

    public bool HasDisabledDevices => DisabledDevices.Count > 0;

    public int RouteCount =>
        AvailableDevices.Sum(d => d.RouteCount) + DisabledDevices.Sum(d => d.RouteCount);

    /// <summary>路由状态是否已生效（由核心按后端能力如实描述）。</summary>
    public string RoutingStatusText => _routing.Describe();

    /// <summary>技术细节（后端名 + 受限原因），用于状态栏 Tooltip —— 不占用宝贵的横向空间。</summary>
    public string RoutingStatusDetail => _routing.Detail;

    public IReadOnlyList<AudioDevice> MenuDevices => AvailableDevices.ToList();

    private IEnumerable<AudioDevice> AllDevices() => AvailableDevices.Concat(DisabledDevices);

    private void OnLanguageChanged()
    {
        foreach (var session in _sessions) session.RaiseAllPropertiesChanged();
        foreach (var device in AllDevices()) device.RaiseAllPropertiesChanged();

        SessionsView.Refresh();
        UpdateDerivedState();
    }

    // ======================================================================
    //  Toast（右下角浮层 + 撤销）
    // ======================================================================

    private void ShowToast(string message, ToastKind kind = ToastKind.Success, int seconds = 4)
        => AddToast(new ToastModel
        {
            Message = message,
            Kind = kind,
            ExpiresAt = DateTime.Now.AddSeconds(seconds),
        });

    /// <summary>
    /// 带撤销的提示。刻意给到 12 秒：撤销是「给用户反悔的机会」，
    /// 几秒就消失等于没给（设计文档里的 3s 主要针对纯告知型提示）。
    /// </summary>
    private void ShowUndoToast(string message, Action undo, ToastKind kind = ToastKind.Success, int seconds = 12)
        => AddToast(new ToastModel
        {
            Message = message,
            Kind = kind,
            ActionText = Loc.T("action.undo"),
            Action = undo,
            ExpiresAt = DateTime.Now.AddSeconds(seconds),
        });

    private void AddToast(ToastModel toast)
    {
        while (Toasts.Count >= MaxToasts) Toasts.RemoveAt(0);

        Toasts.Add(toast);
        StartupLog.Write($"toast[{toast.Kind}]: {toast.Message}{(toast.HasAction ? " (可撤销)" : string.Empty)}");
    }

    /// <summary>供窗口层弹一条提示（语言切换、导入结果等）。</summary>
    public void Notify(string message, bool isError = false)
        => ShowToast(message, isError ? ToastKind.Warning : ToastKind.Success, isError ? 6 : 4);

    private void DismissToast(ToastModel toast)
    {
        if (toast is not null) Toasts.Remove(toast);
    }

    private void ExecuteToastAction(ToastModel toast)
    {
        if (toast is null) return;

        try
        {
            toast.Action?.Invoke();
        }
        catch (Exception ex)
        {
            StartupLog.Write($"toast action failed: {ex.Message}");
        }

        Toasts.Remove(toast);
    }

    private void PruneToasts()
    {
        var now = DateTime.Now;
        for (var i = Toasts.Count - 1; i >= 0; i--)
        {
            if (Toasts[i].ExpiresAt <= now) Toasts.RemoveAt(i);
        }
    }

    // ======================================================================
    //  路由操作
    // ======================================================================

    /// <summary>
    /// 幂等新增路由：同一 (Pid, DeviceId) 不重复添加。
    /// 首次落点记为 Route，已有路由再落到第二个设备记为 Duplicate（对应核心 flag 2 = 复制）。
    /// </summary>
    public RoutingOutcome TryAddRoutes(IReadOnlyList<AppSession> sessions, AudioDevice device)
    {
        if (sessions.Count == 0) return RoutingOutcome.NotImplemented;

        if (device.IsUnavailable)
        {
            ShowToast(Loc.T("toast.deviceUnavailable"), ToastKind.Warning, seconds: 3);
            return RoutingOutcome.DeviceUnavailable;
        }

        var added = new List<RouteChip>();

        foreach (var session in sessions)
        {
            if (device.HasRouteFor(session.Pid)) continue;

            var mode = session.RouteCount > 0 ? RouteMode.Duplicate : RouteMode.Route;
            var chip = RouteChip.From(session, mode);
            device.Routes.Add(chip);

            _routing.Route(new RouteRecord
            {
                Key = chip.RouteKey,
                ExePath = session.ExePath,
                ProcessName = session.ProcessName,
                DisplayName = session.DisplayName,
                DeviceId = device.Id,
                DeviceName = device.FriendlyName,
                Mode = mode,
                LastPid = session.Pid,
            });

            added.Add(chip);
        }

        if (added.Count == 0)
        {
            ShowToast(Loc.F("toast.alreadyRouted", sessions[0].DisplayName), ToastKind.Info, seconds: 3);
            return RoutingOutcome.AlreadyRouted;
        }

        RecomputeRouteCounts();
        UpdateDerivedState();

        var label = sessions.Count > 1 ? Loc.F("app.countMany", sessions.Count) : sessions[0].DisplayName;
        ShowUndoToast(Loc.F("toast.routed", label, device.FriendlyName), () =>
        {
            foreach (var chip in added) device.Routes.Remove(chip);
            RecomputeRouteCounts();
            UpdateDerivedState();
        });

        return RoutingOutcome.Applied;
    }

    public void RemoveRoute(RouteChip chip)
    {
        if (chip is null) return;

        var device = FindDevice(chip);
        if (device is null) return;

        var index = device.Routes.IndexOf(chip);
        if (index < 0) return;

        device.Routes.RemoveAt(index);

        // 按身份键解除（不靠 PID）：应用退出后 CLI 也能解除同一条路由
        _routing.Unroute(chip.RouteKey, device.Id);
        RecomputeRouteCounts();
        UpdateDerivedState();

        ShowUndoToast(Loc.F("toast.removed", device.FriendlyName, chip.DisplayName), () =>
        {
            var at = Math.Clamp(index, 0, device.Routes.Count);
            device.Routes.Insert(at, chip);
            RecomputeRouteCounts();
            UpdateDerivedState();
        });
    }

    public AudioDevice? FindDevice(RouteChip chip) => AllDevices().FirstOrDefault(d => d.Routes.Contains(chip));

    /// <summary>
    /// 真实静音：走各平台后端的公开接口（Windows 用 ISimpleAudioVolume，
    /// Linux 用 set-sink-input-mute），不需要注入式核心。
    /// 下一次会话刷新会从后端读回真值，UI 不会与系统状态脱节。
    /// </summary>
    public void ToggleAppMute(AppSession session)
    {
        var target = !session.IsMuted;

        if (IsDemo)
        {
            session.IsMuted = target;
            ShowToast(Loc.F(target ? "toast.demoMuted" : "toast.demoUnmuted", session.DisplayName),
                      ToastKind.Info, seconds: 3);
            return;
        }

        if (!Backend.SetProcessMute(session.Pid, target))
        {
            ShowToast(Loc.F("toast.noSession", session.DisplayName), ToastKind.Warning, seconds: 3);
            StartupLog.Write($"mute: pid={session.Pid} 失败（无可操作会话）");
            return;
        }

        session.IsMuted = target;
        StartupLog.Write($"mute(真实生效): pid={session.Pid} muted={target}");

        ShowUndoToast(Loc.F(target ? "toast.muted" : "toast.unmuted", session.DisplayName), () =>
        {
            if (Backend.SetProcessMute(session.Pid, !target))
            {
                session.IsMuted = !target;
            }
        });
    }

    /// <summary>
    /// 仅在此设备静音。Linux 后端能精确做到（该流在这台设备上静音）；
    /// Windows 需要核心按路由单独控流，当前只记录状态。
    /// </summary>
    public void ToggleChipMute(RouteChip chip, AudioDevice device)
    {
        chip.IsMuted = !chip.IsMuted;

        var outcome = Backend.SetRouteMute(chip.Pid, device.Id, chip.IsMuted);
        var effective = outcome == RoutingOutcome.Applied;

        if (!effective)
        {
            StartupLog.Write(
                $"route-mute: pid={chip.Pid} device='{device.Id}' muted={chip.IsMuted} → 本地记录（{outcome}）");
        }

        ShowToast(
            Loc.F(chip.IsMuted ? "toast.chipMuted" : "toast.chipUnmuted", chip.DisplayName, device.FriendlyName),
            ToastKind.Info, seconds: 4);
    }

    /// <summary>「路由到 ▸」的条目点击：已在目标设备上则取消，否则新增。</summary>
    public void ToggleRouteTo(AppSession session, AudioDevice device)
    {
        var existing = device.Routes.FirstOrDefault(r => r.Pid == session.Pid);

        if (existing is not null)
        {
            RemoveRoute(existing);
            return;
        }

        TryAddRoutes(new[] { session }, device);
    }

    /// <summary>「复制到多个设备…」对话框确认：批量按 Duplicate 模式落点。</summary>
    public void DuplicateToDevices(AppSession session, IEnumerable<AudioDevice> targets)
    {
        var added = new List<(AudioDevice Device, RouteChip Chip)>();

        foreach (var device in targets)
        {
            if (device.IsUnavailable || device.HasRouteFor(session.Pid)) continue;

            var chip = RouteChip.From(session, RouteMode.Duplicate);
            device.Routes.Add(chip);

            _routing.Route(new RouteRecord
            {
                Key = chip.RouteKey,
                ExePath = session.ExePath,
                ProcessName = session.ProcessName,
                DisplayName = session.DisplayName,
                DeviceId = device.Id,
                DeviceName = device.FriendlyName,
                Mode = RouteMode.Duplicate,
                LastPid = session.Pid,
            });

            added.Add((device, chip));
        }

        if (added.Count == 0)
        {
            ShowToast(Loc.T("toast.allExist"), ToastKind.Info, seconds: 3);
            return;
        }

        RecomputeRouteCounts();
        UpdateDerivedState();

        ShowUndoToast(Loc.F("toast.copiedMany", session.DisplayName, added.Count), () =>
        {
            foreach (var (device, chip) in added) device.Routes.Remove(chip);
            RecomputeRouteCounts();
            UpdateDerivedState();
        });
    }

    /// <summary>chip 菜单「复制到其他设备」：按 Duplicate 模式再挂一条到目标设备。</summary>
    public void CopyRouteTo(RouteChip source, AudioDevice device)
    {
        if (device.IsUnavailable)
        {
            ShowToast(Loc.T("toast.deviceUnavailable"), ToastKind.Warning, seconds: 3);
            return;
        }

        if (device.HasRouteFor(source.Pid))
        {
            ShowToast(Loc.F("toast.alreadyRouted", source.DisplayName), ToastKind.Info, seconds: 3);
            return;
        }

        var chip = new RouteChip
        {
            Pid = source.Pid,
            RouteKey = source.RouteKey,
            DisplayName = source.DisplayName,
            Icon = source.Icon,
            AvatarBrush = source.AvatarBrush,
            Mode = RouteMode.Duplicate,
        };

        device.Routes.Add(chip);
        _routing.Route(new RouteRecord
        {
            Key = source.RouteKey,
            DisplayName = source.DisplayName,
            DeviceId = device.Id,
            DeviceName = device.FriendlyName,
            Mode = RouteMode.Duplicate,
            LastPid = source.Pid,
        });

        RecomputeRouteCounts();
        UpdateDerivedState();

        ShowUndoToast(Loc.F("toast.copied", source.DisplayName, device.FriendlyName), () =>
        {
            device.Routes.Remove(chip);
            RecomputeRouteCounts();
            UpdateDerivedState();
        });
    }

    /// <summary>应用的⇢徽章 = 它挂在几个设备上。</summary>
    private void RecomputeRouteCounts()
    {
        var devices = AllDevices().ToList();
        foreach (var session in _sessions)
        {
            session.RouteCount = devices.Count(d => d.HasRouteFor(session.Pid));
        }
    }

    // ======================================================================
    //  拖拽状态
    // ======================================================================

    public void SetDropTarget(AudioDevice? target, IReadOnlyList<int> pids)
    {
        var rejectsAll = target is not null &&
                         (target.IsUnavailable || pids.All(target.HasRouteFor));

        foreach (var device in AllDevices())
        {
            var isTarget = ReferenceEquals(device, target);
            device.IsDropTarget = isTarget;
            device.IsDropRejected = isTarget && rejectsAll;
        }
    }

    public void ClearDropTarget() => SetDropTarget(null, Array.Empty<int>());

    // ======================================================================
    //  数据刷新
    // ======================================================================

    private void OnTick()
    {
        _tick++;

        if (_tick % 3 == 0) RefreshLive();
        else RefreshSessionsOnly();
    }

    private void RefreshFromUser() => RefreshLive();

    private void RefreshLive()
    {
        IsDemo = !Backend.IsAvailable;
        var trace = !_firstRefreshLogged;

        if (trace) StartupLog.Write($"refresh#0: backend={Backend.Name} available={!IsDemo}");

        var sessions = IsDemo ? MockAudioService.Sessions() : Backend.EnumerateSessions();
        if (!IsDemo) Decorate(sessions);
        if (trace) StartupLog.Write($"refresh#0: sessions={sessions.Count}");
        ApplySessions(sessions);

        if (IsDemo)
        {
            ApplyDevices(MockAudioService.Devices());
        }
        else
        {
            if (trace) StartupLog.Write("refresh#0: enumerating devices…");
            var devices = Backend.EnumerateDevices();
            if (trace) StartupLog.Write($"refresh#0: devices={devices.Count}");
            ApplyDevices(devices);
        }

        RestoreRoutes(sessions);
        UpdateDerivedState();
        _firstRefreshLogged = true;
    }

    private void RefreshSessionsOnly()
    {
        var sessions = IsDemo ? MockAudioService.Sessions() : Backend.EnumerateSessions();
        if (!IsDemo) Decorate(sessions);

        ApplySessions(sessions);
        RestoreRoutes(sessions);
        UpdateDerivedState();
    }

    /// <summary>
    /// 把已保存的路由**自动套用到刚出现的应用**上。
    ///
    /// 这是"按可执行文件路径存"换来的能力：应用重启后 PID 变了，路由依然生效；
    /// 同一次运行内对同一 (键|设备|PID) 只下发一次，不会变成每秒重复改道。
    /// </summary>
    private void RestoreRoutes(IReadOnlyList<AppSession> sessions)
    {
        if (IsDemo || sessions.Count == 0) return;

        var devices = AvailableDevices.ToList();
        if (devices.Count == 0) return;

        var matches = _reconciler.Reconcile(sessions, devices);
        if (matches.Count == 0) return;

        var added = 0;
        foreach (var match in matches)
        {
            if (match.Device.HasRouteFor(match.Pid)) continue;

            var session = sessions.FirstOrDefault(s => s.Pid == match.Pid);
            if (session is null) continue;

            match.Device.Routes.Add(RouteChip.From(session, match.Record.Mode));
            added++;
        }

        if (added == 0) return;

        RecomputeRouteCounts();
        UpdateDerivedState();

        // 只在本次运行首次恢复时提示：之后每次应用启动都弹提示就是噪音
        if (_restoreNotified) return;

        _restoreNotified = true;
        ShowToast(Loc.F("toast.routesRestored", added), ToastKind.Info, seconds: 4);
    }

    /// <summary>
    /// 给核心模型补上表现层数据（图标 / 字母头像底色）。
    /// 核心不持有 UI 类型，因此这一步必须在 UI 层做 —— 这也是它留在 ViewModel 的原因。
    /// </summary>
    private static void Decorate(IReadOnlyList<AppSession> sessions)
    {
        foreach (var session in sessions)
        {
            session.Icon = AppIconService.GetIcon(session.ExePath);
            session.AvatarBrush = AvatarPalette.BrushFor(
                string.IsNullOrEmpty(session.ExePath) ? session.ProcessName : session.ExePath);
        }
    }

    /// <summary>按 PID 做差量合并：已存在的对象只更新可变字段，避免每秒重建列表造成闪烁。</summary>
    private void ApplySessions(IReadOnlyList<AppSession> fresh)
    {
        var freshPids = fresh.Select(s => s.Pid).ToHashSet();

        for (var i = _sessions.Count - 1; i >= 0; i--)
        {
            if (!freshPids.Contains(_sessions[i].Pid)) _sessions.RemoveAt(i);
        }

        var index = _sessions.ToDictionary(s => s.Pid);

        foreach (var item in fresh)
        {
            if (index.TryGetValue(item.Pid, out var current))
            {
                current.Volume = item.Volume;
                current.IsMuted = item.IsMuted;
                current.Peak = item.Peak;
                current.IsPlaying = item.IsPlaying;
            }
            else
            {
                _sessions.Add(item);
                index[item.Pid] = item;
            }
        }

        RecomputeRouteCounts();
    }

    private void ApplyDevices(IReadOnlyList<AudioDevice> fresh)
    {
        var freshIds = fresh.Select(d => d.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var stale in _deviceIndex.Keys.Where(k => !freshIds.Contains(k)).ToList())
        {
            _deviceIndex.Remove(stale);
        }

        foreach (var item in fresh)
        {
            if (_deviceIndex.TryGetValue(item.Id, out var current))
            {
                current.IsDefault = item.IsDefault;
                current.IsEnabled = item.IsEnabled;
            }
            else
            {
                _deviceIndex[item.Id] = item;
            }
        }

        var available = _deviceIndex.Values
            .Where(d => d.IsEnabled)
            .OrderByDescending(d => d.IsDefault)
            .ThenBy(d => d.FriendlyName, StringComparer.CurrentCulture)
            .ThenBy(d => d.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var disabled = _deviceIndex.Values
            .Where(d => !d.IsEnabled)
            .OrderBy(d => d.FriendlyName, StringComparer.CurrentCulture)
            .ThenBy(d => d.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        SyncCollection(AvailableDevices, available);
        SyncCollection(DisabledDevices, disabled);
    }

    private static void SyncCollection(ObservableCollection<AudioDevice> target, List<AudioDevice> desired)
    {
        var same = target.Count == desired.Count;
        if (same)
        {
            for (var i = 0; i < desired.Count; i++)
            {
                if (!ReferenceEquals(target[i], desired[i]))
                {
                    same = false;
                    break;
                }
            }
        }

        if (same) return;

        target.Clear();
        foreach (var device in desired) target.Add(device);
    }

    private void UpdateDerivedState()
    {
        HasSessions = _sessions.Count > 0;
        HasVisibleSessions = SessionsView.Cast<object>().Any();

        EngineText = Loc.T(IsDemo ? "status.engine.demo" : "status.engine.wasapi");
        StatusText = Loc.F("status.routes", RouteCount, _routing.Describe());

        PruneToasts();

        OnPropertyChanged(nameof(SessionCount));
        OnPropertyChanged(nameof(DeviceCount));
        OnPropertyChanged(nameof(SessionCountText));
        OnPropertyChanged(nameof(DeviceCountText));
        OnPropertyChanged(nameof(DisabledHeaderText));
        OnPropertyChanged(nameof(HasDisabledDevices));
        OnPropertyChanged(nameof(RouteCount));
        OnPropertyChanged(nameof(RoutingStatusText));
        OnPropertyChanged(nameof(RoutingStatusDetail));
        OnPropertyChanged(nameof(HasSearchText));
        NotifyEmptyStates();
    }

    private void NotifyEmptyStates()
    {
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(ShowNoMatchState));
    }

    private bool FilterSession(object item)
    {
        if (string.IsNullOrWhiteSpace(_searchText)) return true;
        return item is AppSession session &&
               session.SearchKey.Contains(_searchText.Trim().ToLowerInvariant(), StringComparison.Ordinal);
    }
}
