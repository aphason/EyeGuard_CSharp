// EyeGuard/Services/AudioService.cs
using System.Runtime.InteropServices;
using System.IO;

namespace EyeGuard.Services;

public class AudioService : IDisposable
{
    [DllImport("winmm.dll")]
    private static extern int mciSendString(string command, System.Text.StringBuilder? returnString, int returnLength, IntPtr callback);

    private const string Alias = "EyeGuardAudio";

    public void PlayBreakPre()
    {
        Play(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources", "sounds", "breakpre.mid"));
    }

    public void PlayBreak()
    {
        Play(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources", "sounds", "break.mid"));
    }

    public void PlayUnlock()
    {
        Play(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources", "sounds", "unlock.mid"));
    }

    private void Play(string filePath)
    {
        if (!File.Exists(filePath)) return;
        Stop();
        mciSendString($"open \"{filePath}\" type sequencer alias {Alias}", null, 0, IntPtr.Zero);
        mciSendString($"play {Alias}", null, 0, IntPtr.Zero);
    }

    public void Stop()
    {
        mciSendString($"close {Alias}", null, 0, IntPtr.Zero);
    }

    public void Dispose()
    {
        Stop();
    }
}
