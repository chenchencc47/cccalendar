using System.Globalization;
using System.Windows;
using CcCalendar.Core.Reminders;

namespace CcCalendar.Desktop;

public partial class ReminderPopupWindow : Window
{
    private readonly Func<ReminderDelivery, Task> acknowledge;
    private readonly Func<ReminderDelivery, TimeSpan, Task> snooze;
    private bool actionCompleted;

    public ReminderPopupWindow(
        ReminderDelivery delivery,
        int defaultSnoozeMinutes,
        Func<ReminderDelivery, Task> acknowledge,
        Func<ReminderDelivery, TimeSpan, Task> snooze)
    {
        InitializeComponent();
        this.acknowledge = acknowledge;
        this.snooze = snooze;
        Delivery = delivery;
        SnoozeOptions =
        [
            new("5 分钟", 5),
            new("10 分钟", 10),
            new("30 分钟", 30),
            new("1 小时", 60),
        ];
        SelectedSnoozeOption = SnoozeOptions.MinBy(
            option => Math.Abs(option.Minutes - defaultSnoozeMinutes))!;
        DataContext = this;
        Loaded += WindowLoaded;
        Closed += WindowClosed;
    }

    public ReminderDelivery Delivery { get; }

    public string Header => Delivery.WasMissed ? "错过提醒" : "提醒";

    public string TriggerText => $"原定 {Delivery.TriggerAtUtc.ToLocalTime().ToString("M月d日 HH:mm", CultureInfo.GetCultureInfo("zh-CN"))}";

    public IReadOnlyList<SnoozeOption> SnoozeOptions { get; }

    public SnoozeOption SelectedSnoozeOption { get; set; }

    public void CloseWithoutAction()
    {
        actionCompleted = true;
        Close();
    }

    private void WindowLoaded(object sender, RoutedEventArgs e)
    {
        Rect workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 16;
        Top = workArea.Bottom - Height - 16;
    }

    private async void SnoozeClick(object sender, RoutedEventArgs e)
    {
        IsEnabled = false;
        try
        {
            await snooze(Delivery, TimeSpan.FromMinutes(SelectedSnoozeOption.Minutes));
            actionCompleted = true;
            Close();
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
    }

    private async void AcknowledgeClick(object sender, RoutedEventArgs e)
    {
        IsEnabled = false;
        try
        {
            await acknowledge(Delivery);
            actionCompleted = true;
            Close();
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
    }

    private async void WindowClosed(object? sender, EventArgs e)
    {
        if (!actionCompleted)
        {
            try
            {
                await acknowledge(Delivery);
            }
            catch
            {
            }
        }
    }

    private void ShowError(Exception exception)
    {
        ErrorText.Text = $"操作失败：{exception.Message}";
        ErrorText.Visibility = Visibility.Visible;
        IsEnabled = true;
    }
}

public sealed record SnoozeOption(string Label, int Minutes);
