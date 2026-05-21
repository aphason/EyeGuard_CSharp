using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using EyeGuard.Services;

namespace EyeGuard;

public partial class MainWindow : Window
{
    private readonly TimerService _timerService;
    private readonly AudioService _audioService;
    private ContextMenu? _contextMenu;
    private MenuItem? _topmostItem;
    private MenuItem? _cancelTopmostItem;

    public Action? OpenSettingsAction { get; set; }

    public MainWindow(TimerService timerService, AudioService audioService)
    {
        InitializeComponent();
        _timerService = timerService;
        _audioService = audioService;

        _timerService.WorkTick += OnWorkTick;
        _timerService.ThreeMinuteWarning += OnThreeMinuteWarning;
        _timerService.StateChanged += OnStateChanged;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Position at top-right corner of the screen where the cursor is
        var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(
            (int)System.Windows.Forms.Cursor.Position.X,
            (int)System.Windows.Forms.Cursor.Position.Y));

        if (screen != null)
        {
            // Convert Screen.WorkingArea (physical pixels) to WPF device-independent pixels
            using var g = System.Drawing.Graphics.FromHwnd(IntPtr.Zero);
            double scaleX = g.DpiX / 96.0;
            double scaleY = g.DpiY / 96.0;

            Left = (screen.WorkingArea.Right - 200) / scaleX;
            Top = (screen.WorkingArea.Top + 10) / scaleY;
        }

        // Build context menu
        _contextMenu = new ContextMenu();
        AddMenuItem("立即休息", "rest");
        _contextMenu.Items.Add(new Separator());
        AddMenuItem("推迟休息3分钟", "postpone3");
        AddMenuItem("推迟休息5分钟", "postpone5");
        AddMenuItem("推迟休息10分钟", "postpone10");
        _contextMenu.Items.Add(new Separator());
        _topmostItem = AddMenuItem("总在最前显示", "topmost");
        _cancelTopmostItem = AddMenuItem("取消最前显示", "canceltopmost");
        _contextMenu.Items.Add(new Separator());
        AddMenuItem("设置属性", "settings");
        AddMenuItem("关闭退出", "exit");

        // Initialize topmost checkmark state (default is Topmost=true from XAML)
        if (Topmost)
        {
            _topmostItem.Header = "✓ 总在最前显示";
        }
        else
        {
            _cancelTopmostItem.Header = "✓ 取消最前显示";
        }

        // Right-click to open menu
        PreviewMouseRightButtonUp += (s, args) =>
        {
            UpdatePostponeMenus();
            _contextMenu.IsOpen = true;
        };
    }

    private MenuItem AddMenuItem(string header, string tag)
    {
        var item = new MenuItem { Header = header, Tag = tag };
        item.Click += ContextMenuItem_Click;
        _contextMenu!.Items.Add(item);
        return item;
    }

    private bool ConfirmAction(string message)
    {
        return System.Windows.MessageBox.Show(message, "爱眼卫士",
            MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    private void UpdateCountdownImmediate()
    {
        var remaining = _timerService.RemainingSeconds;
        var total = _timerService.TotalSeconds;
        var minutes = remaining / 60;
        var seconds = remaining % 60;
        CountdownText.Text = $"{minutes:D2}:{seconds:D2}";
        double progress = total > 0 ? (double)remaining / total : 0;
        ProgressBar.Width = progress * 136;
    }

    private void ContextMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi)
        {
            switch (mi.Tag)
            {
                case "rest":
                    if (ConfirmAction("立即休息？"))
                    {
                        _timerService.SkipToRest();
                    }
                    break;
                case "postpone3":
                    if (ConfirmAction("推迟休息3分钟？"))
                    {
                        _timerService.Postpone(3);
                        UpdateCountdownImmediate();
                    }
                    break;
                case "postpone5":
                    if (ConfirmAction("推迟休息5分钟？"))
                    {
                        _timerService.Postpone(5);
                        UpdateCountdownImmediate();
                    }
                    break;
                case "postpone10":
                    if (ConfirmAction("推迟休息10分钟？"))
                    {
                        _timerService.Postpone(10);
                        UpdateCountdownImmediate();
                    }
                    break;
                case "topmost":
                    Topmost = true;
                    _topmostItem!.Header = "✓ 总在最前显示";
                    _cancelTopmostItem!.Header = "取消最前显示";
                    break;
                case "canceltopmost":
                    Topmost = false;
                    _topmostItem!.Header = "总在最前显示";
                    _cancelTopmostItem!.Header = "✓ 取消最前显示";
                    break;
                case "settings":
                    OpenSettingsAction?.Invoke();
                    break;
                case "exit":
                    if (ConfirmAction("确认退出爱眼卫士？"))
                    {
                        System.Windows.Application.Current.Shutdown();
                    }
                    break;
            }
        }
    }

    private void UpdatePostponeMenus()
    {
        var canPostpone = _timerService.CanPostpone;
        if (_contextMenu == null) return;
        foreach (var item in _contextMenu.Items)
        {
            if (item is MenuItem mi && mi.Tag is string tag && tag.StartsWith("postpone"))
            {
                mi.IsEnabled = canPostpone;
            }
        }
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void OnWorkTick(int remaining, int total)
    {
        Dispatcher.Invoke(() =>
        {
            var minutes = remaining / 60;
            var seconds = remaining % 60;
            CountdownText.Text = $"{minutes:D2}:{seconds:D2}";

            double progress = total > 0 ? (double)remaining / total : 0;
            ProgressBar.Width = progress * 156;
        });
    }

    private void OnThreeMinuteWarning()
    {
        Dispatcher.Invoke(() =>
        {
            _audioService.PlayBreakPre();
            System.Windows.MessageBox.Show(
                "3分钟后即将休息，请保存当前工作。",
                "爱眼卫士提示",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        });
    }

    private void OnStateChanged()
    {
        Dispatcher.Invoke(() =>
        {
            if (_timerService.State == AppState.Working)
            {
                Show();
            }
            else if (_timerService.State == AppState.Resting)
            {
                Hide();
            }
        });
    }
}
