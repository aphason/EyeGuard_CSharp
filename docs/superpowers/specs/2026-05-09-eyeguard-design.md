# 爱眼卫士 (EyeGuard) — 设计文档

## 概述

Windows 桌面定时休息护眼软件，工作/休息双状态切换，支持多屏幕、全屏检测、强制休息模式。

## 技术选型

- **运行时**: .NET 8
- **UI 框架**: WPF (Windows Presentation Foundation)
- **语言**: C# 12
- **模式**: 代码后置 + 服务层（轻量架构，非 MVVM 重型框架）

## 项目结构

```
EyeGuard_CSharp/
├── EyeGuard.sln
├── EyeGuard/
│   ├── App.xaml / App.cs           # 应用入口 + 系统托盘初始化
│   ├── MainWindow.xaml/.cs         # 倒计时窗口（工作时）
│   ├── LockScreenWindow.xaml/.cs   # 锁屏窗口（休息时）
│   ├── SettingsWindow.xaml/.cs     # 设置对话框（模态）
│   ├── Services/
│   │   ├── TimerService.cs         # 状态机 + 计时核心
│   │   ├── FullscreenDetector.cs   # 全屏应用检测
│   │   ├── AudioService.cs         # MID 音频播放
│   │   ├── KeyboardHook.cs         # 低级键盘钩子
│   │   └── SettingsService.cs      # 设置持久化
│   ├── Models/
│   │   └── AppSettings.cs          # 设置数据模型
│   ├── resources/sounds/           # MID 音频文件
│   └── appsettings.json            # 配置存储
```

## 状态机

```
工作中 ──(计时结束 / 立即休息)──▶ 休息中
  ▲                                    │
  │◀──(休息结束 / 用户解锁)─────────────┘

工作中子状态:
  ├── 推迟 → 倒计时累加 3/5/10 分钟
  ├── 全屏应用 → 暂停计时 → 退出全屏 → 恢复
  └── 关机重启 → 重新开始

休息中子状态:
  ├── 非强制 → 解锁图标始终显示
  ├── 一般强制 → 1 分钟后显示解锁图标
  ├── 完全强制 → 无解锁图标
  └── 计时结束 → 播放 unlock.mid → 回到工作中
```

## 组件设计

### 1. CountdownWindow (MainWindow)

- 尺寸: 180×70，无边框，无最大/最小/关闭按钮
- 位置: 桌面右上角，支持鼠标拖动
- 显示: MM:SS 倒计时，每秒刷新
- 进度条: 深色背景上的蓝色进度条，从满到空（从右向左消退）
- 右键菜单: 立即休息、推迟3/5/10分钟、总在最前/取消最前、设置属性、关闭退出
- 倒计时 3 分钟时: 弹出提示窗口 + 播放 breakpre.mid
- 总在最前: 设置 Topmost = true/false
- 推迟: 每点击消耗一次推迟次数，用完禁用菜单项

### 2. LockScreenWindow (锁屏窗口)

- 每屏幕一个实例（主屏 + 扩展屏）
- 全屏黑色背景，覆盖所有内容
- 居中显示休息倒计时（大号字体）
- 右下角: 条件显示解锁图标
- 键盘钩子: 屏蔽 Win 键、Alt+Tab、Alt+F4

### 3. SettingsWindow (设置对话框)

- 模态窗口，深色主题
- 设置项:
  - 开机自启动（复选框 + 注册表写入）
  - 工作时间（分钟，数字输入）
  - 休息时间（分钟，数字输入）
  - 允许推迟次数（1-6 下拉）
  - 强制休息模式（非强制/一般强制/完全强制下拉）
- 保存: 写入 JSON 文件，实时生效
- 取消: 恢复修改前的值
- 关闭图标: 等价于取消

### 4. TimerService

- 核心状态机，管理 Working/Resting/Paused 状态
- Timer 驱动倒计时
- 事件: OnWorkTick, OnRestTick, OnWorkCompleted, OnRestCompleted
- 推迟逻辑: 在原基础上加时间，消耗次数
- 每次休息完成后重置推迟次数

### 5. FullscreenDetector

- 定时轮询（约 1 秒间隔）
- 使用 Windows API (EnumWindows + GetWindowPlacement) 检测全屏窗口
- 全屏条件: 窗口大小等于屏幕工作区大小且无标题栏
- 检测到全屏时通知 TimerService 暂停
- 退出全屏时通知 TimerService 恢复

### 6. AudioService

- 使用 PlaySound API 或 mciSendString 播放 MID 文件
- 支持: breakpre.mid（提前 3 分钟提醒）、break.mid（休息开始）、unlock.mid（解锁/休息结束）

### 7. KeyboardHook

- 低级键盘钩子 (SetWindowsHookEx WH_KEYBOARD_LL)
- 休息时拦截: Win 键、Alt+Tab、Alt+F4
- 工作中不拦截

### 8. SettingsService

- JSON 文件读写 (System.Text.Json)
- 默认路径: %APPDATA%\EyeGuard\settings.json
- 保存时立即写文件，加载时反序列化

## 多屏幕策略

- LockScreenWindow 通过 Screen.AllScreens 枚举所有显示器
- 每个屏幕创建一个全屏窗口，窗口位置和尺寸匹配屏幕边界
- DPI: WPF 设备无关单位自动适应不同 DPI

## 配置数据模型 (AppSettings)

```csharp
class AppSettings {
    bool AutoStart = false;
    int WorkDurationMinutes = 50;   // 1-480
    int RestDurationMinutes = 5;    // 1-240
    int MaxPostponeCount = 3;       // 1-10
    ForceMode ForceMode = ForceMode.None;  // None/Semi/Full
}

enum ForceMode { None, Semi, Full }
```

## 错误处理

- MID 文件缺失: 静默跳过音频播放，不影响计时
- 配置文件损坏: 重置为默认值，写日志
- 多屏幕热插拔: 显示锁屏时按当前屏幕列表创建窗口
- 全屏检测异常: 默认为未全屏，不暂停计时

## 日志

- 使用 System.Diagnostics.Trace 输出日志
- 日志记录: 状态切换、推迟操作、全屏检测、异常
