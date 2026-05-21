namespace EyeGuard.Models;

public enum ForceMode
{
    None,    // 非强制
    Semi,    // 一般强制（1分钟后可解锁）
    Full     // 完全强制（不可解锁）
}

public class AppSettings
{
    public bool AutoStart { get; set; } = false;
    public int WorkDurationMinutes { get; set; } = 50;
    public int RestDurationMinutes { get; set; } = 5;
    public int MaxPostponeCount { get; set; } = 3;
    public bool PauseOnFullscreen { get; set; } = false;
    public bool PauseOnIdle { get; set; } = true;
    public int PauseOnIdleMinutes { get; set; } = 5;
    public bool EnableLogging { get; set; } = false;
    public ForceMode ForceMode { get; set; } = ForceMode.None;

    public AppSettings Clone() => (AppSettings)MemberwiseClone();
}
