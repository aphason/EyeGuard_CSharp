using System.IO;

namespace EyeGuard.Services;

public static class Logger
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "EyeGuard", "debug.log");

    private static readonly object _lock = new();

    /// <summary>是否启用日志记录，由设置控制。</summary>
    public static bool Enabled { get; set; }

    public static void Write(string message)
    {
        if (!Enabled) return;
        lock (_lock)
        {
            try
            {
                var dir = Path.GetDirectoryName(LogPath)!;
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.AppendAllText(LogPath,
                    $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
            }
            catch { }
        }
    }
}
