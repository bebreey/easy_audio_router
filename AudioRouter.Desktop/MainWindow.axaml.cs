using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using AudioRouter.Desktop.Converters;
using AudioRouter.Desktop.ViewModels;
using AudioRouter.Core.Diagnostics;
using AudioRouter.Core.Localization;
using AudioRouter.Core.Models;

namespace AudioRouter.Desktop;

/// <summary>
/// 主窗口：视图层 + **拖拽引擎**。
///
/// 【为什么自绘而不用平台 DnD】
/// Avalonia 12 的拖拽入口是
/// <c>DragDrop.DoDragDropAsync(PointerPressedEventArgs, IDataTransfer, DragDropEffects)</c> ——
/// 只接受「按下」事件参数。而拖拽必须在**移动超过阈值**之后才算开始，
/// 事件参数又是池化的（留到 Moved 里用是不可靠的）。
/// 因此这里用「指针捕获 + 命中测试」自绘：不依赖平台 DnD 语义，
/// 三平台行为一致，同时也拿到了设计稿要求的拖拽预览。
///
/// 业务判断（落点是否合法、幂等、撤销）仍然全部在视图模型 + 核心层，
/// 这里只把"谁拖到谁上面"翻译成一次视图模型调用。
/// </summary>
public partial class MainWindow : Window
{
    private const double DragThreshold = 6;

    private enum DragState
    {
        None,
        Pending,
        Dragging,
    }

    private DragState _state;
    private Point _origin;
    private object? _payload;
    private int[] _pids = Array.Empty<int>();
    private AudioDevice? _hoverDevice;
    private bool _overRemoveZone;

    public MainWindow()
    {
        InitializeComponent();

        // 窗口图标：与 exe 内嵌 .ico 同源的 PNG（由 --export-icon 生成）
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app.png");
            if (File.Exists(iconPath)) Icon = new WindowIcon(iconPath);
        }
        catch (Exception ex)
        {
            StartupLog.Write($"icon: 窗口图标加载失败：{ex.Message}");
        }

