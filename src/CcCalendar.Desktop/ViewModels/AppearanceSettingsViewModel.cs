using CcCalendar.Core.Configuration;
using CcCalendar.Desktop.Desktop;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CcCalendar.Desktop.ViewModels;

public sealed class AppearanceSettingsViewModel : ObservableObject
{
    private readonly Action<DesktopAppearanceTarget, DesktopAppearanceSettings> apply;
    private readonly Action<bool> startWithMainWindowHiddenChanged;
    private readonly Action<ThemePreference>? appThemeChanged;
    private readonly DesktopAppearanceSettingsState state;
    private string colorValidationMessage = string.Empty;
    private bool startWithMainWindowHidden;
    private ThemePreference appTheme;

    public AppearanceSettingsViewModel(
        DesktopAppearanceSettings workbench,
        DesktopAppearanceSettings calendar,
        DesktopAppearanceSettings agenda,
        DesktopAppearanceSettings todo,
        Action<DesktopAppearanceTarget, DesktopAppearanceSettings>? apply = null,
        ShortcutSettingsViewModel? shortcuts = null,
        ReminderSettingsViewModel? reminderNotifications = null,
        AiSettingsViewModel? ai = null,
        bool startWithMainWindowHidden = false,
        Action<bool>? startWithMainWindowHiddenChanged = null,
        ThemePreference appTheme = ThemePreference.System,
        Action<ThemePreference>? appThemeChanged = null,
        TeamConnectionViewModel? teamConnection = null)
    {
        state = new DesktopAppearanceSettingsState(workbench, calendar, agenda, todo);
        this.apply = apply ?? ((_, _) => { });
        this.startWithMainWindowHidden = startWithMainWindowHidden;
        this.startWithMainWindowHiddenChanged = startWithMainWindowHiddenChanged ?? (_ => { });
        this.appTheme = appTheme;
        this.appThemeChanged = appThemeChanged;
        FontFamilies =
        [
            "Segoe UI Variable, Microsoft YaHei UI",
            "Microsoft YaHei UI",
            "Segoe UI",
            "Arial",
        ];
        ColorOptions = CreateColorOptions();
        Shortcuts = shortcuts;
        ReminderNotifications = reminderNotifications;
        Ai = ai;
        TeamConnection = teamConnection;
        RestoreDefaultsCommand = new RelayCommand(RestoreDefaults);
    }

    public IReadOnlyList<string> FontFamilies { get; }

    public IReadOnlyList<AppearanceColorOptionViewModel> ColorOptions { get; }

    public ShortcutSettingsViewModel? Shortcuts { get; }

    public ReminderSettingsViewModel? ReminderNotifications { get; }

    public AiSettingsViewModel? Ai { get; }

    public TeamConnectionViewModel? TeamConnection { get; }

    public bool StartWithMainWindowHidden
    {
        get => startWithMainWindowHidden;
        set
        {
            if (SetProperty(ref startWithMainWindowHidden, value))
            {
                startWithMainWindowHiddenChanged(value);
            }
        }
    }

    public DesktopAppearanceTarget SelectedTarget
    {
        get => state.SelectedTarget;
        set
        {
            if (value == state.SelectedTarget)
            {
                return;
            }

            state.SelectTarget(value);
            ColorValidationMessage = string.Empty;
            RefreshColorOptions();
            OnPropertyChanged(string.Empty);
        }
    }

    public ThemePreference Theme
    {
        get => Current.Theme;
        set => Change(() => state.SetTheme(value));
    }

    public ThemePreference AppTheme
    {
        get => appTheme;
        set
        {
            if (SetProperty(ref appTheme, value))
            {
                appThemeChanged?.Invoke(value);
            }
        }
    }

    public DesktopBackgroundMaterial Material
    {
        get => Current.Material;
        set => Change(() => state.SetMaterial(value));
    }

    public double OpacityPercent
    {
        get => Current.Opacity * 100;
        set => Change(() => state.SetOpacity(value / 100));
    }

    public double CornerRadius
    {
        get => Current.CornerRadius;
        set => Change(() => state.SetCornerRadius(value));
    }

    public double ScalePercent
    {
        get => Current.Scale * 100;
        set => Change(() => state.SetScale(value / 100));
    }

    public string FontFamily
    {
        get => Current.FontFamily;
        set => Change(() => state.Update(settings => settings with { FontFamily = value }));
    }

