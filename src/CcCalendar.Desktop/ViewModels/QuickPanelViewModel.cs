using CcCalendar.Core.Calendars;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CcCalendar.Desktop.ViewModels;

public sealed class QuickPanelViewModel : ObservableObject
{
    private readonly TimeProvider timeProvider;
    private string currentTime = string.Empty;
    private string dateText = string.Empty;

    public QuickPanelViewModel(
        TimeProvider timeProvider,
        IChineseCalendarService? chineseCalendarService = null,
        WeatherViewModel? weather = null)
    {
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        Calendar = new CalendarViewModel(timeProvider, chineseCalendarService);
        ChineseCalendarService = chineseCalendarService;
        Weather = weather;
        RefreshClock();
    }

    public CalendarViewModel Calendar { get; }

    public WeatherViewModel? Weather { get; }

    private IChineseCalendarService? ChineseCalendarService { get; }

    public string CurrentTime
    {
        get => currentTime;
        private set => SetProperty(ref currentTime, value);
    }

    public string DateText
    {
        get => dateText;
        private set => SetProperty(ref dateText, value);
    }

    public void RefreshClock()
    {
        DateTimeOffset now = timeProvider.GetLocalNow();
        CurrentTime = now.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        DateOnly currentDate = DateOnly.FromDateTime(now.DateTime);
        ChineseCalendarDayInfo info = ChineseCalendarService?.GetDay(currentDate)
            ?? ChineseCalendarDayInfo.Empty(currentDate);
        string lunarText = string.IsNullOrEmpty(info.LunarText) ? string.Empty : $" · 农历{info.LunarText}";
        DateText = $"{now.ToString("yyyy年M月d日 dddd", System.Globalization.CultureInfo.GetCultureInfo("zh-CN"))}{lunarText}";
    }
}
