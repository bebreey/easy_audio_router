using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AudioRouter.Core.Diagnostics;
using AudioRouter.Core.Localization;
using AudioRouter.Core.Models;
using AudioRouter.Core.Mvvm;
using AudioRouter.Gui.Models;
using AudioRouter.Gui.ViewModels;

namespace AudioRouter.Gui.Views;

public partial class MainWindow : Window
{
    private const int WmGetMinMaxInfo = 0x0024;
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaSystemBackdropType = 38;

    private const int DwmsbtMainWindow = 2;      // Mica
    private const int DwmsbtTransientWindow = 3; // Acrylic
    private const int DwmcpRound = 2;

    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;

    /// <summary>开始拖拽所需的最小位移（设计文档 5.2：4px）。</summary>
    private const double DragThreshold = 4;

    private readonly MainViewModel _viewModel = new();

    // ---- 拖拽状态 ----
    private enum DragSource
    {
        None,
        App,
        Chip,
    }

    private DragSource _dragSource = DragSource.None;
    private Point _dragStart;
    private bool _isDragging;
    private Window? _dragPreview;
    private AudioDevice? _dropTarget;
    private bool _isOverAppList;

    private AppSession? _dragApp;
    private List<AppSession> _dragPayload = new();
    private RouteChip? _dragChip;
    private AudioDevice? _dragChipOwner;

    public MainWindow()
    {
        InitializeComponent();

        // 窗口图标：与 Avalonia 版同一份 .ico（见 csproj 的 Resource 链接）
        try
        {
            Icon = new BitmapImage(new Uri("pack://application:,,,/Assets/app.ico"));
        }
        catch (Exception ex)
        {
            StartupLog.Write($"icon: 窗口图标加载失败：{ex.Message}");
        }
        DataContext = _viewModel;
        StateChanged += (_, _) => UpdateMaximizeGlyph();
        PreviewKeyDown += MainWindow_PreviewKeyDown;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var handle = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(handle)?.AddHook(WindowProc);

        ApplyWindowAppearance(handle);

        StartupLog.Write("main window: source initialized (window is up)");
    }

    /// <summary>
    /// 深色标题栏 + Win11 圆角 + 尝试挂 Mica 背景。
    /// 系统材质是增强项：失败不影响观感（底层已是 95% 不透明的深色）。
    /// </summary>
    private static void ApplyWindowAppearance(IntPtr handle)
    {
        TrySetDwm(handle, DwmwaUseImmersiveDarkMode, 1);
        TrySetDwm(handle, DwmwaWindowCornerPreference, DwmcpRound);

        if (Environment.OSVersion.Version.Build >= 22000)
        {
            if (!TrySetDwm(handle, DwmwaSystemBackdropType, DwmsbtMainWindow))
            {
                TrySetDwm(handle, DwmwaSystemBackdropType, DwmsbtTransientWindow);
            }
        }
    }

    private static bool TrySetDwm(IntPtr handle, int attribute, int value)
    {
        try
        {
            return DwmSetWindowAttribute(handle, attribute, ref value, sizeof(int)) == 0;
        }
        catch
        {
            return false;
        }
    }

    // ======================================================================
    //  窗口按钮
    // ======================================================================

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeRestore_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void UpdateMaximizeGlyph()
    {
        var resource = WindowState == WindowState.Maximized ? "Geo.Win.Restore" : "Geo.Win.Max";
        if (TryFindResource(resource) is Geometry geometry)
        {
            MaxGlyph.Data = geometry;
        }
    }

