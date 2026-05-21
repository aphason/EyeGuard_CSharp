# 爱眼卫士新增需求 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现三个新需求：解锁关闭3分钟提示、键盘鼠标静止暂停计时、内存优化

**Architecture:** 在现有WPF应用基础上增量修改。需求1在App.xaml.cs中加Win32 API调用关闭窗口；需求2新建IdleDetector服务+修改AppSettings/SettingsWindow；需求3在FullscreenDetector等热点路径上做资源复用。

**Tech Stack:** .NET 8 WPF, Win32 API (user32.dll), System.Timers.Timer

---

### Task 1: 需求1 — 解锁时关闭3分钟提示窗口

**Files:**
- Modify: `EyeGuard/App.xaml.cs`

- [ ] **Step 1: 在 App.xaml.cs 中添加 DllImport 和关闭方法**

在文件顶部 `using` 区域之后添加 DllImport，并在 `OnRestCompleted` 中添加关闭调用：

```csharp
// App.xaml.cs 现有 using 区域后添加：
using System.Runtime.InteropServices;
using System.Text;
```

在 `App` 类中添加：

```csharp
private const uint WM_CLOSE = 0x0010;

[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

[DllImport("user32.dll", SetLastError = true)]
private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

private static void CloseThreeMinuteDialog()
{
    var hwnd = FindWindow(null, "爱眼卫士提示");
    if (hwnd != IntPtr.Zero)
    {
        SendMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
    }
}
```

- [ ] **Step 2: 在 OnRestCompleted 中调用关闭方法**

修改 `OnRestCompleted` 方法，在 `HideLockScreens()` 后调用：

```csharp
private void OnRestCompleted()
{
    Dispatcher.Invoke(() =>
    {
        _lockScreenManager?.HideLockScreens();
        CloseThreeMinuteDialog();  // 新增：关闭残留的3分钟提示窗口
        _fullscreenDetector?.Reset();
        _audioService?.PlayUnlock();
        _timerService?.StartWorkCycle();
    });
}
```

- [ ] **Step 3: 确认编译通过**

Run: `dotnet build EyeGuard/EyeGuard.csproj -c Debug`
Expected: Build succeeded

---

### Task 2: 需求2 — AppSettings 新增属性

**Files:**
- Modify: `EyeGuard/Models/AppSettings.cs`

- [ ] **Step 1: 添加 PauseOnIdle 和 PauseOnIdleMinutes 属性**

```csharp
public class AppSettings
{
    public bool AutoStart { get; set; } = false;
    public int WorkDurationMinutes { get; set; } = 50;
    public int RestDurationMinutes { get; set; } = 5;
    public int MaxPostponeCount { get; set; } = 3;
    public bool PauseOnFullscreen { get; set; } = false;
    public bool PauseOnIdle { get; set; } = true;          // 新增
    public int PauseOnIdleMinutes { get; set; } = 5;       // 新增
    public bool EnableLogging { get; set; } = false;
    public ForceMode ForceMode { get; set; } = ForceMode.None;
}
```

默认值：PauseOnIdle=true, PauseOnIdleMinutes=5，符合需求。

---

### Task 3: 需求2 — 新建 IdleDetector 服务

**Files:**
- Create: `EyeGuard/Services/IdleDetector.cs`

- [ ] **Step 1: 创建 IdleDetector 类**

```csharp
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
                Logger.Write("IdleDetector: PauseOnIdle disabled, resuming");
            }
            return;
        }

        if (_timerService.State != AppState.Working)
        {
            if (_isIdlePaused)
            {
                _isIdlePaused = false;
                Logger.Write("IdleDetector: state changed, clearing idle flag");
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
            Logger.Write($"IdleDetector: idle {idleSeconds}s >= {settings.PauseOnIdleMinutes}min → PAUSE");
        }
        else if (_isIdlePaused && idleSeconds < 2)
        {
            _isIdlePaused = false;
            _timerService.StartWorkCycle();
            Logger.Write("IdleDetector: input detected → new work cycle");
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
```

注意：`Environment.TickCount` 是 `int` 类型，约49天后会溢出。IdleDetector 通常在数小时内运行，且溢出后 dwTime 也会同时溢出，差值计算仍正确，因此无影响。

---

### Task 4: 需求2 — SettingsWindow UI 新增配置行

**Files:**
- Modify: `EyeGuard/SettingsWindow.xaml`
- Modify: `EyeGuard/SettingsWindow.xaml.cs`

- [ ] **Step 1: SettingsWindow.xaml — 在"允许推迟次数"后新增空闲暂停配置行**

在"允许推迟次数"的 Grid 之后、"软件全屏运行时暂停计时"的 Grid 之前，插入新行：

