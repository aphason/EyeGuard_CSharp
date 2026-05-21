using EyeGuard.Models;

namespace EyeGuard.Services;

public enum AppState
{
    Working,
    Resting,
    Paused
}

public class TimerService
{
    private readonly SettingsService _settings;
    private AppState _state = AppState.Working;
    private int _remainingSeconds;
    private int _totalSeconds;
    private int _postponeUsed;
    private int _maxPostponeCount;
    private bool _paused;
    private int _pausedRemainingSeconds;
    private System.Timers.Timer? _timer;
    private bool _threeMinuteFired;

    // Events
    public event Action<int, int>? WorkTick;          // remainingSeconds, totalSeconds
    public event Action<int, int>? RestTick;          // remainingSeconds, totalSeconds
    public event Action? WorkCompleted;
    public event Action? RestCompleted;
    public event Action? ThreeMinuteWarning;
    public event Action? StateChanged;

    // Properties
    public AppState State => _state;
    public int RemainingSeconds => _remainingSeconds;
    public int TotalSeconds => _totalSeconds;
    public int PostponeUsed => _postponeUsed;
    public bool CanPostpone => _postponeUsed < _maxPostponeCount;

    // Constructor: takes SettingsService, subscribes to SettingsChanged
    public TimerService(SettingsService settings)
    {
        _settings = settings;
        ApplySettings(settings.Current);
        _settings.SettingsChanged += ApplySettings;
    }

    private void ApplySettings(AppSettings s)
    {
        _maxPostponeCount = s.MaxPostponeCount;
    }

    // StartWorkCycle: set state to Working, set remaining to work duration from settings, reset postpone, start timer
    public void StartWorkCycle()
    {
        _state = AppState.Working;
        _remainingSeconds = _settings.Current.WorkDurationMinutes * 60;
        _totalSeconds = _remainingSeconds;
        _postponeUsed = 0;
        _paused = false;
        _threeMinuteFired = false;
        StartTimer();
        StateChanged?.Invoke();
        Logger.Write($"Timer: StartWorkCycle — {_totalSeconds}s total, paused={_paused}");
    }

    // StartRestCycle: set state to Resting, set remaining to rest duration, start timer
    public void StartRestCycle()
    {
        _state = AppState.Resting;
        _remainingSeconds = _settings.Current.RestDurationMinutes * 60;
        _totalSeconds = _remainingSeconds;
        _paused = false;
        StartTimer();
        StateChanged?.Invoke();
        Logger.Write($"Timer: StartRestCycle — {_totalSeconds}s total");
    }

    // Postpone: add minutes to remaining time, increment counter, only if CanPostpone and Working
    public void Postpone(int minutes)
    {
        if (!CanPostpone || _state != AppState.Working) return;
        _postponeUsed++;
        _remainingSeconds += minutes * 60;
        _totalSeconds = _remainingSeconds;
        _threeMinuteFired = false;
    }

    // Pause: save remaining, stop timer, only if Working and not already paused
    public void Pause()
    {
        if (_state != AppState.Working)
        {
            Logger.Write($"Timer: Pause ignored — state={_state}");
            return;
        }
        if (_paused)
        {
            Logger.Write("Timer: Pause ignored — already paused");
            return;
        }
        _paused = true;
        _pausedRemainingSeconds = _remainingSeconds;
        StopTimer();
        Logger.Write($"Timer: PAUSE — remaining={_pausedRemainingSeconds}s");
    }

    // Resume: restore remaining, restart timer, only if Working and paused
    public void Resume()
    {
        if (_state != AppState.Working)
        {
            Logger.Write($"Timer: Resume ignored — state={_state}");
            return;
        }
        if (!_paused)
        {
            Logger.Write($"Timer: Resume ignored — not paused (state={_state})");
            return;
        }
        _paused = false;
        _remainingSeconds = _pausedRemainingSeconds;
        StartTimer();
        Logger.Write($"Timer: RESUME — remaining={_pausedRemainingSeconds}s");
    }

    // SkipToRest: skip work cycle immediately, fire WorkCompleted
    public void SkipToRest()
    {
        if (_state != AppState.Working) return;
        StopTimer();
        WorkCompleted?.Invoke();
    }

    // StartTimer: creates 1-second timer, elapses every 1s
    private void StartTimer()
    {
        StopTimer();
        _timer = new System.Timers.Timer(1000);
        _timer.Elapsed += OnTick;
        _timer.AutoReset = true;
        _timer.Start();
    }

    private void StopTimer()
    {
        if (_timer != null)
        {
            _timer.Stop();
            _timer.Elapsed -= OnTick;
            _timer.Dispose();
            _timer = null;
        }
    }

    // OnTick: decrement remaining, fire appropriate events based on state
    private void OnTick(object? sender, System.Timers.ElapsedEventArgs e)
    {
        _remainingSeconds--;

        if (_state == AppState.Working)
        {
            if (_remainingSeconds <= 180 && !_threeMinuteFired)
            {
                _threeMinuteFired = true;
                ThreeMinuteWarning?.Invoke();
            }

            WorkTick?.Invoke(_remainingSeconds, _totalSeconds);

            if (_remainingSeconds <= 0)
            {
                StopTimer();
                WorkCompleted?.Invoke();
            }
        }
        else if (_state == AppState.Resting)
        {
            RestTick?.Invoke(_remainingSeconds, _totalSeconds);

            if (_remainingSeconds <= 0)
            {
                StopTimer();
                RestCompleted?.Invoke();
            }
        }
    }

    // Stop: stop timer, cleanup
    public void Stop()
    {
        StopTimer();
    }
}
