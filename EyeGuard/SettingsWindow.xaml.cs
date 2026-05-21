using System.Windows;
using EyeGuard.Models;
using EyeGuard.Services;

namespace EyeGuard;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _settingsService;
    private AppSettings _originalSettings;

    public SettingsWindow(SettingsService settingsService)
    {
        InitializeComponent();
        _settingsService = settingsService;

        _originalSettings = settingsService.Current.Clone();
        LoadSettings(_originalSettings);
        ForceModeCombo.SelectionChanged += (s, e) => UpdateForceModeHint();
        UpdateForceModeHint();
    }

    private void LoadSettings(AppSettings s)
    {
        AutoStartCheck.IsChecked = s.AutoStart;
        WorkDurationBox.Text = s.WorkDurationMinutes.ToString();
        RestDurationBox.Text = s.RestDurationMinutes.ToString();
        PostponeCombo.SelectedIndex = s.MaxPostponeCount - 1;
        PauseOnFullscreenCheck.IsChecked = s.PauseOnFullscreen;
        PauseOnIdleCheck.IsChecked = s.PauseOnIdle;
        PauseOnIdleMinutesBox.Text = s.PauseOnIdleMinutes.ToString();
        EnableLoggingCheck.IsChecked = s.EnableLogging;
        ForceModeCombo.SelectedIndex = (int)s.ForceMode;
    }

    private void UpdateForceModeHint()
    {
        var hints = new[] {
            "非强制：休息过程中可随时解锁",
            "一般强制：休息1分钟后显示解锁按钮",
            "完全强制：休息过程中不可解锁，必须等到休息结束"
        };
        ForceModeHint.Text = hints[ForceModeCombo.SelectedIndex];
    }

    private AppSettings ReadSettings()
    {
        int.TryParse(WorkDurationBox.Text, out int workMinutes);
        int.TryParse(RestDurationBox.Text, out int restMinutes);

        return new AppSettings
        {
            AutoStart = AutoStartCheck.IsChecked ?? false,
            WorkDurationMinutes = System.Math.Clamp(workMinutes, 1, 480),
            RestDurationMinutes = System.Math.Clamp(restMinutes, 1, 240),
            MaxPostponeCount = PostponeCombo.SelectedIndex + 1,
            PauseOnFullscreen = PauseOnFullscreenCheck.IsChecked ?? false,
            PauseOnIdle = PauseOnIdleCheck.IsChecked ?? false,
            PauseOnIdleMinutes = System.Math.Clamp(
                int.TryParse(PauseOnIdleMinutesBox.Text, out int idleMin) ? idleMin : 5, 3, 30),
            EnableLogging = EnableLoggingCheck.IsChecked ?? false,
            ForceMode = (ForceMode)ForceModeCombo.SelectedIndex
        };
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var result = System.Windows.MessageBox.Show("确认保存设置？保存后设置将立即生效。", "爱眼卫士",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        var settings = ReadSettings();
        _settingsService.Save(settings);
        _originalSettings = settings.Clone();
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        var result = System.Windows.MessageBox.Show("确认取消？修改将不会保存。", "爱眼卫士",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        DialogResult = false;
        Close();
    }
}
