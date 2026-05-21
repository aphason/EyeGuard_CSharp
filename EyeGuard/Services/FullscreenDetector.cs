using System.Runtime.InteropServices;
using System.Text;

namespace EyeGuard.Services;

public class FullscreenDetector : IDisposable
{
    private readonly TimerService _timerService;
    private readonly SettingsService _settingsService;
    private System.Timers.Timer? _timer;
    private bool _wasFullscreen;
    private bool _firstTick = true;
    private readonly StringBuilder _classNameBuffer = new(256);
    private readonly StringBuilder _titleBuffer = new(256);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WINDOWPLACEMENT
    {
        public int length;
        public int flags;
        public int showCmd;
        public System.Drawing.Point ptMinPosition;
        public System.Drawing.Point ptMaxPosition;
        public RECT rcNormalPosition;
    }

    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;
    private const uint WS_VISIBLE = 0x10000000;
    private const uint WS_EX_TOOLWINDOW = 0x00000080;
    private const int SW_SHOWMAXIMIZED = 3;

    private static readonly string[] SystemWindowClasses = {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd",
        "Windows.UI.Core.CoreWindow",     // UWP system compositor (touch keyboard, input panel, etc.)
        "ApplicationFrameWindow",         // UWP app frame container
        "Windows.UI.Composition.DesktopWindowContentBridge" // UWP composition bridge
    };

    public FullscreenDetector(TimerService timerService, SettingsService settingsService)
    {
        _timerService = timerService;
        _settingsService = settingsService;
    }

    public void Start()
    {
        // First tick after 5s to let app fully initialize; then every 1s
        _timer = new System.Timers.Timer(5000);
        _timer.Elapsed += OnTimerTick;
        _timer.AutoReset = true;
        _timer.Start();
        Logger.Write("FullscreenDetector started (first tick in 5s, then 1s interval)");
    }

    public void StartWithNoDelay()
    {
        // For testing: immediately start checking
        _timer = new System.Timers.Timer(1000);
        _timer.Elapsed += OnTimerTick;
        _timer.AutoReset = true;
        _timer.Start();
        Logger.Write("FullscreenDetector started (immediate, 1s interval)");
    }

    private void OnTimerTick(object? sender, System.Timers.ElapsedEventArgs e)
    {
        if (_firstTick)
        {
            _firstTick = false;
            Logger.Write("FullscreenDetector: first tick fired");
            // After initial 5s delay, switch to 1s interval
            if (_timer != null)
                _timer.Interval = 1000;
        }
        CheckFullscreen();
    }

    private void CheckFullscreen()
    {
        if (!_settingsService.Current.PauseOnFullscreen)
        {
            if (_wasFullscreen)
            {
                _wasFullscreen = false;
                _timerService.Resume();
                Logger.Write("Detector: PauseOnFullscreen disabled, resuming");
            }
            return;
        }

        bool foundFullscreen = false;
        uint ourProcessId = (uint)Environment.ProcessId;

        EnumWindows((hWnd, lParam) =>
        {
            if (!IsWindowVisible(hWnd))
                return true;

            // Skip our own application windows
            GetWindowThreadProcessId(hWnd, out uint processId);
            if (processId == ourProcessId)
                return true;

            // Get window info
            _classNameBuffer.Clear();
            GetClassName(hWnd, _classNameBuffer, 256);
            string cls = _classNameBuffer.ToString();

            if (Array.IndexOf(SystemWindowClasses, cls) >= 0)
                return true;

            int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
            if ((exStyle & WS_EX_TOOLWINDOW) != 0)
                return true;

            int style = GetWindowLong(hWnd, GWL_STYLE);
            if ((style & WS_VISIBLE) == 0)
                return true;

            if (!GetWindowRect(hWnd, out var rect))
                return true;

            int w = rect.Right - rect.Left;
            int h = rect.Bottom - rect.Top;

            // Skip tiny or off-screen windows
            if (w < 800 || h < 600)
                return true;

            var hMonitor = MonitorFromWindow(hWnd, MONITOR_DEFAULTTONEAREST);
            var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (!GetMonitorInfo(hMonitor, ref mi))
                return true;

            int sw = mi.rcMonitor.Right - mi.rcMonitor.Left;
            int sh = mi.rcMonitor.Bottom - mi.rcMonitor.Top;

            // === Fullscreen detection using geometry ===
            // Condition 1: positioned at monitor origin
            bool atOrigin = rect.Left == mi.rcMonitor.Left && rect.Top == mi.rcMonitor.Top;
            // Condition 2: covers full monitor (2px tolerance)
            bool coversMonitor = Math.Abs(w - sw) <= 2 && Math.Abs(h - sh) <= 2;

            if (atOrigin && coversMonitor)
            {
                // Get window title for logging
                int len = GetWindowTextLength(hWnd);
                _titleBuffer.Clear();
                _titleBuffer.Capacity = Math.Max(len + 1, 256);
                if (len > 0) GetWindowText(hWnd, _titleBuffer, _titleBuffer.Capacity);

                Logger.Write($"FULLSCREEN DETECTED: class=[{cls}] title=[{_titleBuffer}] " +
                    $"rect=({rect.Left},{rect.Top}-{rect.Right},{rect.Bottom}) " +
                    $"mon=({mi.rcMonitor.Left},{mi.rcMonitor.Top}-{mi.rcMonitor.Right},{mi.rcMonitor.Bottom})");

                foundFullscreen = true;
                return false;
            }

            return true;
        }, IntPtr.Zero);

        if (foundFullscreen && !_wasFullscreen)
        {
            _wasFullscreen = true;
            _timerService.Pause();
            Logger.Write("Detector: fullscreen detected → PAUSE");
        }
        else if (!foundFullscreen && _wasFullscreen)
        {
            _wasFullscreen = false;
            _timerService.Resume();
            Logger.Write("Detector: no fullscreen → RESUME");
        }
    }

    public void Reset()
    {
        _wasFullscreen = false;
        Logger.Write("Detector: Reset() called");
    }

    public void Stop()
    {
        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;
        _wasFullscreen = false;
        Logger.Write("FullscreenDetector stopped");
    }

    public void Dispose()
    {
        Stop();
    }
}
