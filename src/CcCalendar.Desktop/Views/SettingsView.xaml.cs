using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CcCalendar.Core.Configuration;
using CcCalendar.Desktop.ViewModels;

namespace CcCalendar.Desktop.Views;

public partial class SettingsView : UserControl
{
    private const string CurrentReleaseNotes = "0.6.5 更新内容：\n\n"
        + "• 修复退出应用后托盘残留图标仍可触发窗口异常的问题。\n"
        + "• 退出开始时立即隐藏托盘图标，并忽略退出期间到达的托盘操作。\n"
        + "• 提醒调度器退出最多等待 2 秒，避免后台任务拖住应用进程。\n"
        + "• 增加单实例保护，重复启动不会再产生多个后台进程。\n"
        + "• 托盘通知和资源销毁改为可重复调用，减少升级时残留进程。";

    public SettingsView()
    {
        InitializeComponent();
    }

    private void ReleaseNotesClick(object sender, System.Windows.RoutedEventArgs e)
    {
        MessageBox.Show(
            Window.GetWindow(this),
            CurrentReleaseNotes,
            "cccalendar 更新内容",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void ShortcutPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox { DataContext: ShortcutBindingViewModel binding })
        {
            return;
        }

        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl
            or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin)
        {
            e.Handled = true;
            return;
        }

        ModifierKeys modifiers = Keyboard.Modifiers;
        binding.TryAssign(new ShortcutGestureSettings
        {
            Control = modifiers.HasFlag(ModifierKeys.Control),
            Alt = modifiers.HasFlag(ModifierKeys.Alt),
            Shift = modifiers.HasFlag(ModifierKeys.Shift),
            Windows = modifiers.HasFlag(ModifierKeys.Windows),
            Key = key.ToString(),
        });
        e.Handled = true;
    }

    private void AiApiKeyPasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is PasswordBox passwordBox
            && DataContext is AppearanceSettingsViewModel { Ai: not null } settings)
        {
            settings.Ai.ApiKeyInput = passwordBox.Password;
        }
    }

    private void PasteAiEndpointClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (!System.Windows.Clipboard.ContainsText())
        {
            return;
        }

        AiEndpointTextBox.Text = System.Windows.Clipboard.GetText().Trim();
        AiEndpointTextBox.Focus();
        AiEndpointTextBox.CaretIndex = AiEndpointTextBox.Text.Length;
    }

    private async void CheckForUpdatesClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (System.Windows.Application.Current is App app)
        {
            Window? owner = System.Windows.Window.GetWindow(this);
            try
            {
                App.UpdateCheckResult result = await app.CheckForUpdatesAsync(owner);
                switch (result.Status)
                {
                    case App.UpdateCheckStatus.Available:
                        MessageBox.Show(
                            owner,
                            $"发现新版本 {result.Update?.Version}，主窗口右上角已显示下载按钮。",
                            "cccalendar 更新",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                        break;
                    case App.UpdateCheckStatus.Latest:
                        MessageBox.Show(
                            owner,
                            $"当前已是最新版本（{result.CurrentVersion}）。",
                            "cccalendar 更新",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                        break;
                    case App.UpdateCheckStatus.Failed:
                        MessageBox.Show(
                            owner,
                            $"检查更新失败：{result.Error}",
                            "cccalendar 更新",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        break;
                    default:
                        MessageBox.Show(
                            owner,
                            result.Error ?? "当前无法检查更新。",
                            "cccalendar 更新",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        break;
                }
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    owner,
                    $"检查更新失败：{exception.Message}",
                    "cccalendar 更新",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
    }

    private void CustomColorClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: AppearanceColorOptionViewModel option })
        {
            return;
        }

        var dialog = new ColorPickerWindow(option.Value)
        {
            Owner = Window.GetWindow(this),
        };
        if (dialog.ShowDialog() == true)
        {
            option.Value = dialog.SelectedColor;
            option.IsPaletteOpen = false;
        }
    }
}
