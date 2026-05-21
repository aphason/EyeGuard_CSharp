# 爱眼卫士 (EyeGuard)

**爱眼卫士** 是一款 C#+WPF(.NET 8.0) 编写的Windows 桌面定时休息护眼软件，帮助用户定时锁定屏幕，离开电脑休息，保护视力。
- 本软件借助AI生成，大量参考借鉴眼睛护士EyeFoo软件，增加了多屏幕锁定支持，感谢原作者！

## 功能特性

### 工作 / 休息双状态
- **工作时**：桌面右上角显示倒计时窗口，以 MM:SS 格式实时显示距下次休息的时间，背景进度条随倒计时逐渐消退
- ![图片描述](docs/images/1.工作时倒计时.png)
- **休息时**：锁定所有屏幕（主屏 + 扩展屏），全屏黑色遮罩，居中显示休息倒计时，屏蔽 Win 键、Alt+Tab、Alt+F4
-  ![图片描述](docs/images/4.休息时锁定.png)
  
### 右键菜单
- 立即休息、推迟休息（3/5/10 分钟，可配置总推迟次数）
- 窗口置顶 / 取消置顶
- 设置属性、关闭退出
- ![图片描述](docs/images/2.右键菜单.png)
### 三种强制休息模式
- **非强制**：休息时可随时点击解锁图标解锁
- **一般强制**：休息 1 分钟后显示解锁图标，点击解锁
- **完全强制**：休息期间不可解锁，必须等到倒计时结束

### 倒计时提示
- 距休息还剩 3 分钟时弹窗提示，同时播放 `breakpre.mid` 打铃提示音
- 休息开始时播放 `break.mid`，结束后播放 `unlock.mid`

### 设置属性
- 工作时间长度（分钟）
- 休息时间长度（分钟）
- 允许推迟休息次数（1-6 次）
- 键盘鼠标静止N分钟后暂停计时（恢复后重新计时）
- 全屏运行时暂停计时（支持检测全屏应用如 PPT、浏览器 F11、游戏等）
- 开机自启动
- ![图片描述](docs/images/3.属性设置.png)
  
### 其他特性
- 多屏幕支持（不同分辨率和缩放比例）
- 单实例运行
- 系统托盘图标
- 深色科技简洁风格 UI
- 窗口可拖拽移动，无边框设计

## 技术栈

- **语言**: C#
- **框架**: WPF (.NET 8.0)
- **目标平台**: Windows

## 项目结构

```
EyeGuard_CSharp/
├── EyeGuard/                  # 主项目
│   ├── App.xaml / App.xaml.cs  # 应用入口，服务初始化
│   ├── MainWindow.xaml / .cs   # 工作倒计时窗口
│   ├── LockScreenWindow.xaml / .cs  # 休息锁屏窗口
│   ├── SettingsWindow.xaml / .cs     # 设置属性窗口
│   ├── Models/                 # 数据模型
│   │   └── AppSettings.cs      # 应用设置模型
│   ├── Services/               # 核心服务
│   │   ├── AudioService.cs     # 音频播放服务
│   │   ├── FullscreenDetector.cs # 全屏应用检测
│   │   ├── IdleDetector.cs     # 空闲检测
│   │   ├── KeyboardHook.cs     # 键盘钩子（屏蔽按键）
│   │   ├── LockScreenManager.cs # 锁屏管理器
│   │   ├── Logger.cs           # 日志
│   │   ├── SettingsService.cs  # 设置持久化服务
│   │   └── TimerService.cs     # 计时器核心逻辑
│   └── resources/sounds/       # 提示音文件
│       ├── break.mid           # 休息开始提示音
│       ├── breakpre.mid        # 即将休息提示音
│       └── unlock.mid          # 解锁提示音
├── docs/                       # 文档
└── EyeGuard.sln                # 解决方案文件
```

## 构建运行

```bash
# 使用 .NET CLI 构建
dotnet build EyeGuard/EyeGuard.csproj

# 发布
dotnet publish EyeGuard/EyeGuard.csproj -c Release -o publish
```