    // ======================================================================
    //  自绘标题栏拖拽
    // ======================================================================

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            MaximizeRestore_Click(sender, e);
            return;
        }

        if (e.ButtonState == MouseButtonState.Pressed)
        {
            try
            {
                DragMove();
            }
            catch
            {
                // 拖拽过程中窗口状态变化会抛异常，忽略
            }
        }
    }

    // ======================================================================
    //  拖拽交互（统一入口：窗口级事件 + 视觉树命中测试）
    //
    //  三个方向：
    //    ① 左侧应用条目 → 设备卡        = 新增路由（幂等）
    //    ② 设备卡里的 chip → 另一设备卡 = 复制到该设备（Duplicate）
    //    ③ 设备卡里的 chip → 左侧应用区 = 移除该路由
    //
    //  为什么统一放在窗口级：自绘拖拽需要鼠标捕获，而捕获之后
    //  事件只送往「捕获元素及其祖先」。把捕获点设在窗口上，
    //  两种拖拽源就共用同一套状态机，不必为每种来源各写一份。
    // ======================================================================

    private void Window_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(this);
        _isDragging = false;

        var origin = e.OriginalSource as DependencyObject;

        _dragApp = FindDataContext<AppSession>(origin);
        if (_dragApp is not null)
        {
            _dragSource = DragSource.App;
            _dragChip = null;
            _dragChipOwner = null;
            return;
        }

        var chip = FindDataContext<RouteChip>(origin);
        _dragChipOwner = chip is null ? null : _viewModel.FindDevice(chip);
        _dragChip = _dragChipOwner is null ? null : chip;
        _dragSource = _dragChip is null ? DragSource.None : DragSource.Chip;
    }

    private void Window_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragSource == DragSource.None) return;

        var point = e.GetPosition(this);

        if (!_isDragging)
        {
            if (Math.Abs(point.X - _dragStart.X) < DragThreshold &&
                Math.Abs(point.Y - _dragStart.Y) < DragThreshold)
            {
                return;
            }

            if (!BeginDrag()) return;
        }

        MoveDragPreview(point);
        AutoScrollDevices(point);
        UpdateDropTargets(point);
    }

    private void Window_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging) EndDrag(commit: true);

        _dragApp = null;
        _dragChip = null;
        _dragChipOwner = null;
        _dragSource = DragSource.None;
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || !_isDragging) return;

        EndDrag(commit: false);
        _dragApp = null;
        _dragChip = null;
        _dragChipOwner = null;
        _dragSource = DragSource.None;
        e.Handled = true;
    }

    private bool BeginDrag()
    {
        DragPreviewModel model;

        if (_dragSource == DragSource.App && _dragApp is not null)
        {
            // 多选拖拽：按下的条目若已在选中集合内，就拖动整个选中集合
            var selected = AppList.SelectedItems.OfType<AppSession>().ToList();
            _dragPayload = selected.Contains(_dragApp) && selected.Count > 0
                ? selected
                : new List<AppSession> { _dragApp };

            _dragApp.IsDragging = true;

            model = new DragPreviewModel
            {
                DisplayName = _dragApp.DisplayName,
                Initial = _dragApp.Initial,
                AvatarBrush = _dragApp.AvatarBrush,
                ExtraCount = Math.Max(0, _dragPayload.Count - 1),
            };
        }
        else if (_dragSource == DragSource.Chip && _dragChip is not null)
        {
            _dragChip.IsDragging = true;

            model = new DragPreviewModel
            {
                DisplayName = _dragChip.DisplayName,
                Initial = _dragChip.Initial,
                AvatarBrush = _dragChip.AvatarBrush,
            };
        }
        else
        {
            return false;
        }

        _dragPreview = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            ShowInTaskbar = false,
            Topmost = true,
            IsHitTestVisible = false,
            ResizeMode = ResizeMode.NoResize,
            SizeToContent = SizeToContent.WidthAndHeight,
            Content = new ContentControl
            {
                ContentTemplate = (DataTemplate)FindResource("DragPreviewTemplate"),
                Content = model,
            },
        };
        _dragPreview.Show();

        _isDragging = true;

        // 捕获窗口：拖拽期间持续在窗口级事件里收到 Move / Up
        Mouse.Capture(this);
        MoveDragPreview(_dragStart);

        StartupLog.Write($"drag: begin ({_dragSource}) / payload={_dragPayload.Count}");

        return true;
    }

    /// <summary>
    /// 用 SetWindowPos 直接以物理像素定位预览窗口。
    /// 不走 Window.Left/Top：那套坐标要跟 DPI 缩放打交道，在 PerMonitorV2 下很容易偏。
    /// </summary>
    private void MoveDragPreview(Point windowPoint)
    {
        if (_dragPreview is null) return;

        var handle = new WindowInteropHelper(_dragPreview).Handle;
        if (handle == IntPtr.Zero) return;

        var screen = PointToScreen(windowPoint);
        SetWindowPos(handle, IntPtr.Zero, (int)screen.X + 18, (int)screen.Y + 18,
                     0, 0, SwpNoSize | SwpNoZOrder | SwpNoActivate);
    }

    private void UpdateDropTargets(Point windowPoint)
    {
        if (_dragSource == DragSource.Chip && _dragChip is not null)
        {
            var overLeft = IsOverAppList(windowPoint);
            if (overLeft != _isOverAppList)
            {
                _isOverAppList = overLeft;
                _viewModel.IsRemoveTarget = overLeft;
            }

            var target = overLeft ? null : HitTestDevice(windowPoint);
            if (!ReferenceEquals(target, _dropTarget))
            {
                _dropTarget = target;
                _viewModel.SetDropTarget(target, new[] { _dragChip.Pid });
            }

            return;
        }

        var device = HitTestDevice(windowPoint);
        if (!ReferenceEquals(device, _dropTarget))
        {
            _dropTarget = device;
            _viewModel.SetDropTarget(device, _dragPayload.Select(s => s.Pid).ToList());
        }
    }

    private void EndDrag(bool commit)
    {
        var source = _dragSource;
        var target = _dropTarget;
        var chip = _dragChip;
        var owner = _dragChipOwner;
        var payload = _dragPayload;
        var overLeft = _isOverAppList;

        _isDragging = false;
        _dropTarget = null;
        _isOverAppList = false;
        _dragPayload = new List<AppSession>();

        if (_dragApp is not null) _dragApp.IsDragging = false;
        if (chip is not null) chip.IsDragging = false;

        _viewModel.IsRemoveTarget = false;
        _viewModel.ClearDropTarget();

        try
        {
            _dragPreview?.Close();
        }
        catch
        {
            // 预览窗口可能已被系统回收
        }

        _dragPreview = null;
        Mouse.Capture(null);

        if (!commit) return;

        // ③ chip 拖回左栏 = 移除该路由
        if (source == DragSource.Chip && chip is not null && overLeft)
        {
            _viewModel.RemoveRoute(chip);
            StartupLog.Write("drop: chip → 左栏（移除路由）");
            return;
        }

        // ② chip 拖到另一设备 = 复制到该设备
        if (source == DragSource.Chip && chip is not null)
        {
            if (target is null || ReferenceEquals(target, owner)) return;

            _viewModel.CopyRouteTo(chip, target);
            StartupLog.Write($"drop: chip → 复制到 '{target.FriendlyName}'");
            return;
        }

        // ① 应用拖到设备 = 新增路由（反馈含撤销，由 ViewModel 统一处理）
        if (source == DragSource.App && target is not null && payload.Count > 0)
        {
            var outcome = _viewModel.TryAddRoutes(payload, target);
            StartupLog.Write($"drop: {outcome} → '{target.FriendlyName}' x{payload.Count}");
        }
    }

    /// <summary>整卡命中：卡片任意位置都算该设备（比只命中拖放区更友好）。</summary>
    private AudioDevice? HitTestDevice(Point windowPoint)
    {
        var hit = VisualTreeHelper.HitTest(this, windowPoint);
        return WalkForDataContext<AudioDevice>(hit?.VisualHit);
    }

    private static T? FindDataContext<T>(DependencyObject? source) where T : class
        => WalkForDataContext<T>(source);

    private static T? WalkForDataContext<T>(DependencyObject? current) where T : class
    {
        while (current is not null)
        {
            if (current is FrameworkElement element && element.DataContext is T match) return match;

            current = current is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(current)
                : null;
        }

        return null;
    }

    /// <summary>
    /// 是否落在左侧应用列表上（chip 拖回这里 = 移除路由）。
    /// 用几何判定而不是 IsMouseOver：拖拽期间有鼠标捕获，
    /// IsMouseOver 反映的是被捕获元素，不能用来判断光标真实位置。
    /// </summary>
    private bool IsOverAppList(Point windowPoint)
    {
        if (!AppList.IsVisible) return false;

        var screen = PointToScreen(windowPoint);
        var topLeft = AppList.PointToScreen(new Point(0, 0));
        var bottomRight = AppList.PointToScreen(new Point(AppList.ActualWidth, AppList.ActualHeight));

        return screen.X >= topLeft.X && screen.X <= bottomRight.X &&
               screen.Y >= topLeft.Y && screen.Y <= bottomRight.Y;
    }

    /// <summary>拖到设备面板上下边缘时自动滚动（设计文档 5.5）。</summary>
    private void AutoScrollDevices(Point windowPoint)
    {
        if (!DeviceScroll.IsVisible || DeviceScroll.ScrollableHeight <= 0) return;

        var screen = PointToScreen(windowPoint);
        var top = DeviceScroll.PointToScreen(new Point(0, 0));
        var bottom = DeviceScroll.PointToScreen(new Point(0, DeviceScroll.ActualHeight));

        const double edge = 48;
        const double step = 18;

        if (screen.Y < top.Y + edge)
        {
            DeviceScroll.ScrollToVerticalOffset(DeviceScroll.VerticalOffset - step);
        }
        else if (screen.Y > bottom.Y - edge)
        {
            DeviceScroll.ScrollToVerticalOffset(DeviceScroll.VerticalOffset + step);
        }
    }

    // ======================================================================
    //  右键菜单
    //
    //  菜单结构在代码里构建、样式由主题的隐式 ContextMenu / MenuItem 样式提供。
    //  这么做是为了：①动态设备列表（勾选态）天然适合代码构建；
    //  ②Themes/Templates.xaml 是纯 ResourceDictionary，无法承载事件处理器。
    // ======================================================================

    private void AppList_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        var session = FindDataContext<AppSession>(e.OriginalSource as DependencyObject);
        if (session is null) return;

        // 右键先选中，符合资源管理器式交互习惯
        AppList.SelectedItem = session;

        var menu = new ContextMenu();

        var mute = new MenuItem
        {
            Header = Loc.T(session.IsMuted ? "menu.unmute" : "menu.mute"),
            IsCheckable = true,
            IsChecked = session.IsMuted,
        };
        mute.Click += (_, _) => _viewModel.ToggleAppMute(session);
        menu.Items.Add(mute);

        var duplicate = new MenuItem
        {
            Header = Loc.T("menu.duplicateToMany"),
            IsEnabled = _viewModel.MenuDevices.Count > 0,
        };
        duplicate.Click += (_, _) => ShowDuplicateDialog(session);
        menu.Items.Add(duplicate);

        // 关键：必须在构造时就带一个占位子项。
        // WPF 依据「当前有没有子项」判定 MenuItem.Role 是否为 SubmenuHeader；
        // 零子项会被当成叶子项 —— 不显示箭头、不触发悬停展开、SubmenuOpened 永不触发。
        var routeTo = new MenuItem { Header = Loc.T("menu.routeTo") };
        routeTo.Items.Add(new MenuItem { Header = "…", IsEnabled = false });
        routeTo.SubmenuOpened += (_, _) => PopulateRouteTo(routeTo, session);
        menu.Items.Add(routeTo);

        menu.Items.Add(new Separator());

        var refresh = new MenuItem { Header = Loc.T("menu.refreshSessions") };
        refresh.Click += (_, _) => _viewModel.RefreshCommand.Execute(null);
        menu.Items.Add(refresh);

        OpenMenu(menu, e);
    }

    private void PopulateRouteTo(MenuItem parent, AppSession session)
    {
        parent.Items.Clear();

        var devices = _viewModel.MenuDevices;
        StartupLog.Write($"menu: 展开「路由到」子菜单，候选设备 {devices.Count} 个");

        if (devices.Count == 0)
        {
            parent.Items.Add(new MenuItem { Header = Loc.T("menu.noDevices"), IsEnabled = false });
            return;
        }

        foreach (var device in devices)
        {
            var routed = device.HasRouteFor(session.Pid);
            var item = new MenuItem
            {
                Header = device.FriendlyName,
                IsCheckable = true,
                IsChecked = routed,
            };
            var target = device;
            var source = session;
            item.Click += (_, _) => _viewModel.ToggleRouteTo(source, target);
            parent.Items.Add(item);
        }
    }

    private void DeviceList_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        var chip = FindDataContext<RouteChip>(e.OriginalSource as DependencyObject);
        if (chip is null) return;

        var device = _viewModel.FindDevice(chip);
        if (device is null) return;

        var menu = new ContextMenu();

        var remove = new MenuItem { Header = Loc.T("menu.removeRoute") };
        if (TryFindResource("Brush.State.Danger") is Brush danger) remove.Foreground = danger;
        remove.Click += (_, _) => _viewModel.RemoveRoute(chip);
        menu.Items.Add(remove);

        var mute = new MenuItem
        {
            Header = Loc.T(chip.IsMuted ? "menu.unmuteThisDevice" : "menu.muteThisDevice"),
            IsCheckable = true,
            IsChecked = chip.IsMuted,
        };
        mute.Click += (_, _) => _viewModel.ToggleChipMute(chip, device);
        menu.Items.Add(mute);

        // 同上：占位子项让 WPF 把这一项识别成子菜单头
        var copyTo = new MenuItem { Header = Loc.T("menu.copyToOther") };
        copyTo.Items.Add(new MenuItem { Header = "…", IsEnabled = false });
        copyTo.SubmenuOpened += (_, _) => PopulateCopyTo(copyTo, chip, device);
        menu.Items.Add(copyTo);

        OpenMenu(menu, e);
    }

    private void PopulateCopyTo(MenuItem parent, RouteChip chip, AudioDevice current)
    {
        parent.Items.Clear();

        var others = _viewModel.MenuDevices.Where(d => !ReferenceEquals(d, current)).ToList();
        StartupLog.Write($"menu: 展开「复制到其他设备」子菜单，候选设备 {others.Count} 个");

        if (others.Count == 0)
        {
            parent.Items.Add(new MenuItem { Header = Loc.T("menu.noOtherDevices"), IsEnabled = false });
            return;
        }

        foreach (var device in others)
        {
            var exists = device.HasRouteFor(chip.Pid);
            var item = new MenuItem
            {
                Header = device.FriendlyName,
                IsCheckable = true,
                IsChecked = exists,
                IsEnabled = !exists,
            };
            var target = device;
            var source = chip;
            item.Click += (_, _) => _viewModel.CopyRouteTo(source, target);
            parent.Items.Add(item);
        }
    }

    private void ShowDuplicateDialog(AppSession session)
    {
        var devices = _viewModel.MenuDevices;

        // 菜单项在无可用设备时已是禁用态，这里只做防御
        if (devices.Count == 0) return;

        var dialog = new DuplicateTargetsWindow(session, devices) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            _viewModel.DuplicateToDevices(session, dialog.SelectedDevices);
        }
    }

    private static void OpenMenu(ContextMenu menu, MouseButtonEventArgs e)
    {
        menu.Placement = PlacementMode.MousePoint;
        menu.PlacementTarget = e.OriginalSource as UIElement;
        menu.IsOpen = true;
        e.Handled = true;
    }

    // ======================================================================
    //  语言
    // ======================================================================

    private void Language_Click(object sender, RoutedEventArgs e)
    {
        var localization = LocalizationService.Instance;
        var menu = new ContextMenu();

        foreach (var pack in localization.AvailableLanguages)
        {
            var item = new MenuItem
            {
                Header = pack.Name,
                IsCheckable = true,
                IsChecked = string.Equals(pack.Code, localization.Current?.Code,
                                          StringComparison.OrdinalIgnoreCase),
            };

            var code = pack.Code;
            item.Click += (_, _) =>
            {
                if (LocalizationService.Instance.ChangeLanguage(code))
                {
                    _viewModel.Notify(Loc.F("language.switched", code));
                }
            };

            menu.Items.Add(item);
        }

        menu.Items.Add(new Separator());

        var import = new MenuItem { Header = Loc.T("language.import") };
        import.Click += (_, _) => ImportLanguageFile();
        menu.Items.Add(import);

        var openFolder = new MenuItem { Header = Loc.T("language.openFolder") };
        openFolder.Click += (_, _) => OpenLanguageFolder();
        menu.Items.Add(openFolder);

        menu.Placement = PlacementMode.Bottom;
        menu.PlacementTarget = sender as UIElement;
        menu.IsOpen = true;
    }

    private void ImportLanguageFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = Loc.T("language.import"),
            Filter = "Language pack (*.json)|*.json|All files (*.*)|*.*",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) != true) return;

        if (LocalizationService.Instance.Import(dialog.FileName, out var code, out var error))
        {
            LocalizationService.Instance.ChangeLanguage(code);
            _viewModel.Notify(Loc.F("language.import.ok", code));
        }
        else
        {
            _viewModel.Notify(Loc.F("language.import.failed", error ?? string.Empty), isError: true);
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
    //  无边框窗口最大化不能盖住任务栏
    // ======================================================================

    private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmGetMinMaxInfo)
        {
            AdjustMaximizedBounds(hwnd, lParam);
            handled = true;
        }

        return IntPtr.Zero;
    }

    private static void AdjustMaximizedBounds(IntPtr hwnd, IntPtr lParam)
    {
        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero) return;

        var monitorInfo = new MonitorInfo { cbSize = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref monitorInfo)) return;

        var info = Marshal.PtrToStructure<MinMaxInfo>(lParam);
        var work = monitorInfo.rcWork;
        var full = monitorInfo.rcMonitor;

        info.ptMaxPosition.X = work.Left - full.Left;
        info.ptMaxPosition.Y = work.Top - full.Top;
        info.ptMaxSize.X = work.Right - work.Left;
        info.ptMaxSize.Y = work.Bottom - work.Top;

        info.ptMinTrackSize.X = (int)Math.Max(info.ptMinTrackSize.X, 960);
        info.ptMinTrackSize.Y = (int)Math.Max(info.ptMinTrackSize.Y, 620);

        Marshal.StructureToPtr(info, lParam, true);
    }

    private const int MonitorDefaultToNearest = 0x00000002;

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
                                            int x, int y, int cx, int cy, uint flags);

    // 注意：这里不能叫 Point —— 会把 System.Windows.Point 遮蔽掉，
    // 导致拖拽代码里的坐标全部解析到这个互操作结构体上。
    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public NativePoint ptReserved;
        public NativePoint ptMaxSize;
        public NativePoint ptMaxPosition;
        public NativePoint ptMinTrackSize;
        public NativePoint ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfo
    {
        public int cbSize;
        public Rect rcMonitor;
        public Rect rcWork;
        public int dwFlags;
    }
}
