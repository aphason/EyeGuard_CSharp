using System.Runtime.InteropServices;
using EyeGuard.Models;

namespace EyeGuard.Services;

public class IdleDetector : IDisposable
{
    private readonly TimerService _timerService;
    private readonly SettingsService _settingsService;
    private System.Timers.Timer? _timer;
    private bool _isIdlePaused;
    private bool _isFirstTick = true;

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    public IdleDetector(TimerService timerService, SettingsService settingsService)
    {
        _timerService = timerService;
        _settingsService = settingsService;
    }

    public void Start()
    {
        _timer = new System.Timers.Timer(5000);
        _timer.Elapsed += OnTick;
        _timer.AutoReset = true;
        _timer.Start();
        Logger.Write("IdleDetector started (first tick in 5s, then 1s interval)");
    }

    private void OnTick(object? sender, System.Timers.ElapsedEventArgs e)
    {
        if (_isFirstTick)
        {
            _isFirstTick = false;
            if (_timer != null)
                _timer.Interval = 1000;
        }
        CheckIdle();
    }

    private void CheckIdle()
    {
        var settings = _settingsService.Current;
        if (!settings.PauseOnIdle)
        {
            if (_isIdlePaused)
            {
                _isIdlePaused = false;
                Logger.Write("IdleDetector: PauseOnIdle disabled, clearing idle flag");
            }
            return;
        }

        if (_timerService.State != AppState.Working)
        {
            if (_isIdlePaused)
            {
                _isIdlePaused = false;
                Logger.Write("IdleDetector: state not Working, clearing idle flag");
            }
            return;
        }

        var lastInput = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (!GetLastInputInfo(ref lastInput))
            return;

        uint idleMs = (uint)Environment.TickCount - lastInput.dwTime;
        int idleSeconds = (int)(idleMs / 1000);

        if (!_isIdlePaused && idleSeconds >= settings.PauseOnIdleMinutes * 60)
        {
            _isIdlePaused = true;
            _timerService.Pause();
            Logger.Write($"IdleDetector: idle {idleSeconds}s >= {settings.PauseOnIdleMinutes}min -> PAUSE");
        }
        else if (_isIdlePaused && idleSeconds < 2)
        {
            _isIdlePaused = false;
            _timerService.StartWorkCycle();
            Logger.Write("IdleDetector: input detected -> new work cycle");
        }
    }

    public void Stop()
    {
        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;
        _isIdlePaused = false;
        Logger.Write("IdleDetector stopped");
    }

    public void Dispose()
    {
        Stop();
    }
}
