using System.Windows.Media;

namespace AudioRouter.Gui.Models;

/// <summary>自绘拖拽预览窗口的数据（视觉模板见 Themes/Templates.xaml → DragPreviewTemplate）。</summary>
public sealed class DragPreviewModel
{
    public string DisplayName { get; init; } = "";

    public string Initial { get; init; } = "?";

    /// <summary>
    /// 用 <c>object?</c> 承载：值来自核心模型的同名字段（也是 object?），
    /// 运行时实际是 WPF 的 Brush，绑定到 Background 时类型检查会通过。
    /// </summary>
    public object? AvatarBrush { get; init; } = Brushes.Gray;

    /// <summary>多选拖拽时的额外数量，显示为 +N 角标。</summary>
    public int ExtraCount { get; init; }

    public bool HasExtra => ExtraCount > 0;

    public string ExtraText => $"+{ExtraCount}";
}
