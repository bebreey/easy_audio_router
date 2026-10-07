namespace AudioRouter.Core.Models;

public enum ToastKind
{
    Success,
    Info,
    Warning,
    Error,
}

/// <summary>
/// 浮层提示（右下角）。带可选操作（撤销），到点自动消失。
/// 由各 UI 的视图模型统一持有与回收，不额外起计时器。
/// 平台无关，因此放在核心层由 WPF / Avalonia 共用。
/// </summary>
public sealed class ToastModel
{
    public Guid Id { get; } = Guid.NewGuid();

    public string Message { get; init; } = string.Empty;

    public ToastKind Kind { get; init; } = ToastKind.Info;

    /// <summary>操作按钮文案，例如「撤销」。</summary>
    public string? ActionText { get; init; }

    /// <summary>操作回调，例如把刚移除的路由放回去。</summary>
    public Action? Action { get; init; }

    public DateTime ExpiresAt { get; init; }

    public bool HasAction => !string.IsNullOrEmpty(ActionText) && Action is not null;

    /// <summary>是否为可撤销提示（决定操作按钮是否渲染）。</summary>
    public bool IsUndoable => HasAction;

    /// <summary>关闭按钮的无障碍名称（图标按钮必须有名字）。</summary>
    public string CloseText => Localization.Loc.T("action.closeToast");

    /// <summary>操作按钮的无障碍说明。</summary>
    public string ActionHint => Localization.Loc.T("a11y.undoAction");
}
