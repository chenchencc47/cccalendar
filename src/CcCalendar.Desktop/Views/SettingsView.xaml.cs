using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CcCalendar.Core.Configuration;
using CcCalendar.Desktop.ViewModels;

namespace CcCalendar.Desktop.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        Loaded += OnSettingsViewLoaded;
    }

    /// <summary>
    /// 显示当前版本。此前这里是一句写死的 `Text="当前版本 0.6.5；…"`，
    /// 发版时无人更新，于是更新到 0.6.7 后界面仍显示 0.6.5。现在从程序集读取。
    /// </summary>
    private void OnSettingsViewLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        Version version = App.CurrentVersion;
        CurrentVersionText.Text =
            $"当前版本 {version.Major}.{version.Minor}.{version.Build}；启动时也会自动检查";
    }

    /// <summary>
    /// 查看更新内容。
    ///
    /// 说明优先取公网清单的 `releaseNotes`——它随发版一起更新，因此不会像原先写死的常量
    /// 那样过期（原先点开会看到 0.6.5 的说明）。清单不可达时给出明确提示，
    /// 而不是展示一份可能是错的旧说明。
    /// </summary>
    private async void ReleaseNotesClick(object sender, System.Windows.RoutedEventArgs e)
    {
        Window? owner = Window.GetWindow(this);
        string? notes = null;

        if (System.Windows.Application.Current is App app)
        {
            notes = await app.FetchReleaseNotesAsync();
        }

        Version version = App.CurrentVersion;
        string heading = $"当前版本 {version.Major}.{version.Minor}.{version.Build}";
        bool hasNotes = !string.IsNullOrWhiteSpace(notes);

        MessageBox.Show(
            owner,
            hasNotes
                ? notes
                : $"{heading}\n\n暂时无法获取更新说明，请检查网络后重试。",
            "cccalendar 更新内容",
            MessageBoxButton.OK,
            hasNotes ? MessageBoxImage.Information : MessageBoxImage.Warning);
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
