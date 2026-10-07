using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia.Threading;
using AudioRouter.Core.Backends;
using AudioRouter.Core.Diagnostics;
using AudioRouter.Core.Localization;
using AudioRouter.Core.Models;
using AudioRouter.Core.Mvvm;
using AudioRouter.Core.Routing;
using AudioRouter.Desktop.Services;

namespace AudioRouter.Desktop.ViewModels;

/// <summary>
/// Avalonia 视图模型。
///
/// 与 WPF 版共享同一份核心：音频枚举、静音、路由（按 exe 路径持久化 + 自动恢复）、
/// 语言、设置、命令基类全部来自 AudioRouter.Core。
/// 本层唯一的框架依赖是 <see cref="DispatcherTimer"/>（刷新节拍）与 UI 线程调度。
/// </summary>
internal sealed class MainViewModel : ObservableObject
{
    private const int MaxToasts = 3;

    private readonly ObservableCollection<AppSession> _sessions = new();
    private readonly Dictionary<string, AudioDevice> _deviceIndex = new(StringComparer.OrdinalIgnoreCase);
    private readonly DispatcherTimer _timer;

    private readonly RouteStore _routeStore = RouteStore.Load();
    private readonly IRoutingService _routing;
    private readonly RouteReconciler _reconciler;

    private int _tick;
    private string _searchText = string.Empty;
    private bool _isDemo;
    private bool _showDisabled;
    private bool _isRemoveTarget;
    private bool _restoreNotified;

    public MainViewModel()
    {
        _routing = new RoutingService(_routeStore, AudioBackendFactory.Current);
        _reconciler = new RouteReconciler(_routeStore, AudioBackendFactory.Current);

        PlayingGroup = new SessionGroup(Loc.T("group.playing"));
        IdleGroup = new SessionGroup(Loc.T("group.idle"));
        SessionGroups = new ObservableCollection<SessionGroup> { PlayingGroup, IdleGroup };

        RefreshCommand = new RelayCommand(RefreshLive);
        ToggleDisabledCommand = new RelayCommand(() => ShowDisabled = !ShowDisabled);
        ToggleGroupCommand = new RelayCommand<SessionGroup>(g => g.IsExpanded = !g.IsExpanded);
        RemoveRouteCommand = new RelayCommand<RouteChip>(RemoveRoute);
        ToastActionCommand = new RelayCommand<ToastModel>(ExecuteToastAction);
        DismissToastCommand = new RelayCommand<ToastModel>(DismissToast);

        LocalizationService.Instance.LanguageChanged += (_, _) => OnLanguageChanged();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => OnTick();
        _timer.Start();

        RefreshLive();
    }

    private static IAudioBackend Backend => AudioBackendFactory.Current;

    // ======================================================================
    //  数据
    // ======================================================================

    public SessionGroup PlayingGroup { get; }

    public SessionGroup IdleGroup { get; }

    public ObservableCollection<SessionGroup> SessionGroups { get; }

    public ObservableCollection<AudioDevice> AvailableDevices { get; } = new();

    public ObservableCollection<AudioDevice> DisabledDevices { get; } = new();

    public ObservableCollection<ToastModel> Toasts { get; } = new();

    // ======================================================================
    //  命令
    // ======================================================================

    public RelayCommand RefreshCommand { get; }

    public RelayCommand ToggleDisabledCommand { get; }

    public RelayCommand<SessionGroup> ToggleGroupCommand { get; }

    public RelayCommand<RouteChip> RemoveRouteCommand { get; }

    public RelayCommand<ToastModel> ToastActionCommand { get; }

    public RelayCommand<ToastModel> DismissToastCommand { get; }

