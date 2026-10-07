using System.Collections.ObjectModel;
using AudioRouter.Core.Localization;

namespace AudioRouter.Core.Models;

public enum DeviceKind
{
    Speakers,
    Headphones,
    Hdmi,
    Usb,
    Virtual,
    Bluetooth,
    Spdif,
    Other,
}

/// <summary>
/// 路由模式。取值与现有 C++ 核心的 <c>session_guid_and_flag</c> 高 2 位编码一致
/// （1 = Route，2 = Duplicate），接入核心时不需要新增协议。
/// </summary>
public enum RouteMode
{
    Route = 1,
    Duplicate = 2,
}

/// <summary>一条已路由记录（per-route 实体：同一应用可在多个设备各有一条）。</summary>
public sealed class RouteChip : ObservableObject
{
    private bool _isMuted;
    private bool _isDragging;
    private object? _icon;
    private object? _avatarBrush;

    public Guid RouteId { get; init; } = Guid.NewGuid();

    public int Pid { get; init; }

    /// <summary>持久化路由的身份键（exe 路径）。解除路由时用它定位记录，而不是靠 PID。</summary>
    public string RouteKey { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public RouteMode Mode { get; init; } = RouteMode.Route;

    /// <summary>表现层：图标（由 UI 层填充）。</summary>
    public object? Icon
    {
        get => _icon;
        set
        {
            if (!Set(ref _icon, value)) return;
            OnPropertyChanged(nameof(HasIcon));
        }
    }

    /// <summary>表现层：字母头像底色（由 UI 层填充）。</summary>
    public object? AvatarBrush
    {
        get => _avatarBrush;
        set => Set(ref _avatarBrush, value);
    }

    public bool HasIcon => _icon is not null;

    public string Initial => AppSession.InitialFor(DisplayName);

    public bool IsMuted
    {
        get => _isMuted;
        set => Set(ref _isMuted, value);
    }

    /// <summary>交互态：正在被拖拽。</summary>
    public bool IsDragging
    {
        get => _isDragging;
        set => Set(ref _isDragging, value);
    }

    public bool IsDuplicate => Mode == RouteMode.Duplicate;

    /// <summary>模式徽章：仅「复制」需要显示（Route 是默认语义，不额外标注）。</summary>
    public string ModeText => IsDuplicate ? Loc.T("route.duplicate") : string.Empty;

    /// <summary>无障碍名称：chip 的移除按钮是纯图标按钮，必须有名字。</summary>
    public string RemoveText => Loc.T("action.removeRoute");

    public string RemoveHint => Loc.T("a11y.removeRouteHint");

    public static RouteChip From(AppSession session, RouteMode mode) => new()
    {
        Pid = session.Pid,
        RouteKey = Routing.RouteKey.For(session.ExePath, session.ProcessName),
        DisplayName = session.DisplayName,
        Icon = session.Icon,
        AvatarBrush = session.AvatarBrush,
        Mode = mode,
    };
}

/// <summary>一个音频输出设备（渲染端点）。</summary>
public sealed class AudioDevice : ObservableObject
{
    private bool _isDefault;
    private bool _isEnabled = true;
    private bool _isDropTarget;
    private bool _isDropRejected;
    private bool _isDragging;

    public string Id { get; init; } = string.Empty;

    public string FriendlyName { get; init; } = string.Empty;

    public DeviceKind Kind { get; init; } = DeviceKind.Other;

    public bool IsActive { get; init; }

    /// <summary>格式摘要，如「24 bit · 48 kHz · 2ch」。取不到时为 null（界面不显示，不编造）。</summary>
    public string? FormatText { get; init; }

    public bool HasFormat => !string.IsNullOrWhiteSpace(FormatText);

    /// <summary>卡片徽章文案（模板里拿不到视图模型，因此由模型提供，与 KindText 同一套路）。</summary>
    public string BadgeDefaultText => Loc.T("device.default");

    public string BadgeDisabledText => Loc.T("device.disabled");

    public ObservableCollection<RouteChip> Routes { get; } = new();

    public AudioDevice()
    {
        // 关键：Routes 是集合，但界面上的「有没有路由」「路由数」是**派生属性**。
        // 集合变化不会自动让派生属性发通知 —— 漏了这层订阅，
        // chip 区域的可见性绑定就会永远停在旧值（表现为：计数对了，chip 却不显示）。
        Routes.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasRoutes));
            OnPropertyChanged(nameof(RouteCount));
        };
    }

    public bool IsDefault
    {
        get => _isDefault;
        set => Set(ref _isDefault, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (!Set(ref _isEnabled, value)) return;
            OnPropertyChanged(nameof(IsUnavailable));
            OnPropertyChanged(nameof(StateText));
            OnPropertyChanged(nameof(DropHint));
        }
    }

    public bool IsUnavailable => !_isEnabled;

    /// <summary>交互态：拖拽悬停命中本设备。</summary>
    public bool IsDropTarget
    {
        get => _isDropTarget;
        set
        {
            if (!Set(ref _isDropTarget, value)) return;
            OnPropertyChanged(nameof(DropHint));
        }
    }

    /// <summary>交互态：拖拽悬停但该应用已在此设备上（幂等拒绝）。</summary>
    public bool IsDropRejected
    {
        get => _isDropRejected;
        set
        {
            if (!Set(ref _isDropRejected, value)) return;
            OnPropertyChanged(nameof(DropHint));
        }
    }

    /// <summary>交互态：本设备卡片内的 chip 正在被拖拽。</summary>
    public bool IsDragging
    {
        get => _isDragging;
        set => Set(ref _isDragging, value);
    }

    public string DropHint
    {
        get
        {
            if (IsDropRejected) return Loc.T("device.dropRejected");
            if (IsUnavailable) return Loc.T("device.unavailable");
            if (IsDropTarget) return Loc.F("device.dropHintTarget", FriendlyName);
            return Loc.T("device.dropHint");
        }
    }

    public bool HasRoutes => Routes.Count > 0;

    public bool UsesHeadphoneIcon => Kind is DeviceKind.Headphones or DeviceKind.Bluetooth;

    public string KindText => Kind switch
    {
        DeviceKind.Speakers => Loc.T("device.kind.speakers"),
        DeviceKind.Headphones => Loc.T("device.kind.headphones"),
        DeviceKind.Hdmi => Loc.T("device.kind.hdmi"),
        DeviceKind.Usb => Loc.T("device.kind.usb"),
        DeviceKind.Virtual => Loc.T("device.kind.virtual"),
        DeviceKind.Bluetooth => Loc.T("device.kind.bluetooth"),
        DeviceKind.Spdif => Loc.T("device.kind.spdif"),
        _ => Loc.T("device.kind.other"),
    };

    public string StateText => _isEnabled
        ? (IsActive ? Loc.T("device.state.online") : Loc.T("device.state.unavailable"))
        : Loc.T("device.state.disabled");

    public bool HasRouteFor(int pid) => Routes.Any(r => r.Pid == pid);

    public int RouteCount => Routes.Count;
}