    public double FontSize
    {
        get => Current.FontSize;
        set => Change(() => state.Update(settings => settings with
        {
            FontSize = Math.Clamp(value, 11, 20),
        }));
    }

    public bool IsTextBold
    {
        get => Current.IsTextBold;
        set => Change(() => state.Update(settings => settings with { IsTextBold = value }));
    }

    public DesktopDensity Density
    {
        get => Current.Density;
        set => Change(() => state.Update(settings => settings with { Density = value }));
    }

    public bool ShowDateEvents
    {
        get => Current.ShowDateEvents;
        set => Change(() => state.Update(settings => settings with { ShowDateEvents = value }));
    }

    public bool ShowDetailedEvents
    {
        get => Current.ShowDetailedEvents;
        set => Change(() => state.Update(settings => settings with { ShowDetailedEvents = value }));
    }

    public bool ShowDateSeparators
    {
        get => Current.ShowDateSeparators;
        set => Change(() => state.Update(settings => settings with { ShowDateSeparators = value }));
    }

    public bool HighlightCurrentMonth
    {
        get => Current.HighlightCurrentMonth;
        set => Change(() => state.Update(settings => settings with { HighlightCurrentMonth = value }));
    }

    public bool ShowTextOutline
    {
        get => Current.ShowTextOutline;
        set => Change(() => state.Update(settings => settings with { ShowTextOutline = value }));
    }

    public string BackgroundColor
    {
        get => Current.BackgroundColor;
        set => SetColor(DesktopColorRole.Background, value);
    }

    public string PrimaryTextColor
    {
        get => Current.PrimaryTextColor;
        set => SetColor(DesktopColorRole.PrimaryText, value);
    }

    public string SecondaryTextColor
    {
        get => Current.SecondaryTextColor;
        set => SetColor(DesktopColorRole.SecondaryText, value);
    }

    public string DateTextColor
    {
        get => Current.DateTextColor;
        set => SetColor(DesktopColorRole.DateText, value);
    }

    public string LunarTextColor
    {
        get => Current.LunarTextColor;
        set => SetColor(DesktopColorRole.LunarText, value);
    }

    public string WeekendTextColor
    {
        get => Current.WeekendTextColor;
        set => SetColor(DesktopColorRole.WeekendText, value);
    }

    public string HolidayTextColor
    {
        get => Current.HolidayTextColor;
        set => SetColor(DesktopColorRole.HolidayText, value);
    }

    public string DayOffBackgroundColor
    {
        get => Current.DayOffBackgroundColor;
        set => SetColor(DesktopColorRole.DayOffBackground, value);
    }

    public string DayOffTextColor
    {
        get => Current.DayOffTextColor;
        set => SetColor(DesktopColorRole.DayOffText, value);
    }

    public string WorkdayBackgroundColor
    {
        get => Current.WorkdayBackgroundColor;
        set => SetColor(DesktopColorRole.WorkdayBackground, value);
    }

    public string WorkdayTextColor
    {
        get => Current.WorkdayTextColor;
        set => SetColor(DesktopColorRole.WorkdayText, value);
    }

    public string TodayBackgroundColor
    {
        get => Current.TodayBackgroundColor;
        set => SetColor(DesktopColorRole.TodayBackground, value);
    }

    public string TodayTextColor
    {
        get => Current.TodayTextColor;
        set => SetColor(DesktopColorRole.TodayText, value);
    }

    public string SeparatorColor
    {
        get => Current.SeparatorColor;
        set => SetColor(DesktopColorRole.Separator, value);
    }

    public string ColorValidationMessage
    {
        get => colorValidationMessage;
        private set => SetProperty(ref colorValidationMessage, value);
    }

    public IRelayCommand RestoreDefaultsCommand { get; }

    public DesktopAppearanceSettings GetSettings(DesktopAppearanceTarget target)
    {
        return state.GetSettings(target);
    }

    private DesktopAppearanceSettings Current => state.Current;

    private void Change(Action update)
    {
        DesktopAppearanceSettings before = Current;
        update();
        if (before == Current)
        {
            return;
        }

        ApplyCurrent();
        RefreshColorOptions();
        OnPropertyChanged(string.Empty);
    }

    private void SetColor(DesktopColorRole role, string value)
    {
        if (!state.SetColor(role, value))
        {
            ColorValidationMessage = "颜色需使用 #RRGGBB 格式";
            OnPropertyChanged(string.Empty);
            return;
        }

        ColorValidationMessage = string.Empty;
        ApplyCurrent();
        RefreshColorOptions();
        OnPropertyChanged(string.Empty);
    }

