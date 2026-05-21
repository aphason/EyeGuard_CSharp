using System.Windows;
using System.Windows.Media;
using EyeGuard.Models;
using EyeGuard.Services;

namespace EyeGuard;

public partial class LockScreenWindow : Window
{
    private readonly TimerService _timerService;
    private readonly Action? _onUnlocked;
    private readonly int _targetLeft;
    private readonly int _targetTop;
    private readonly int _targetWidth;
    private readonly int _targetHeight;
    private bool _canUnlock;
    private System.Timers.Timer? _oneMinTimer;
    private static readonly SolidColorBrush _dotFilled = new(System.Windows.Media.Color.FromRgb(0x58, 0xa6, 0xff));
    private static readonly SolidColorBrush _dotEmpty = new(System.Windows.Media.Color.FromRgb(0x33, 0x33, 0x33));
    private static readonly int _totalDots = 10;

    public LockScreenWindow(TimerService timerService, ForceMode forceMode, Action? onUnlocked,
                            int screenLeft, int screenTop, int screenWidth, int screenHeight)
    {
        InitializeComponent();
        _timerService = timerService;
        _onUnlocked = onUnlocked;
        _targetLeft = screenLeft;
        _targetTop = screenTop;
        _targetWidth = screenWidth;
        _targetHeight = screenHeight;

        // Position will be set in OnSourceInitialized via Win32 for correct DPI handling

        this.SourceInitialized += OnSourceInitialized;

        switch (forceMode)
        {
            case ForceMode.None:
                _canUnlock = true;
                ShowUnlockButton();
                break;
            case ForceMode.Semi:
                _canUnlock = false;
                // Show unlock after 1 minute
                _oneMinTimer = new System.Timers.Timer(60000) { AutoReset = false };
                _oneMinTimer.Elapsed += (s, e) =>
                {
                    _oneMinTimer.Stop();
                    _oneMinTimer.Dispose();
                    Dispatcher.Invoke(() =>
                    {
                        _canUnlock = true;
                        ShowUnlockButton();
                    });
                };
                _oneMinTimer.Start();
                break;
            case ForceMode.Full:
                _canUnlock = false;
                // Never shows unlock button
                break;
        }

        _timerService.RestTick += OnRestTick;
        this.Closed += (s, e) =>
        {
            _timerService.RestTick -= OnRestTick;
            _oneMinTimer?.Dispose();
            this.SourceInitialized -= OnSourceInitialized;
        };
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;

        // Use Win32 SetWindowPos with physical pixel coordinates to position on target monitor
        var mi = new NativeMethods.MONITORINFO();
        mi.cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFO>();
        var hMonitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
        NativeMethods.GetMonitorInfo(hMonitor, ref mi);

        // Move window to exact physical pixel bounds of target monitor
        NativeMethods.SetWindowPos(hwnd, IntPtr.Zero,
            _targetLeft, _targetTop, _targetWidth, _targetHeight,
            0x0010); // SWP_NOZORDER

        // Maximize to ensure full coverage
        this.WindowState = WindowState.Maximized;
    }

    private void ShowUnlockButton()
    {
        UnlockButton.Visibility = Visibility.Visible;
    }

    private void OnRestTick(int remaining, int total)
    {
        Dispatcher.Invoke(() =>
        {
            var minutes = remaining / 60;
            var seconds = remaining % 60;
            CountdownText.Text = $"{minutes:D2}:{seconds:D2}";

            int filledDots = total > 0 ? (int)((double)remaining / total * _totalDots) : 0;
            var dots = new System.Collections.Generic.List<SolidColorBrush>(_totalDots);
            for (int i = 0; i < _totalDots; i++)
                dots.Add(i < filledDots ? _dotFilled : _dotEmpty);
            ProgressDots.ItemsSource = dots;
        });
    }

    private void UnlockButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_canUnlock) return;
        // Tell TimerService to complete the rest cycle
        _timerService.Stop();
        // Notify App.cs that unlock happened
        _onUnlocked?.Invoke();
    }
}

internal static class NativeMethods
{
    public const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    public struct MONITORINFO
    {
        public int cbSize;
        public NativeMethods.RECT rcMonitor;
        public NativeMethods.RECT rcWork;
        public uint dwFlags;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
