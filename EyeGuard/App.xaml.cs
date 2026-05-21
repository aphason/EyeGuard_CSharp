using System.Windows;
using System.Threading;
using System.Runtime.InteropServices;
using EyeGuard.Services;

namespace EyeGuard;

public partial class App : System.Windows.Application
{
    private SettingsService? _settingsService;
    private AudioService? _audioService;
    private TimerService? _timerService;
    private FullscreenDetector? _fullscreenDetector;
    private KeyboardHook? _keyboardHook;
    private LockScreenManager? _lockScreenManager;
    private IdleDetector? _idleDetector;
    private MainWindow? _mainWindow;
    private System.Windows.Forms.NotifyIcon? _notifyIcon;
    private static Mutex? _mutex;
    private bool _isSettingsOpen;
    private const uint WM_CLOSE = 0x0010;

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    private static void CloseThreeMinuteDialog()
    {
        var hwnd = FindWindow(null, "爱眼卫士提示");
        if (hwnd != IntPtr.Zero)
        {
            SendMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mutex = new Mutex(true, "EyeGuard_SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            System.Windows.MessageBox.Show("爱眼卫士已在运行中。", "爱眼卫士",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Environment.Exit(0);
            return;
        }

        // Initialize services
        _settingsService = new SettingsService();
        _audioService = new AudioService();
        _timerService = new TimerService(_settingsService);
        _keyboardHook = new KeyboardHook();
        _lockScreenManager = new LockScreenManager(_timerService, _keyboardHook);

        // Wire up transitions
        _timerService.WorkCompleted += OnWorkCompleted;
        _timerService.RestCompleted += OnRestCompleted;

        // Lock screen unlock handler
        _lockScreenManager.OnUnlocked = OnRestCompleted;

        // Start fullscreen detection
        _fullscreenDetector = new FullscreenDetector(_timerService, _settingsService!);
        _fullscreenDetector.Start();

        // Start idle detection
        _idleDetector = new IdleDetector(_timerService, _settingsService!);
        _idleDetector.Start();

        // System tray icon
        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Text = "爱眼卫士",
            Visible = true
        };

        // Try to load the app icon, fall back to default if assembly icon isn't available
        try
        {
            _notifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(
                System.Reflection.Assembly.GetExecutingAssembly().Location);
        }
        catch
        {
            // Use a default icon if extraction fails
        }

        _notifyIcon.MouseClick += (s, args) =>
        {
            if (args.Button == System.Windows.Forms.MouseButtons.Left)
            {
                OpenSettings();
            }
        };

        var trayMenu = new System.Windows.Forms.ContextMenuStrip();
        trayMenu.Items.Add("设置属性", null, (s, args) => { if (!_isSettingsOpen) OpenSettings(); });
        trayMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        trayMenu.Items.Add("关闭退出", null, (s, args) =>
        {
            var result = System.Windows.MessageBox.Show("确认退出爱眼卫士？", "爱眼卫士",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes) Shutdown();
        });
        _notifyIcon.ContextMenuStrip = trayMenu;

        // Main window
        _mainWindow = new MainWindow(_timerService, _audioService);
        _mainWindow.OpenSettingsAction = OpenSettings;
        _mainWindow.Show();

        // Start first work cycle
        _timerService.StartWorkCycle();
    }

    private void OnWorkCompleted()
    {
        Dispatcher.Invoke(() =>
        {
            _audioService?.PlayBreak();
            _lockScreenManager?.ShowLockScreens(_settingsService!.Current.ForceMode);
            _timerService?.StartRestCycle();
        });
    }

    private void OnRestCompleted()
    {
        Dispatcher.Invoke(() =>
        {
            _lockScreenManager?.HideLockScreens();
            CloseThreeMinuteDialog();
            _fullscreenDetector?.Reset();
            _audioService?.PlayUnlock();
            _timerService?.StartWorkCycle();
        });
    }

    private void OpenSettings()
    {
        if (_isSettingsOpen) return;
        _isSettingsOpen = true;
        Dispatcher.Invoke(() =>
        {
            try
            {
                var settingsWin = new SettingsWindow(_settingsService!);
                settingsWin.Owner = _mainWindow;
                settingsWin.ShowDialog();

                // If settings were saved (DialogResult == true) and currently working, restart work cycle
                if (settingsWin.DialogResult == true && _timerService?.State == AppState.Working)
                {
                    _timerService.StartWorkCycle();
                }
            }
            finally
            {
                _isSettingsOpen = false;
            }
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mutex?.Dispose();
        _keyboardHook?.Dispose();
        _fullscreenDetector?.Dispose();
        _idleDetector?.Dispose();
        _audioService?.Dispose();
        _notifyIcon?.Dispose();
        _timerService?.Stop();
        base.OnExit(e);
    }
}