    private void RestoreDefaults()
    {
        state.RestoreDefaults();
        ColorValidationMessage = string.Empty;
        ApplyCurrent();
        RefreshColorOptions();
        OnPropertyChanged(string.Empty);
    }

    private void ApplyCurrent()
    {
        apply(state.SelectedTarget, Current);
    }

    private IReadOnlyList<AppearanceColorOptionViewModel> CreateColorOptions()
    {
        return
        [
            CreateColorOption("窗口背景", DesktopColorRole.Background),
            CreateColorOption("主要文字", DesktopColorRole.PrimaryText),
            CreateColorOption("次要文字", DesktopColorRole.SecondaryText),
            CreateColorOption("日期文字", DesktopColorRole.DateText),
            CreateColorOption("农历文字", DesktopColorRole.LunarText),
            CreateColorOption("周末日期", DesktopColorRole.WeekendText),
            CreateColorOption("节日文字", DesktopColorRole.HolidayText),
            CreateColorOption("调休背景", DesktopColorRole.DayOffBackground),
            CreateColorOption("调休文字", DesktopColorRole.DayOffText),
            CreateColorOption("调班背景", DesktopColorRole.WorkdayBackground),
            CreateColorOption("调班文字", DesktopColorRole.WorkdayText),
            CreateColorOption("今天背景", DesktopColorRole.TodayBackground),
            CreateColorOption("今天文字", DesktopColorRole.TodayText),
            CreateColorOption("分割线", DesktopColorRole.Separator),
        ];
    }

    private AppearanceColorOptionViewModel CreateColorOption(
        string label,
        DesktopColorRole role)
    {
        return new AppearanceColorOptionViewModel(
            label,
            role,
            GetColor(role),
            SetColor);
    }

    private void RefreshColorOptions()
    {
        foreach (AppearanceColorOptionViewModel option in ColorOptions)
        {
            option.SetValueFromSettings(GetColor(option.Role));
        }
    }

    private string GetColor(DesktopColorRole role)
    {
        return role switch
        {
            DesktopColorRole.Background => Current.BackgroundColor,
            DesktopColorRole.PrimaryText => Current.PrimaryTextColor,
            DesktopColorRole.SecondaryText => Current.SecondaryTextColor,
            DesktopColorRole.DateText => Current.DateTextColor,
            DesktopColorRole.LunarText => Current.LunarTextColor,
            DesktopColorRole.WeekendText => Current.WeekendTextColor,
            DesktopColorRole.HolidayText => Current.HolidayTextColor,
            DesktopColorRole.DayOffBackground => Current.DayOffBackgroundColor,
            DesktopColorRole.DayOffText => Current.DayOffTextColor,
            DesktopColorRole.WorkdayBackground => Current.WorkdayBackgroundColor,
            DesktopColorRole.WorkdayText => Current.WorkdayTextColor,
            DesktopColorRole.TodayBackground => Current.TodayBackgroundColor,
            DesktopColorRole.TodayText => Current.TodayTextColor,
            DesktopColorRole.Separator => Current.SeparatorColor,
            _ => throw new ArgumentOutOfRangeException(nameof(role)),
        };
    }
}

public sealed class AppearanceColorOptionViewModel : ObservableObject
{
    private static readonly string[] PaletteColors = CreatePaletteColors();

    private readonly Action<DesktopColorRole, string> update;
    private string value;
    private bool isPaletteOpen;
    private string customValue;
    private int red;
    private int green;
    private int blue;

    public AppearanceColorOptionViewModel(
        string label,
        DesktopColorRole role,
        string value,
        Action<DesktopColorRole, string> update)
    {
        Label = label;
        Role = role;
        this.value = value;
        customValue = value;
        this.update = update;
        SyncChannels(value);
        PaletteOptions = [.. PaletteColors.Select(color => new ColorChoiceViewModel(color, () => SelectColor(color)))];
        TogglePaletteCommand = new RelayCommand(() =>
        {
            CustomValue = Value;
            IsPaletteOpen = !IsPaletteOpen;
        });
        ApplyCustomColorCommand = new RelayCommand(ApplyCustomColor);
    }

    public string Label { get; }

    public DesktopColorRole Role { get; }

    public IReadOnlyList<ColorChoiceViewModel> PaletteOptions { get; }

    public IRelayCommand TogglePaletteCommand { get; }

    public IRelayCommand ApplyCustomColorCommand { get; }

