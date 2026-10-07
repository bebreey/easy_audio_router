using AudioRouter.Core.Localization;

namespace AudioRouter.Core.Models;

/// <summary>
/// 一个正在发声的应用程序（按 PID 聚合其音频会话）。
///
/// 【关于表现层字段】<see cref="Icon"/> / <see cref="AvatarBrush"/> 用 <c>object?</c> 承载：
/// 具体类型由宿主 UI 决定（WPF 放 ImageSource，Avalonia 放 Bitmap），
/// 因此核心库**不引用任何 UI 框架类型**，同时又能被界面直接绑定。
/// 这两个槽位由 UI 层在枚举之后填充，核心自己不会去创建它们。
/// </summary>
public sealed class AppSession : ObservableObject
{
    private float _volume;
    private float _peak;
    private bool _isMuted;
    private bool _isPlaying;
    private bool _isDragging;
    private int _routeCount;
    private object? _icon;
    private object? _avatarBrush;

    public int Pid { get; init; }

    public string ExePath { get; init; } = string.Empty;

    public string ProcessName { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    /// <summary>表现层：应用图标（由 UI 层填充）。</summary>
    public object? Icon
    {
        get => _icon;
        set
        {
            if (!Set(ref _icon, value)) return;
            OnPropertyChanged(nameof(HasIcon));
        }
    }

    /// <summary>表现层：取不到图标时的字母头像底色（由 UI 层填充）。</summary>
    public object? AvatarBrush
    {
        get => _avatarBrush;
        set => Set(ref _avatarBrush, value);
    }

    /// <summary>有没有真实图标（没有就退回字母头像）。</summary>
    public bool HasIcon => _icon is not null;

    public float Volume
    {
        get => _volume;
        set
        {
            if (!Set(ref _volume, value)) return;
            OnPropertyChanged(nameof(VolumePercent));
        }
    }

    /// <summary>实时峰值 0..1。Windows 需要注入式核心才能拿到，其它平台由各自后端提供。</summary>
    public float Peak
    {
        get => _peak;
        set => Set(ref _peak, value);
    }

    public bool IsMuted
    {
        get => _isMuted;
        set => Set(ref _isMuted, value);
    }

    public bool IsPlaying
    {
        get => _isPlaying;
        set
        {
            if (!Set(ref _isPlaying, value)) return;
            OnPropertyChanged(nameof(StateText));
            OnPropertyChanged(nameof(GroupTitle));
        }
    }

    /// <summary>交互态：正在被拖拽（源项显示为 40% 透明）。</summary>
    public bool IsDragging
    {
        get => _isDragging;
        set => Set(ref _isDragging, value);
    }

    public int RouteCount
    {
        get => _routeCount;
        set
        {
            if (!Set(ref _routeCount, value)) return;
            OnPropertyChanged(nameof(HasRoutes));
        }
    }

    public bool HasRoutes => _routeCount > 0;

    public int VolumePercent => (int)Math.Round(_volume * 100);

    public string Subtitle => ProcessName.Length > 0 ? $"{ProcessName} · {Pid}" : ProcessName;

    public string GroupTitle => _isPlaying ? Loc.T("group.playing") : Loc.T("group.idle");

    public string StateText => _isPlaying ? Loc.T("app.state.playing") : Loc.T("app.state.idle");

    /// <summary>字母头像用的首字符。</summary>
    public string Initial => InitialFor(DisplayName);

    /// <summary>搜索过滤用的归一化键（小写）。</summary>
    public string SearchKey => $"{DisplayName} {ProcessName} {Pid}".ToLowerInvariant();

    public static string InitialFor(string name)
    {
        foreach (var ch in name)
        {
            if (char.IsLetterOrDigit(ch)) return char.ToUpperInvariant(ch).ToString();
        }

        return "?";
    }
}