    // ======================================================================
    //  状态
    // ======================================================================

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!Set(ref _searchText, value)) return;
            RebuildGroups();
        }
    }

    public bool IsDemo
    {
        get => _isDemo;
        private set => Set(ref _isDemo, value);
    }

    public bool ShowDisabled
    {
        get => _showDisabled;
        set
        {
            if (!Set(ref _showDisabled, value)) return;
            OnPropertyChanged(nameof(TextDisabledGroup));
        }
    }

    public int RouteCount =>
        AvailableDevices.Sum(d => d.RouteCount) + DisabledDevices.Sum(d => d.RouteCount);

    public bool HasAnyRoutes => RouteCount > 0;

    public bool HasSessions => _sessions.Count > 0;

    public bool HasVisibleSessions => PlayingGroup.HasItems || IdleGroup.HasItems;

    public bool HasDisabledDevices => DisabledDevices.Count > 0;

    /// <summary>把 chip 拖到左侧应用区 = 移除该路由（落点提示）。</summary>
    public bool IsRemoveTarget
    {
        get => _isRemoveTarget;
        set => Set(ref _isRemoveTarget, value);
    }

    public bool ShowEmptyState => !HasSessions;

    public bool ShowNoMatchState => HasSessions && !HasVisibleSessions;

    public string SessionCountText => _sessions.Count.ToString();

    public string DeviceCountText => AvailableDevices.Count.ToString();

    public string StatusText => Loc.F("status.routes", RouteCount, _routing.Describe());

    public string RoutingStatusDetail => _routing.Detail;

    // ======================================================================
    //  本地化文案（属性形式：编译期绑定可校验，切换语言时统一刷新）
    // ======================================================================

    public string TextAppsSection => Loc.T("section.apps");

    public string TextDevicesSection => Loc.T("section.devices");

    public string TextSearchPlaceholder => Loc.T("search.placeholder");

    public string TextEmptyNoAudioTitle => Loc.T("empty.noAudio.title");

    public string TextEmptyNoAudioSubtitle => Loc.T("empty.noAudio.subtitle");

    public string TextEmptyNoMatchTitle => Loc.T("empty.noMatch.title");

    public string TextEmptyNoMatchSubtitle => Loc.T("empty.noMatch.subtitle");

    public string TextDemoBadge => Loc.T("badge.demo");

    public string TextDeviceDefault => Loc.T("device.default");

    public string TextDeviceDisabled => Loc.T("device.disabled");

    public string TextRouteDuplicate => Loc.T("route.duplicate");

    public string TextUndo => Loc.T("action.undo");

    public string TextA11yRefresh => Loc.T("toolbar.refresh");

    public string TextA11yLanguage => Loc.T("toolbar.language");

    public string TextA11yRemoveRoute => Loc.T("action.removeRoute");

    public string TextA11yRemoveRouteHint => Loc.T("a11y.removeRouteHint");

    public string TextA11ySearchHint => Loc.T("a11y.searchHint");

    public string TextA11yUndo => Loc.T("a11y.undoAction");

    public string TextA11yCloseToast => Loc.T("action.closeToast");

    // 自绘标题栏的三个窗口按钮
    public string TextA11yMinimize => Loc.T("window.minimize");

    public string TextA11yMaximize => Loc.T("window.maximize");

    public string TextA11yClose => Loc.T("window.close");

    public string TextDisabledGroup =>
        $"{(ShowDisabled ? "▾" : "▸")} {Loc.T("device.disabledGroup")} ({DisabledDevices.Count})";

    private void OnLanguageChanged()
    {
        PlayingGroup.Title = Loc.T("group.playing");
        IdleGroup.Title = Loc.T("group.idle");

        foreach (var session in _sessions) session.RaiseAllPropertiesChanged();

        foreach (var device in AllDevices())
        {
            device.RaiseAllPropertiesChanged();

            // chip 是设备里的子集合，遍历设备**不会**覆盖到它 ——
            // 漏掉这一层，chip 上的「复制」徽章会停在旧语言（本轮实测踩到）。
            foreach (var chip in device.Routes) chip.RaiseAllPropertiesChanged();
        }

        // 视图模型自己持有的文案也必须刷新。
        // 漏掉这一组时，切换语言后「栏头 / 搜索框 / 空状态 / 无障碍名称」会停在旧语言
        // —— 本轮实测踩到（界面语言变了，栏头还是中文）。
        OnPropertyChanged(nameof(TextAppsSection));
        OnPropertyChanged(nameof(TextDevicesSection));
        OnPropertyChanged(nameof(TextSearchPlaceholder));
        OnPropertyChanged(nameof(TextEmptyNoAudioTitle));
        OnPropertyChanged(nameof(TextEmptyNoAudioSubtitle));
        OnPropertyChanged(nameof(TextEmptyNoMatchTitle));
        OnPropertyChanged(nameof(TextEmptyNoMatchSubtitle));
        OnPropertyChanged(nameof(TextDemoBadge));
        OnPropertyChanged(nameof(TextDeviceDefault));
        OnPropertyChanged(nameof(TextDeviceDisabled));
        OnPropertyChanged(nameof(TextRouteDuplicate));
        OnPropertyChanged(nameof(TextUndo));
        OnPropertyChanged(nameof(TextA11yRefresh));
        OnPropertyChanged(nameof(TextA11yLanguage));
        OnPropertyChanged(nameof(TextA11yRemoveRoute));
        OnPropertyChanged(nameof(TextA11yRemoveRouteHint));
        OnPropertyChanged(nameof(TextA11ySearchHint));
        OnPropertyChanged(nameof(TextA11yUndo));
        OnPropertyChanged(nameof(TextA11yCloseToast));
        OnPropertyChanged(nameof(TextA11yMinimize));
        OnPropertyChanged(nameof(TextA11yMaximize));
        OnPropertyChanged(nameof(TextA11yClose));
        OnPropertyChanged(nameof(TextDisabledGroup));

        RaiseDerived();
    }

    private IEnumerable<AudioDevice> AllDevices() => AvailableDevices.Concat(DisabledDevices);

    // ======================================================================
    //  Toast
    // ======================================================================

    public void Notify(string message, bool isError = false)
        => ShowToast(message, isError ? ToastKind.Warning : ToastKind.Success, isError ? 6 : 4);

    private void ShowToast(string message, ToastKind kind = ToastKind.Success, int seconds = 4)
        => AddToast(new ToastModel
        {
            Message = message,
            Kind = kind,
            ExpiresAt = DateTime.Now.AddSeconds(seconds),
        });

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

    private void DismissToast(ToastModel? toast)
    {
        if (toast is not null) Toasts.Remove(toast);
    }

    private void ExecuteToastAction(ToastModel? toast)
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
    //  路由操作（逻辑与 WPF 版一致，均落在核心）
    // ======================================================================

    public RoutingOutcome TryAddRoutes(IReadOnlyList<AppSession> sessions, AudioDevice device)
    {
        if (sessions.Count == 0) return RoutingOutcome.NotImplemented;

        if (device.IsUnavailable)
        {
            ShowToast(Loc.T("toast.deviceUnavailable"), ToastKind.Warning, 3);
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
            ShowToast(Loc.F("toast.alreadyRouted", sessions[0].DisplayName), ToastKind.Info, 3);
            return RoutingOutcome.AlreadyRouted;
        }

        RecomputeRouteCounts();
        RaiseDerived();

        var label = sessions.Count > 1 ? Loc.F("app.countMany", sessions.Count) : sessions[0].DisplayName;
        ShowUndoToast(Loc.F("toast.routed", label, device.FriendlyName), () =>
        {
            foreach (var chip in added) device.Routes.Remove(chip);
            RecomputeRouteCounts();
            RaiseDerived();
        });

        return RoutingOutcome.Applied;
    }

    /// <summary>
    /// 把一条已有路由从它所在的设备移动到另一台设备（拖动路由标签即为本操作）。
    ///
    /// 底层用 <c>RoutingService.Move</c> —— 它**不下发卸载**，只改记录后把整套重新下发。
    /// 这一点是刻意的：卸载是整条撤销，会把这次移动刚建立的设备列表一起撤掉，
    /// 结果拖动看起来"什么都没发生"（这正是之前那个 NotImplemented 之外的坑）。
    /// </summary>
    public void MoveRoute(RouteChip? chip, AudioDevice target)
    {
        if (chip is null) return;

        var source = FindDevice(chip);
        if (source is null || ReferenceEquals(source, target)) return;

        if (target.IsUnavailable)
        {
            ShowToast(Loc.T("toast.deviceUnavailable"), ToastKind.Warning, 3);
            return;
        }

        var index = source.Routes.IndexOf(chip);
        if (index < 0) return;

        var outcome = _routing.Move(chip.RouteKey, source.Id, target.Id, target.FriendlyName);

        if (outcome is RoutingOutcome.NotFound or RoutingOutcome.Failed)
        {
            return;   // 失败就不动界面，避免"看起来移动了其实没有"
        }

        source.Routes.RemoveAt(index);
        target.Routes.Add(chip);
        RecomputeRouteCounts();
        RaiseDerived();
    }
    public void RemoveRoute(RouteChip? chip)
    {
        if (chip is null) return;

        var device = FindDevice(chip);
        if (device is null) return;

        var index = device.Routes.IndexOf(chip);
        if (index < 0) return;

        device.Routes.RemoveAt(index);
        _routing.Unroute(chip.RouteKey, device.Id);
        RecomputeRouteCounts();
        RaiseDerived();

        ShowUndoToast(Loc.F("toast.removed", device.FriendlyName, chip.DisplayName), () =>
        {
            device.Routes.Insert(Math.Clamp(index, 0, device.Routes.Count), chip);
            RecomputeRouteCounts();
            RaiseDerived();
        });
    }

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

    // ======================================================================
    //  右键菜单用的能力
    // ======================================================================

    /// <summary>菜单里的候选设备（只列可用设备：失效设备不该出现在菜单里）。</summary>
    public IReadOnlyList<AudioDevice> MenuDevices => AvailableDevices.ToList();

    public AudioDevice? FindDevice(RouteChip chip)
        => AllDevices().FirstOrDefault(d => d.Routes.Contains(chip));

    /// <summary>chip 菜单「复制到其他设备」：按 Duplicate 模式再挂一条到目标设备。</summary>
    public void CopyRouteTo(RouteChip source, AudioDevice device)
    {
        if (device.IsUnavailable)
        {
            ShowToast(Loc.T("toast.deviceUnavailable"), ToastKind.Warning, 3);
            return;
        }

        if (device.HasRouteFor(source.Pid))
        {
            ShowToast(Loc.F("toast.alreadyRouted", source.DisplayName), ToastKind.Info, 3);
            return;
        }

        var chip = new RouteChip
        {
            Pid = source.Pid,
            RouteKey = source.RouteKey,
            DisplayName = source.DisplayName,
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
        RaiseDerived();

        ShowUndoToast(Loc.F("toast.copied", source.DisplayName, device.FriendlyName), () =>
        {
            device.Routes.Remove(chip);
            RecomputeRouteCounts();
            RaiseDerived();
        });
    }

    /// <summary>真实静音：走各平台后端的公开接口，不需要注入式核心。</summary>
    public void ToggleAppMute(AppSession? session)
    {
        if (session is null) return;

        var target = !session.IsMuted;

        if (IsDemo)
        {
            session.IsMuted = target;
            ShowToast(Loc.F(target ? "toast.demoMuted" : "toast.demoUnmuted", session.DisplayName),
                ToastKind.Info, 3);
            return;
        }

        if (!Backend.SetProcessMute(session.Pid, target))
        {
            ShowToast(Loc.F("toast.noSession", session.DisplayName), ToastKind.Warning, 3);
            return;
        }

        session.IsMuted = target;
        StartupLog.Write($"mute(真实生效): pid={session.Pid} muted={target}");

        ShowUndoToast(Loc.F(target ? "toast.muted" : "toast.unmuted", session.DisplayName), () =>
        {
            if (Backend.SetProcessMute(session.Pid, !target)) session.IsMuted = !target;
        });
    }

    public void ToggleChipMute(RouteChip? chip, AudioDevice? device)
    {
        if (chip is null || device is null) return;

        chip.IsMuted = !chip.IsMuted;
        Backend.SetRouteMute(chip.Pid, device.Id, chip.IsMuted);

        ShowToast(
            Loc.F(chip.IsMuted ? "toast.chipMuted" : "toast.chipUnmuted", chip.DisplayName, device.FriendlyName),
            ToastKind.Info, 4);
    }

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
            ShowToast(Loc.T("toast.allExist"), ToastKind.Info, 3);
            return;
        }

        RecomputeRouteCounts();
        RaiseDerived();

        ShowUndoToast(Loc.F("toast.copiedMany", session.DisplayName, added.Count), () =>
        {
            foreach (var (device, chip) in added) device.Routes.Remove(chip);
            RecomputeRouteCounts();
            RaiseDerived();
        });
    }

    private void RecomputeRouteCounts()
    {
        var devices = AllDevices().ToList();
        foreach (var session in _sessions)
        {
            session.RouteCount = devices.Count(d => d.HasRouteFor(session.Pid));
        }
    }

    // ======================================================================
    //  拖拽反馈
    // ======================================================================

    /// <summary>拖拽悬停：只高亮命中的设备，并区分「可落点」与「拒绝」。</summary>
    public void SetDropTarget(AudioDevice? target, IReadOnlyList<int> pids)
    {
        var rejects = target is not null && (target.IsUnavailable || pids.All(target.HasRouteFor));

        foreach (var device in AllDevices())
        {
            var isTarget = ReferenceEquals(device, target);
            device.IsDropTarget = isTarget;
            device.IsDropRejected = isTarget && rejects;
        }
    }

    public void ClearDropTarget() => SetDropTarget(null, Array.Empty<int>());

    /// <summary>把拖拽负载的 PID 还原成会话对象（拖拽通道只传 PID，不传引用）。</summary>
    public IReadOnlyList<AppSession> SessionsByPids(IEnumerable<int> pids)
    {
        var set = pids.ToHashSet();
        return _sessions.Where(s => set.Contains(s.Pid)).ToList();
    }

    // ======================================================================
    //  刷新
    // ======================================================================

    private void OnTick()
    {
        _tick++;
        if (_tick % 3 == 0) RefreshLive();
        else RefreshSessionsOnly();
    }

    private void RefreshLive()
    {
        IsDemo = !Backend.IsAvailable;

        var sessions = IsDemo ? DemoData.Sessions() : Backend.EnumerateSessions();
        if (!IsDemo) Decorate(sessions);
        ApplySessions(sessions);

        if (IsDemo) ApplyDevices(DemoData.Devices());
        else ApplyDevices(Backend.EnumerateDevices());

        RestoreRoutes(sessions);
        RaiseDerived();
    }

    private void RefreshSessionsOnly()
    {
        var sessions = IsDemo ? DemoData.Sessions() : Backend.EnumerateSessions();
        if (!IsDemo) Decorate(sessions);

        ApplySessions(sessions);
        RestoreRoutes(sessions);
        RaiseDerived();
    }

    /// <summary>
    /// 给核心模型补表现层数据（真实 exe 图标）。
    /// 核心不持有 UI 类型，因此这一步必须在 UI 层做 —— 这也是它留在 ViewModel 的原因。
    /// 取不到图标就留空，界面自动退回字母头像。
    /// </summary>
    private static void Decorate(IReadOnlyList<AppSession> sessions)
    {
        foreach (var session in sessions)
        {
            session.Icon = AppIconService.Get(session.ExePath);
            session.AvatarBrush = null; // 底色由 Avalonia 侧转换器按名字生成
        }
    }

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
        RaiseDerived();

        if (_restoreNotified) return;

        _restoreNotified = true;
        ShowToast(Loc.F("toast.routesRestored", added), ToastKind.Info, 4);
    }

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
        RebuildGroups();
    }

    /// <summary>重算两个分组的内容（分组由视图模型表达，不依赖 CollectionView）。</summary>
    private void RebuildGroups()
    {
        var query = _searchText.Trim().ToLowerInvariant();

        var visible = _sessions.Where(s =>
            query.Length == 0 || s.SearchKey.Contains(query, StringComparison.Ordinal)).ToList();

        Sync(PlayingGroup.Items,
            visible.Where(s => s.IsPlaying).OrderBy(s => s.DisplayName, StringComparer.CurrentCulture).ToList());

        Sync(IdleGroup.Items,
            visible.Where(s => !s.IsPlaying).OrderBy(s => s.DisplayName, StringComparer.CurrentCulture).ToList());

        PlayingGroup.Title = Loc.T("group.playing");
        IdleGroup.Title = Loc.T("group.idle");

        OnPropertyChanged(nameof(PlayingGroup));
        OnPropertyChanged(nameof(IdleGroup));
    }

    /// <summary>按引用做差量同步：避免每秒重建列表造成闪烁与滚动位置丢失。</summary>
    private static void Sync(ObservableCollection<AppSession> target, List<AppSession> desired)
    {
        if (target.Count == desired.Count)
        {
            var same = true;
            for (var i = 0; i < desired.Count; i++)
            {
                if (!ReferenceEquals(target[i], desired[i]))
                {
                    same = false;
                    break;
                }
            }

            if (same) return;
        }

        target.Clear();
        foreach (var session in desired) target.Add(session);
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

        SyncDevices(AvailableDevices, _deviceIndex.Values
            .Where(d => d.IsEnabled)
            .OrderByDescending(d => d.IsDefault)
            .ThenBy(d => d.FriendlyName, StringComparer.CurrentCulture)
            .ThenBy(d => d.Id, StringComparer.OrdinalIgnoreCase)
            .ToList());

        SyncDevices(DisabledDevices, _deviceIndex.Values
            .Where(d => !d.IsEnabled)
            .OrderBy(d => d.FriendlyName, StringComparer.CurrentCulture)
            .ThenBy(d => d.Id, StringComparer.OrdinalIgnoreCase)
            .ToList());
    }

    private static void SyncDevices(ObservableCollection<AudioDevice> target, List<AudioDevice> desired)
    {
        if (target.Count == desired.Count)
        {
            var same = true;
            for (var i = 0; i < desired.Count; i++)
            {
                if (!ReferenceEquals(target[i], desired[i]))
                {
                    same = false;
                    break;
                }
            }

            if (same) return;
        }

        target.Clear();
        foreach (var device in desired) target.Add(device);
    }

    private void RaiseDerived()
    {
        PruneToasts();

        OnPropertyChanged(nameof(RouteCount));
        OnPropertyChanged(nameof(HasAnyRoutes));
        OnPropertyChanged(nameof(HasSessions));
        OnPropertyChanged(nameof(HasVisibleSessions));
        OnPropertyChanged(nameof(HasDisabledDevices));
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(ShowNoMatchState));
        OnPropertyChanged(nameof(SessionCountText));
        OnPropertyChanged(nameof(DeviceCountText));
        OnPropertyChanged(nameof(TextDisabledGroup));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(RoutingStatusDetail));
    }
}
