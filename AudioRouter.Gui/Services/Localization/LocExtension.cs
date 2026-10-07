using System.Windows.Data;
using System.Windows.Markup;
using AudioRouter.Core.Localization;

namespace AudioRouter.Gui.Services.Localization;

/// <summary>
/// XAML 里取语言文案：<c>Text="{loc:Loc section.apps}"</c>
///
/// 实现成绑定而不是静态取值，是为了让切换语言时界面**实时刷新**：
/// LocalizationService 实现 INotifyPropertyChanged，索引器路径为 [key]，
/// 语言切换时对 "Item[]" 广播一次即可刷新全部引用。
/// </summary>
[MarkupExtensionReturnType(typeof(string))]
internal sealed class LocExtension : MarkupExtension
{
    public LocExtension()
    {
    }

    public LocExtension(string key) => Key = key;

    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding($"[{Key}]")
        {
            Source = LocalizationService.Instance,
            Mode = BindingMode.OneWay,
        };

        return binding.ProvideValue(serviceProvider);
    }
}
