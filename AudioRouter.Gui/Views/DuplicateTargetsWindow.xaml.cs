using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AudioRouter.Core.Localization;
using AudioRouter.Core.Models;

namespace AudioRouter.Gui.Views;

/// <summary>
/// 「复制到多个设备…」对话框：勾选多个目标设备，一次按 Duplicate 模式批量落点。
/// 已路由的设备默认勾选且置灰，避免重复添加（与拖拽的幂等规则一致）。
/// </summary>
public partial class DuplicateTargetsWindow : Window
{
    private readonly List<(AudioDevice Device, CheckBox Box)> _targets = new();

    public DuplicateTargetsWindow(AppSession session, IReadOnlyList<AudioDevice> devices)
    {
        InitializeComponent();

        Subtitle.Text = Loc.F("dialog.duplicate.subtitle", session.DisplayName);

        foreach (var device in devices)
        {
            var already = device.HasRouteFor(session.Pid);

            var box = new CheckBox
            {
                Content = already
                    ? Loc.F("dialog.duplicate.already", device.FriendlyName)
                    : device.FriendlyName,
                IsChecked = already,
                IsEnabled = !already,
                Margin = new Thickness(0, 6, 0, 6),
            };
            box.Checked += (_, _) => UpdateSummary();
            box.Unchecked += (_, _) => UpdateSummary();

            _targets.Add((device, box));
            TargetList.Children.Add(box);
        }

        UpdateSummary();
    }

    /// <summary>用户勾选、且尚未路由的目标设备。</summary>
    public IReadOnlyList<AudioDevice> SelectedDevices =>
        _targets.Where(t => t.Box.IsChecked == true && t.Box.IsEnabled).Select(t => t.Device).ToList();

    private void UpdateSummary()
    {
        var count = SelectedDevices.Count;
        Summary.Text = count == 0
            ? Loc.T("dialog.duplicate.none")
            : Loc.F("dialog.duplicate.summary", count);
        ApplyButton.IsEnabled = count > 0;
    }

    private void Apply_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Root_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;

        try
        {
            DragMove();
        }
        catch
        {
            // 拖拽期间窗口状态变化会抛异常，忽略
        }
    }
}
