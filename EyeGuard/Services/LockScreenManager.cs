using EyeGuard.Models;

namespace EyeGuard.Services;

public class LockScreenManager
{
    private readonly TimerService _timerService;
    private readonly KeyboardHook _keyboardHook;
    private readonly List<LockScreenWindow> _lockWindows = new();

    public Action? OnUnlocked { get; set; }

    public LockScreenManager(TimerService timerService, KeyboardHook keyboardHook)
    {
        _timerService = timerService;
        _keyboardHook = keyboardHook;
    }

    public void ShowLockScreens(ForceMode forceMode)
    {
        HideLockScreens();

        // Enable keyboard hook to block Win/Alt+Tab/Alt+F4
        _keyboardHook.Enable();

        var screens = System.Windows.Forms.Screen.AllScreens;
        foreach (var screen in screens)
        {
            var bounds = screen.Bounds;
            var lockWin = new LockScreenWindow(
                _timerService, forceMode, OnUnlocked,
                bounds.Left, bounds.Top,
                bounds.Width, bounds.Height);
            lockWin.Show();
            _lockWindows.Add(lockWin);
        }
    }

    public void HideLockScreens()
    {
        foreach (var win in _lockWindows)
        {
            win.Close();
        }
        _lockWindows.Clear();

        // Disable keyboard hook when lock screens are gone
        _keyboardHook.Disable();
    }
}