```xml
<!-- 允许推迟次数 (现有)... -->

<!-- Idle pause (新增) -->
<Grid Margin="0,0,0,12">
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="*"/>
        <ColumnDefinition Width="Auto"/>
    </Grid.ColumnDefinitions>
    <StackPanel Grid.Column="0">
        <TextBlock Text="键盘鼠标静止后暂停计时" Foreground="#e6edf3"/>
        <TextBlock Text="勾选后键盘鼠标静止时暂停工作倒计时" Foreground="#8b949e" FontSize="11"/>
    </StackPanel>
    <StackPanel Grid.Column="1" Orientation="Horizontal" VerticalAlignment="Center">
        <CheckBox x:Name="PauseOnIdleCheck" VerticalAlignment="Center"
                  Foreground="#e6edf3" Margin="0,0,8,0"/>
        <TextBlock Text="静止" Margin="0,0,4,0" Foreground="#8b949e" VerticalAlignment="Center"/>
        <TextBox x:Name="PauseOnIdleMinutesBox" Text="5" Width="50" TextAlignment="Center"
                 Background="#0d1117" Foreground="#c9d1d9" BorderBrush="#30363d"
                 Padding="4,3"/>
        <TextBlock Text="分钟后暂停" Margin="6,0,0,0" Foreground="#8b949e" VerticalAlignment="Center"/>
    </StackPanel>
</Grid>

<Separator Background="#30363d" Margin="0,0,0,12"/>

<!-- Pause on fullscreen (现有)... -->
```

同时调整窗口高度：`Height="520"` → `Height="580"`（或适当值以完整显示）。

- [ ] **Step 2: SettingsWindow.xaml.cs — 修改 LoadSettings、ReadSettings 和 _originalSettings**

```csharp
// _originalSettings 初始化中添加：
PauseOnIdle = settingsService.Current.PauseOnIdle,
PauseOnIdleMinutes = settingsService.Current.PauseOnIdleMinutes,

// LoadSettings 中添加：
PauseOnIdleCheck.IsChecked = s.PauseOnIdle;
PauseOnIdleMinutesBox.Text = s.PauseOnIdleMinutes.ToString();

// ReadSettings 中添加：
PauseOnIdle = PauseOnIdleCheck.IsChecked ?? true,
PauseOnIdleMinutes = System.Math.Clamp(
    int.TryParse(PauseOnIdleMinutesBox.Text, out int idleMin) ? idleMin : 5, 3, 30),

// Save_Click 的 _originalSettings 拷贝中添加：
PauseOnIdle = settings.PauseOnIdle,
PauseOnIdleMinutes = settings.PauseOnIdleMinutes,
```

---

### Task 5: 需求2 — App.xaml.cs 中初始化 IdleDetector

**Files:**
- Modify: `EyeGuard/App.xaml.cs`

- [ ] **Step 1: 添加 IdleDetector 字段和初始化**

```csharp
// 在现有字段区域新增：
private IdleDetector? _idleDetector;

// 在 OnStartup 中 _lockScreenManager 初始化后添加：
_idleDetector = new IdleDetector(_timerService, _settingsService);
_idleDetector.Start();

// 在 OnExit 中释放：
_idleDetector?.Dispose();
```

---

### Task 6: 需求3 — 内存优化

**Files:**
- Modify: `EyeGuard/Services/FullscreenDetector.cs`
- Modify: `EyeGuard/Services/TimerService.cs`

- [ ] **Step 1: FullscreenDetector — 将 StringBuilder 提升为字段复用**

```csharp
// 现有字段区域新增：
private readonly StringBuilder _classNameBuffer = new(256);
private readonly StringBuilder _titleBuffer = new(256);
```

在 `CheckFullscreen` 方法中，替换局部 StringBuilder 创建：

```csharp
// 替换：
var className = new StringBuilder(256);
GetClassName(hWnd, className, 256);
// 为：
_classNameBuffer.Clear();
GetClassName(hWnd, _classNameBuffer, 256);
```

和：

```csharp
// 替换：
var title = new StringBuilder(len + 1);
if (len > 0) GetWindowText(hWnd, title, len + 1);
// 为：
_titleBuffer.Clear();
_titleBuffer.Capacity = len + 1;
if (len > 0) GetWindowText(hWnd, _titleBuffer, len + 1);
```

- [ ] **Step 2: TimerService — 减少闭包分配**

`System.Timers.Timer` 的 `Elapsed` 事件处理器已经在 `OnTick` 实例方法中，不存在额外闭包。无需修改。

但 `LockScreenWindow.xaml.cs` 中有一处 lambda 事件订阅：

```csharp
_oneMinTimer.Elapsed += (s, e) =>
{
    // ...
    Dispatcher.Invoke(() =>
    {
        _canUnlock = true;
        ShowUnlockButton();
    });
};
```

这会在定时器触发后自行清理（timer stop+dispose），不会持续引用。也无需修改。

- [ ] **Step 3: LockScreenManager — 确保资源释放**

`HideLockScreens()` 已经调用 `win.Close()`，但 Close 不保证立即释放所有非托管资源。改为：

```csharp
public void HideLockScreens()
{
    foreach (var win in _lockWindows)
    {
        win.Close();
    }
    _lockWindows.Clear();
    _keyboardHook.Disable();
}
```

这里 `Close()` 后就清除了列表，每个 LockScreenWindow 的 `Closed` 事件已经反订阅了 `RestTick` 和 `SourceInitialized`。无需额外修改。

---

### Task 7: 验证构建

**Files:** N/A

- [ ] **Step 1: 构建项目**

```bash
dotnet build EyeGuard/EyeGuard.csproj -c Debug
```

Expected: Build succeeded (0 errors, 0 warnings)
