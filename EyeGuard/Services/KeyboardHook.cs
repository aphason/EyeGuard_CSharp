using System.Runtime.InteropServices;
using System.Diagnostics;

namespace EyeGuard.Services;

public class KeyboardHook : IDisposable
{
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;

    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;
    private const int VK_TAB = 0x09;
    private const int VK_F4 = 0x73;
    private const int VK_MENU = 0x12;  // ALT

    private IntPtr _hookId = IntPtr.Zero;
    private LowLevelKeyboardProc? _proc;
    private bool _enabled;

    public void Enable()
    {
        _enabled = true;
        if (_hookId == IntPtr.Zero)
        {
            _proc = HookCallback;
            using var curProcess = Process.GetCurrentProcess();
            using var mainModule = curProcess.MainModule!;
            _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc,
                GetModuleHandle(mainModule.ModuleName), 0);
        }
    }

    public void Disable()
    {
        _enabled = false;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (!_enabled || nCode < 0)
            return CallNextHookEx(_hookId, nCode, wParam, lParam);

        int vkCode = Marshal.ReadInt32(lParam);
        bool isKeyDown = wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN;

        if (!isKeyDown)
            return CallNextHookEx(_hookId, nCode, wParam, lParam);

        // Block Win key
        if (vkCode == VK_LWIN || vkCode == VK_RWIN)
            return (IntPtr)1;

        // Block Alt+F4
        if (vkCode == VK_F4 && (GetAsyncKeyState(VK_MENU) & 0x8000) != 0)
            return (IntPtr)1;

        // Block Alt+Tab: when Alt is held and Tab is pressed
        if (vkCode == VK_TAB && (GetAsyncKeyState(VK_MENU) & 0x8000) != 0)
            return (IntPtr)1;

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    public void Dispose()
    {
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
        _enabled = false;
        GC.SuppressFinalize(this);
    }
}
