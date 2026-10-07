using Avalonia.Controls;
using AudioRouter.Core.Localization;
using AudioRouter.Core.Models;

namespace AudioRouter.Desktop.Views;

/// <summary>对话框里的一行：设备 + 勾选状态。</summary>
public sealed class DeviceChoice : ObservableObject
{
    private bool _isSelected;

    public required AudioDevice Device { get; init; }

    public bool IsSelected
    {
        get => _isSelected;
        set => Set(ref _isSelected, value);
    }
}

/// <summary>
/// 「复制到多个设备」模态对话框。
/// 返回值语义与 WPF 版一致：确认时才拿到选中集合；未选任何设备时「应用」按钮不可用。
/// </summary>
public partial class DuplicateTargetsWindow : Window
{
    private readonly List<DeviceChoice> _choices = new();

    /// <summary>XAML 设计器 / 无参构造用（不显示真实数据）。</summary>
    public DuplicateTargetsWindow()
    {
        InitializeComponent();
    }

    public DuplicateTargetsWindow(AppSession session, IReadOnlyList<AudioDevice> devices) : this()
    {
        Title = Loc.T("dialog.duplicate.title");
        TitleText.Text = Loc.T("dialog.duplicate.title");
        SubtitleText.Text = session.DisplayName;

        foreach (var device in devices)
        {
            var choice = new DeviceChoice { Device = device };
            choice.PropertyChanged += (_, _) => UpdateApplyState();
            _choices.Add(choice);
        }

        ChoicesControl.ItemsSource = _choices;

        CancelButton.Content = Loc.T("action.cancel");
        ApplyButton.Content = Loc.T("action.apply");

        HintText.Text = Loc.F("dialog.duplicate.hint", devices.Count);

        UpdateApplyState();

        Core.Diagnostics.StartupLog.Write(
            $"dialog: duplicate targets opened for '{session.DisplayName}' ({devices.Count} devices)");
    }

    /// <summary>确认后选中的设备。</summary>
    public IReadOnlyList<AudioDevice> SelectedDevices =>
        _choices.Where(c => c.IsSelected).Select(c => c.Device).ToList();

    private void UpdateApplyState() => ApplyButton.IsEnabled = _choices.Any(c => c.IsSelected);

    private void OnApply(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close(true);

    private void OnCancel(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close(false);
}
