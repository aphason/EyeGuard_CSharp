# 爱眼卫士 新增需求设计文档

## 概述

本文档描述爱眼卫士（EyeGuard）的三个新增需求：解锁时关闭3分钟提示窗口、键盘鼠标静止暂停计时、内存占用优化。

---

## 需求1：解锁时关闭3分钟提示窗口

### 现状

- `MainWindow.OnThreeMinuteWarning()` 在工作倒计时剩余3分钟时弹出 `MessageBox.Show("3分钟后即将休息...")` 
- 该 MessageBox 是模态对话框，用户不点确定会一直存在
- 如果用户离开，强制休息自动触发并完成解锁后，该窗口仍悬浮在桌面上

### 方案

在 `App.xaml.cs` 的 `OnRestCompleted()` 方法中（解锁完成后），调用 Win32 API 关闭该窗口：

1. 添加 DllImport：
   - `FindWindow(string? lpClassName, string? lpWindowName)` — 按窗口标题查找
   - `SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam)` — 发送关闭消息
   - `WM_CLOSE = 0x0010`

2. 在 `HideLockScreens()` 之后调用：
   ```csharp
   private static void CloseThreeMinuteDialog()
   {
       var hwnd = FindWindow(null, "爱眼卫士提示");
       if (hwnd != IntPtr.Zero)
           SendMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
   }
   ```

### 涉及文件

- `App.xaml.cs` — 添加 DllImport、关闭方法、在 OnRestCompleted 中调用

---

## 需求2：键盘鼠标静止后暂停计时

### 配置项

在 `AppSettings` 中新增：
- `bool PauseOnIdle` — 默认 `true`，标识是否启用空闲暂停
- `int PauseOnIdleMinutes` — 默认 `5`，范围 `[3, 30]`，空闲多少分钟后暂停

### UI 变更

在 `SettingsWindow.xaml` 的"允许推迟次数"配置项之后、"软件全屏运行时暂停计时"之前，新增一行：

```
[✓] 键盘鼠标静止 [___5___] 分钟后暂停计时
     描述文字：勾选后键盘鼠标静止X分钟后暂停工作倒计时
```

- 勾选框绑定 `PauseOnIdle`
- 数字输入框绑定 `PauseOnIdleMinutes`，宽度 50，默认 5，范围 3-30
- 调整窗口高度（520 → ~570）以容纳新配置项

`SettingsWindow.xaml.cs` 相应修改 `LoadSettings()`、`ReadSettings()`、`_originalSettings` 拷贝。

### IdleDetector 服务

新建 `Services/IdleDetector.cs`：

```csharp
public class IdleDetector : IDisposable
{
    private readonly TimerService _timerService;
    private readonly SettingsService _settingsService;
    private System.Timers.Timer? _timer;
    private bool _isIdlePaused;
    private bool _isFirstTick = true;
    
    // 每 1 秒检测一次
    // 使用 GetLastInputInfo() 获取空闲时间
    // 仅在工作状态、且配置启用时生效
}
```

**检测逻辑**：
1. 每秒调用 `GetLastInputInfo` 获取最后输入时间（ticks）
2. 计算 `idleSeconds = (Environment.TickCount - lastInputTick) / 1000`
3. 如果 `!isIdlePaused && idleSeconds >= PauseOnIdleMinutes * 60`：
   - 设置 `_isIdlePaused = true`
   - 调用 `_timerService.Pause()`
4. 如果 `_isIdlePaused && idleSeconds < 2`（用户有输入动作）：
   - 设置 `_isIdlePaused = false`
   - 调用 `_timerService.StartWorkCycle()`（全新周期，重置推迟次数）

**启动时机**：在 `App.xaml.cs` 的 `OnStartup()` 中创建并启动 IdleDetector。

### DllImport

```csharp
[DllImport("user32.dll")]
static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

struct LASTINPUTINFO
{
    public uint cbSize;
    public uint dwTime;
}
```

### 涉及文件

- `Models/AppSettings.cs` — 新增属性
- `Services/IdleDetector.cs` — 新文件
- `Services/TimerService.cs` — 无修改（复用现有 Pause / StartWorkCycle）
- `SettingsWindow.xaml` — UI 布局
- `SettingsWindow.xaml.cs` — 加载/读取新配置
- `App.xaml.cs` — 初始化 IdleDetector，订阅事件

---

## 需求3：内存占用优化

### 分析

当前内存占用偏高（80-100M 启动，180M 长期运行）的原因：

1. **FullscreenDetector 内存分配**：`EnumWindows` 回调每1秒执行，每次分配多个 `StringBuilder` 和 `RECT/MONITORINFO` 结构体
2. **多 LockScreenWindow 实例**：每显示器一个全屏 WPF 窗口，每个都创建完整 UI 树
3. **Event 订阅泄漏风险**：TimerService 的事件订阅可能导致对象无法回收
4. **WPF 窗口 AllocksTransparency**：MainWindow 设了 `Background="Transparent"`，可能触发软件渲染路径

### 优化措施

#### 3.1 FullscreenDetector 优化
- 将 `StringBuilder` 实例移到类级别字段复用，避免每次回调分配
- `MONITORINFO` / `RECT` 结构体栈上分配无需改动

#### 3.2 LockScreenWindow 关闭时释放资源
- 确认 `Closed` 事件中正确反订阅事件
- 在 `LockScreenManager.HideLockScreens()` 中增加显式 `Dispose` 调用

#### 3.3 减少不必要的对象分配
- TimerService 的 `OnTick` 路径减少闭包分配
- Logger 的锁争用优化（不影响内存，但减少 CPU）

#### 3.4 GC 调优建议
- 在长时间空闲或状态切换后考虑调用 `GC.Collect()`（保守使用）

### 涉及文件

- `Services/FullscreenDetector.cs` — 复用 StringBuilder
- `Services/TimerService.cs` — 减少闭包分配
- `LockScreenManager.cs` — 资源释放

---

## 实施顺序

1. **需求1**（改动最小，独立）
2. **需求2**（核心功能，涉及新文件和配置）
3. **需求3**（优化性改动，最后做）

---

## 风险与注意事项

1. FindWindow 查找 MessageBox 依赖于窗口标题，如果后续修改标题文字需要同步更新
2. IdleDetector 和 FullscreenDetector 都可能调用 Pause()，需确认不会互相冲突
3. IdleDetector 的 `StartWorkCycle()` 会重置整个工作状态（包括推迟次数），符合需求