        DataContext = new MainViewModel();
        StartupLog.Write("avalonia: main window created (view model live)");
    }

    private MainViewModel ViewModel => (MainViewModel)DataContext!;

    // ======================================================================
    //  拖拽源：应用行 / 设备卡上的 chip
    // ======================================================================

    private void OnDragSourcePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control) return;
        if (!e.GetCurrentPoint(control).Properties.IsLeftButtonPressed) return;

        _state = DragState.Pending;
        _origin = e.GetPosition(this);
        _payload = control.DataContext;
        _pids = Array.Empty<int>();

        e.Pointer.Capture(control);
    }

    private void OnDragSourcePointerMoved(object? sender, PointerEventArgs e)
    {
        if (_state == DragState.None || _payload is null) return;

        var position = e.GetPosition(this);

        if (_state == DragState.Pending)
        {
            // 阈值：避免"点一下"被误判成拖拽
            if (Math.Abs(position.X - _origin.X) < DragThreshold &&
                Math.Abs(position.Y - _origin.Y) < DragThreshold)
            {
                return;
            }

            BeginDrag();
        }

        UpdateDrag(position);
    }

    private void OnDragSourcePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_state != DragState.Dragging)
        {
            Reset();
            return;
        }

        var position = e.GetPosition(this);
        CompleteDrag(position);
        Reset();
    }

    private void OnDragSourcePointerCaptureLost(object? sender, PointerCaptureLostEventArgs e) => Reset();

    private void BeginDrag()
    {
        _pids = _payload switch
        {
            AppSession session => new[] { session.Pid },
            RouteChip chip => new[] { chip.Pid },
            _ => Array.Empty<int>(),
        };

        if (_pids.Length == 0)
        {
            Reset();
            return;
        }

        _state = DragState.Dragging;
        SetSourceDragging(true);

        var label = _payload switch
        {
            AppSession session => session.DisplayName,
            RouteChip chip => chip.DisplayName,
            _ => string.Empty,
        };

        ShowPreview(label);
        StartupLog.Write($"drag: begin ({(_payload is RouteChip ? "Chip" : "App")}) / payload={_pids.Length}");
    }

    private void UpdateDrag(Point position)
    {
        var device = FindDataContext<AudioDevice>(Avalonia.Input.InputExtensions.InputHitTest(this, position));
        var overRemove = IsOverRemoveZone(position) && _payload is RouteChip;

        if (!ReferenceEquals(device, _hoverDevice) || overRemove != _overRemoveZone)
        {
            _hoverDevice = device;
            _overRemoveZone = overRemove;

            ViewModel.SetDropTarget(device, _pids);
            ViewModel.IsRemoveTarget = overRemove;
        }

        MovePreview(position);
    }

    private void CompleteDrag(Point position)
    {
        // 注意：Avalonia 12 的 InputHitTest 是**普通静态辅助方法，不是扩展方法**，
        // 直接写 InputHitTest(point) 会报 CS0103，必须显式调用。
        var device = FindDataContext<AudioDevice>(Avalonia.Input.InputExtensions.InputHitTest(this, position));

        if (device is not null)
        {
            // 拖的是【已有路由标签】落在设备上 = 把这条路由移动到该设备。
            // 必须放在查会话之前：标签不是会话，_pids 为空，落到下面就会变成
            // TryAddRoutes(0 个会话) → NotImplemented（这曾经让"移动路由"静默失败）。
            if (_payload is RouteChip movedChip)
            {
                ViewModel.MoveRoute(movedChip, device);
                StartupLog.Write($"drop: Moved → chip '{movedChip.DisplayName}' → '{device.FriendlyName}'");
                return;
            }

            var sessions = ViewModel.SessionsByPids(_pids);
            var outcome = ViewModel.TryAddRoutes(sessions, device);
            StartupLog.Write($"drop: {outcome} → '{device.FriendlyName}' x{sessions.Count}");
            return;
        }

        if (IsOverRemoveZone(position) && _payload is RouteChip chip)
        {
            ViewModel.RemoveRoute(chip);
            StartupLog.Write($"drop: Removed → chip '{chip.DisplayName}'");
        }
    }

    private void Reset()
    {
        _state = DragState.None;
        SetSourceDragging(false);
        _payload = null;
        _pids = Array.Empty<int>();
        _hoverDevice = null;
        _overRemoveZone = false;

        DragPreview.IsVisible = false;
        ViewModel.ClearDropTarget();
        ViewModel.IsRemoveTarget = false;
    }

    /// <summary>拖拽源变暗到 45%（设计稿行为）。</summary>
    private void SetSourceDragging(bool value)
    {
        switch (_payload)
        {
            case AppSession session:
                session.IsDragging = value;
                break;
            case RouteChip chip:
                chip.IsDragging = value;
                break;
        }
    }

    // ======================================================================
    //  命中测试与预览
    // ======================================================================

    /// <summary>从命中元素向上找到第一个带着目标数据上下文的控件。</summary>
    private static T? FindDataContext<T>(IInputElement? element) where T : class
    {
        Visual? visual = element as Visual;

        while (visual is not null)
        {
            if (visual is Control control && control.DataContext is T match) return match;
            visual = visual.GetVisualParent();
        }

        return null;
    }

    /// <summary>左侧应用区 = 移除落点（仅 chip 拖拽有意义）。</summary>
    private bool IsOverRemoveZone(Point position)
    {
        var topLeft = AppsPanel.TranslatePoint(new Point(0, 0), this);
        if (topLeft is null) return false;

        var rect = new Rect(topLeft.Value, AppsPanel.Bounds.Size);
        return rect.Contains(position);
    }

    private void ShowPreview(string label)
    {
        PreviewLabel.Text = label;

        if (_payload is AppSession or RouteChip)
        {
            PreviewAvatar.Background = new NameToBrushConverter()
                .Convert(label, typeof(IBrush), null, CultureInfo.InvariantCulture) as IBrush;
        }

        var initial = label.Length > 0 ? char.ToUpperInvariant(label[0]).ToString() : "?";
        PreviewInitial.Text = initial;

        DragPreview.IsVisible = true;
    }

    private void MovePreview(Point position)
    {
        // 预览跟随光标，略微偏移避免挡住落点
        DragPreview.Margin = new Thickness(position.X + 12, position.Y + 14, 0, 0);
    }

    // ======================================================================
    //  右键菜单
    //
    //  与 WPF 版一致：菜单在代码里构建（动态设备列表天然适合代码构建），
    //  但**不需要** WPF 那套「占位子项」技巧 —— 这里在构建菜单时直接填充子菜单
    //  （设备列表在右键那一刻就是已知的）。
    // ======================================================================

    private void OnAppRowContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not AppSession session) return;

        ShowAppMenu(session, control);
        e.Handled = true;
    }

    private void OnChipContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not RouteChip chip) return;

        ShowChipMenu(chip, control);
        e.Handled = true;
    }

    private void ShowAppMenu(AppSession session, Control anchor)
    {
        var menu = new ContextMenu();

        var mute = new MenuItem
        {
            Header = Loc.T(session.IsMuted ? "menu.unmute" : "menu.mute"),
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = session.IsMuted,
        };
        mute.Click += (_, _) => ViewModel.ToggleAppMute(session);
        menu.Items.Add(mute);

        var duplicate = new MenuItem
        {
            Header = Loc.T("menu.duplicateToMany"),
            IsEnabled = ViewModel.MenuDevices.Count > 0,
        };
        duplicate.Click += async (_, _) => await ShowDuplicateDialogAsync(session);
        menu.Items.Add(duplicate);

        var routeTo = new MenuItem { Header = Loc.T("menu.routeTo") };
        var devices = ViewModel.MenuDevices;

        if (devices.Count == 0)
        {
            routeTo.Items.Add(new MenuItem { Header = Loc.T("menu.noDevices"), IsEnabled = false });
        }
        else
        {
            foreach (var device in devices)
            {
                var target = device;
                var item = new MenuItem
                {
                    Header = device.FriendlyName,
                    ToggleType = MenuItemToggleType.CheckBox,
                    IsChecked = device.HasRouteFor(session.Pid),
                };
                item.Click += (_, _) => ViewModel.ToggleRouteTo(session, target);
                routeTo.Items.Add(item);
            }
        }

        menu.Items.Add(routeTo);
        menu.Items.Add(new Separator());

        var refresh = new MenuItem { Header = Loc.T("menu.refreshSessions") };
        refresh.Click += (_, _) => ViewModel.RefreshCommand.Execute(null);
        menu.Items.Add(refresh);

        StartupLog.Write($"menu: app context menu for '{session.DisplayName}' ({devices.Count} candidate devices)");
        menu.Open(anchor);
    }

    private void ShowChipMenu(RouteChip chip, Control anchor)
    {
        var device = ViewModel.FindDevice(chip);
        if (device is null) return;

        var menu = new ContextMenu();

        var remove = new MenuItem { Header = Loc.T("menu.removeRoute") };
        if (this.FindResource("Brush.State.Danger") is IBrush danger) remove.Foreground = danger;
        remove.Click += (_, _) => ViewModel.RemoveRoute(chip);
        menu.Items.Add(remove);

        var mute = new MenuItem
        {
            Header = Loc.T(chip.IsMuted ? "menu.unmuteThisDevice" : "menu.muteThisDevice"),
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = chip.IsMuted,
        };
        mute.Click += (_, _) => ViewModel.ToggleChipMute(chip, device);
        menu.Items.Add(mute);

        var copyTo = new MenuItem { Header = Loc.T("menu.copyToOther") };
        var others = ViewModel.MenuDevices.Where(d => !ReferenceEquals(d, device)).ToList();

        if (others.Count == 0)
        {
            copyTo.Items.Add(new MenuItem { Header = Loc.T("menu.noOtherDevices"), IsEnabled = false });
        }
        else
        {
            foreach (var other in others)
            {
                var target = other;
                var exists = other.HasRouteFor(chip.Pid);
                var item = new MenuItem
                {
                    Header = other.FriendlyName,
                    ToggleType = MenuItemToggleType.CheckBox,
                    IsChecked = exists,
                    IsEnabled = !exists,
                };
                item.Click += (_, _) => ViewModel.CopyRouteTo(chip, target);
                copyTo.Items.Add(item);
            }
        }

        menu.Items.Add(copyTo);

        StartupLog.Write($"menu: chip context menu for '{chip.DisplayName}' on '{device.FriendlyName}'");
        menu.Open(anchor);
    }

    private async Task ShowDuplicateDialogAsync(AppSession session)
    {
        var devices = ViewModel.MenuDevices;
        if (devices.Count == 0) return;

        var dialog = new Views.DuplicateTargetsWindow(session, devices);

        // 与 WPF 版语义一致：确认才拿到选中集合
        var confirmed = await dialog.ShowDialog<bool>(this);
        StartupLog.Write($"dialog: duplicate targets closed (confirmed={confirmed})");

        if (confirmed) ViewModel.DuplicateToDevices(session, dialog.SelectedDevices);
    }

    // ======================================================================
    //  语言
    // ======================================================================

    private void OnLanguageClick(object? sender, RoutedEventArgs e)
    {
        var localization = LocalizationService.Instance;
        var menu = new ContextMenu();

        foreach (var pack in localization.AvailableLanguages)
        {
            var code = pack.Code;
            var item = new MenuItem
            {
                Header = pack.Name,
                ToggleType = MenuItemToggleType.CheckBox,
                IsChecked = string.Equals(pack.Code, localization.Current?.Code, StringComparison.OrdinalIgnoreCase),
            };

            item.Click += (_, _) =>
            {
                if (LocalizationService.Instance.ChangeLanguage(code))
                {
                    ViewModel.Notify(Loc.F("language.switched", code));
                }
            };

            menu.Items.Add(item);
        }

        menu.Items.Add(new Separator());

        var import = new MenuItem { Header = Loc.T("language.import") };
        import.Click += async (_, _) => await ImportLanguageFileAsync();
        menu.Items.Add(import);

        var openFolder = new MenuItem { Header = Loc.T("language.openFolder") };
        openFolder.Click += (_, _) => OpenLanguageFolder();
        menu.Items.Add(openFolder);

        StartupLog.Write($"language: menu opened ({localization.AvailableLanguages.Count} packs)");

        if (sender is Control anchor) menu.Open(anchor);
    }

    private async Task ImportLanguageFileAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Loc.T("language.import"),
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Language pack (*.json)") { Patterns = new[] { "*.json" } },
            },
        });

        if (files.Count == 0) return;

        var path = files[0].TryGetLocalPath();
        if (string.IsNullOrEmpty(path)) return;

        if (LocalizationService.Instance.Import(path, out var code, out var error))
        {
            LocalizationService.Instance.ChangeLanguage(code);
            ViewModel.Notify(Loc.F("language.import.ok", code));
        }
        else
        {
            ViewModel.Notify(Loc.F("language.import.failed", error ?? string.Empty), isError: true);
        }
    }

    private static void OpenLanguageFolder()
    {
        try
        {
            var directory = LocalizationService.UserDirectory;
            Directory.CreateDirectory(directory);

            Process.Start(new ProcessStartInfo { FileName = directory, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            StartupLog.Write($"language: 打开语言目录失败：{ex.Message}");
        }
    }

    // ======================================================================
    //  自绘标题栏
    //  （ExtendClientArea + NoChrome 之后，系统标题栏与三个窗口按钮都不再出现，
    //    这一行头栏就是标题栏，拖动/双击/最小化/最大化/关闭都得自己来）
    // ======================================================================

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // 最大化时不拖动：否则会出现"拖一下先还原再跟手"的跳动
        if (WindowState == WindowState.Maximized) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        // 点在按钮上不能变成拖窗口
        if (IsInsideButton(e.Source as IInputElement)) return;

        BeginMoveDrag(e);
    }

    private void OnTitleBarDoubleTapped(object? sender, TappedEventArgs e) => ToggleMaximize();

    private void OnMinimizeClick(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximizeClick(object? sender, RoutedEventArgs e) => ToggleMaximize();

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void ToggleMaximize()
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    /// <summary>最大化/还原时切换标题栏按钮图标（含系统手势/快捷键触发的状态变化）。</summary>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != WindowStateProperty) return;

        var maximized = WindowState == WindowState.Maximized;
        MaximizeIcon.IsVisible = !maximized;
        RestoreIcon.IsVisible = maximized;
    }

    private static bool IsInsideButton(IInputElement? element)
    {
        Visual? visual = element as Visual;

        while (visual is not null)
        {
            if (visual is Button) return true;
            visual = visual.GetVisualParent();
        }

        return false;
    }
}
