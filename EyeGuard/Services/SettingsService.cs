using System.IO;
using System.Text.Json;
using EyeGuard.Models;

namespace EyeGuard.Services;

public class SettingsService
{
    private readonly string _filePath;
    private AppSettings _current;

    public SettingsService()
    {
        _filePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EyeGuard", "settings.json");
        _current = Load();
        Logger.Enabled = _current.EnableLogging;
    }

    public AppSettings Current => _current;

    public event Action<AppSettings>? SettingsChanged;

    public void Save(AppSettings settings)
    {
        _current = settings;
        var dir = Path.GetDirectoryName(_filePath)!;
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_filePath, json);
        Logger.Enabled = settings.EnableLogging;
        SetAutoStart(settings.AutoStart);
        SettingsChanged?.Invoke(_current);
    }

    private void SetAutoStart(bool enable)
    {
        const string runKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        const string appName = "EyeGuard";

        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(runKey, writable: true);
            if (key == null) return;

            if (enable)
            {
                var exePath = Environment.ProcessPath;
                key.SetValue(appName, $"\"{exePath}\"");
            }
            else
            {
                if (key.GetValue(appName) != null)
                    key.DeleteValue(appName);
            }
        }
        catch
        {
            // Silently fail if registry access is denied
        }
    }

    private AppSettings Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch
        {
            // File corrupted, use defaults
        }
        return new AppSettings();
    }
}
