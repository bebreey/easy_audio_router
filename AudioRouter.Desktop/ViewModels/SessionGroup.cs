using System.Collections.ObjectModel;
using AudioRouter.Core.Models;

namespace AudioRouter.Desktop.ViewModels;

/// <summary>
/// 应用列表的分组（正在播放 / 空闲·已静音）。
///
/// Avalonia 侧不依赖 CollectionView：分组由视图模型直接表达，更简单也更好测。
/// 注意构造函数里订阅了 Items.CollectionChanged —— 这是**故意的**：
/// WPF 版曾因为漏掉集合变化通知，导致「计数对了但列表项不显示」，
/// 这类派生属性通知漏一次就是静默空白，所以这里从设计上堵住。
/// </summary>
internal sealed class SessionGroup : ObservableObject
{
    private string _title;
    private bool _isExpanded = true;

    public SessionGroup(string title)
    {
        _title = title;
        Items.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasItems));
            OnPropertyChanged(nameof(Count));
        };
    }

    public string Title
    {
        get => _title;
        set
        {
            if (!Set(ref _title, value)) return;

            // TitleWithCount 由 Title 派生：改了 Title 必须连带通知它，
            // 否则切换语言后分组标题会停在旧语言（本轮实测踩到）。
            OnPropertyChanged(nameof(TitleWithCount));
        }
    }

    /// <summary>折叠状态（空闲组可折叠，减少噪音）。</summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set => Set(ref _isExpanded, value);
    }

    public ObservableCollection<AppSession> Items { get; } = new();

    public int Count => Items.Count;

    public bool HasItems => Items.Count > 0;

    /// <summary>标题里带计数，例如「正在播放 1」。</summary>
    public string TitleWithCount => $"{_title} {Items.Count}";
}