    public bool IsPaletteOpen
    {
        get => isPaletteOpen;
        set => SetProperty(ref isPaletteOpen, value);
    }

    public string CustomValue
    {
        get => customValue;
        set => SetProperty(ref customValue, value);
    }

    public int Red
    {
        get => red;
        set
        {
            if (SetProperty(ref red, Math.Clamp(value, 0, 255)))
            {
                ApplyChannels();
            }
        }
    }

    public int Green
    {
        get => green;
        set
        {
            if (SetProperty(ref green, Math.Clamp(value, 0, 255)))
            {
                ApplyChannels();
            }
        }
    }

    public int Blue
    {
        get => blue;
        set
        {
            if (SetProperty(ref blue, Math.Clamp(value, 0, 255)))
            {
                ApplyChannels();
            }
        }
    }

    public string Value
    {
        get => value;
        set
        {
            if (SetProperty(ref this.value, value))
            {
                customValue = value;
                OnPropertyChanged(nameof(CustomValue));
                SyncChannels(value);
                update(Role, value);
            }
        }
    }

    public void SetValueFromSettings(string newValue)
    {
        SetProperty(ref value, newValue, nameof(Value));
        CustomValue = newValue;
        SyncChannels(newValue);
    }

    private void SelectColor(string color)
    {
        Value = color;
        IsPaletteOpen = false;
    }

    private void ApplyCustomColor()
    {
        Value = CustomValue;
        IsPaletteOpen = false;
    }

    private static string[] CreatePaletteColors()
    {
        var colors = new List<string>
        {
            "#FFFFFF", "#F5F7FA", "#E8F0FB", "#D9DEE5", "#20242A", "#626A75",
            "#246BCE", "#2E7D4F", "#C43D4B", "#F59E0B", "#7A3E9D", "#0891B2",
        };

        foreach (double hue in Enumerable.Range(0, 12).Select(index => index * 30d))
        {
            foreach (double value in new[] { 1d, 0.88d, 0.75d, 0.62d, 0.5d, 0.38d, 0.26d })
            {
                colors.Add(HsvToHex(hue, 0.82d, value));
            }

            foreach (double value in new[] { 1d, 0.88d, 0.76d })
            {
                colors.Add(HsvToHex(hue, 0.35d, value));
            }
        }

        colors.AddRange(Enumerable.Range(0, 12).Select(index =>
        {
            byte channel = (byte)Math.Round(index * 255d / 11d);
            return $"#{channel:X2}{channel:X2}{channel:X2}";
        }));
        return [.. colors.Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    private static string HsvToHex(double hue, double saturation, double value)
    {
        double chroma = value * saturation;
        double segment = hue / 60d;
        double x = chroma * (1d - Math.Abs(segment % 2d - 1d));
        (double red, double green, double blue) = segment switch
        {
            < 1 => (chroma, x, 0d),
            < 2 => (x, chroma, 0d),
            < 3 => (0d, chroma, x),
            < 4 => (0d, x, chroma),
            < 5 => (x, 0d, chroma),
            _ => (chroma, 0d, x),
        };
        double match = value - chroma;
        return $"#{ToByte(red + match):X2}{ToByte(green + match):X2}{ToByte(blue + match):X2}";
    }

    private static byte ToByte(double value) => (byte)Math.Clamp(Math.Round(value * 255d), 0d, 255d);

    private void ApplyChannels()
    {
        Value = $"#{Red:X2}{Green:X2}{Blue:X2}";
    }

    private void SyncChannels(string color)
    {
        if (color.Length != 7
            || color[0] != '#'
            || !byte.TryParse(color[1..3], System.Globalization.NumberStyles.HexNumber, null, out byte nextRed)
            || !byte.TryParse(color[3..5], System.Globalization.NumberStyles.HexNumber, null, out byte nextGreen)
            || !byte.TryParse(color[5..7], System.Globalization.NumberStyles.HexNumber, null, out byte nextBlue))
        {
            return;
        }

        SetProperty(ref red, nextRed, nameof(Red));
        SetProperty(ref green, nextGreen, nameof(Green));
        SetProperty(ref blue, nextBlue, nameof(Blue));
    }
}

public sealed class ColorChoiceViewModel
{
    public ColorChoiceViewModel(string color, Action select)
    {
        Color = color;
        SelectCommand = new RelayCommand(select);
    }

    public string Color { get; }

    public IRelayCommand SelectCommand { get; }
}
