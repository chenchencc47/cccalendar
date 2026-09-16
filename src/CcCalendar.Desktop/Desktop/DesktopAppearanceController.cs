using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using CcCalendar.Core.Configuration;
using Microsoft.Win32;

namespace CcCalendar.Desktop.Desktop;

public sealed class DesktopAppearanceController
{
    private readonly Border chrome;
    private readonly Window window;
    private DesktopAppearanceSettings settings;

    public DesktopAppearanceController(
        Window window,
        Border chrome,
        DesktopAppearanceSettings settings)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(chrome);
        ArgumentNullException.ThrowIfNull(settings);
        this.window = window;
        this.chrome = chrome;
        this.settings = settings;
        window.SourceInitialized += (_, _) => ApplyMaterial();
        Apply(settings);
    }

    public DesktopAppearanceSettings Settings => settings;

    public void Apply(DesktopAppearanceSettings value)
    {
        ArgumentNullException.ThrowIfNull(value);
        settings = value;
        DesktopAppearanceSettings palette = ResolvePalette(value);
        Color background = ParseColor(palette.BackgroundColor);

        // AllowsTransparency 分层窗口中 alpha=0 的像素会穿透鼠标点击（日历除今日底色外
        // 都无法选中/双击新增），因此表面保留最低 1/255 的不透明度保证整个窗口可交互。
        window.Resources["DesktopSurfaceBrush"] = CreateBrush(
            background,
            Math.Max(value.Opacity, 1.0 / 255));
        window.Resources["DesktopPrimaryTextBrush"] = CreateBrush(ParseColor(palette.PrimaryTextColor));
        window.Resources["DesktopSecondaryTextBrush"] = CreateBrush(ParseColor(palette.SecondaryTextColor));
        window.Resources["DesktopDateTextBrush"] = CreateBrush(ParseColor(palette.DateTextColor));
        window.Resources["DesktopLunarTextBrush"] = CreateBrush(ParseColor(palette.LunarTextColor));
        window.Resources["DesktopWeekendTextBrush"] = CreateBrush(ParseColor(palette.WeekendTextColor));
        window.Resources["DesktopHolidayTextBrush"] = CreateBrush(ParseColor(palette.HolidayTextColor));
        window.Resources["DesktopDayOffBackgroundBrush"] = CreateBrush(ParseColor(palette.DayOffBackgroundColor));
        window.Resources["DesktopDayOffTextBrush"] = CreateBrush(ParseColor(palette.DayOffTextColor));
        window.Resources["DesktopWorkdayBackgroundBrush"] = CreateBrush(ParseColor(palette.WorkdayBackgroundColor));
        window.Resources["DesktopWorkdayTextBrush"] = CreateBrush(ParseColor(palette.WorkdayTextColor));
        window.Resources["DesktopTodayBackgroundBrush"] = CreateBrush(ParseColor(palette.TodayBackgroundColor));
        window.Resources["DesktopTodayTextBrush"] = CreateBrush(ParseColor(palette.TodayTextColor));
        window.Resources["DesktopSeparatorBrush"] = value.ShowDateSeparators
            ? CreateBrush(ParseColor(palette.SeparatorColor))
            : Brushes.Transparent;
        window.Resources["DesktopContentPadding"] = GetContentPadding(value.Density);
        window.Resources["DesktopOtherMonthOpacity"] = value.HighlightCurrentMonth ? 0.45 : 1;
        window.Resources["UiBodyFontSize"] = value.FontSize * value.Scale;
        window.Resources["UiControlHeight"] = 32 * value.Scale;
        window.Resources["UiCompactControlHeight"] = 28 * value.Scale;
        // 桌面组件的紧凑文字（月历格日期角标、农历、日程条）此前是固定 9/10px，
        // 不跟随「字体大小 / 界面缩放」——只有正文标题会变。改为按比例派生：
        // 既保留紧凑表面的层级（角标 < 农历 < 正文），又让设置对所有文字生效。
        window.Resources["DesktopCaptionFontSize"] = DesktopFontScale.Caption(value.FontSize, value.Scale);
        window.Resources["DesktopMicroFontSize"] = DesktopFontScale.Micro(value.FontSize, value.Scale);
        window.Resources["UiTextEffect"] = CreateTextEffect(value.ShowTextOutline, palette);
        window.Resources["SurfaceBrush"] = window.Resources["DesktopSurfaceBrush"];
        window.Resources["TextPrimaryBrush"] = window.Resources["DesktopPrimaryTextBrush"];
        window.Resources["TextSecondaryBrush"] = window.Resources["DesktopSecondaryTextBrush"];
        window.Resources["BorderBrush"] = window.Resources["DesktopSeparatorBrush"];
        window.Resources["AccentBrush"] = window.Resources["DesktopTodayBackgroundBrush"];
        window.Resources["AccentSubtleBrush"] = window.Resources["DesktopDayOffBackgroundBrush"];
        window.Resources["HoverBrush"] = window.Resources["DesktopDayOffBackgroundBrush"];

        chrome.CornerRadius = new CornerRadius(value.CornerRadius);
        window.FontFamily = new FontFamily(value.FontFamily);
        window.FontSize = value.FontSize * value.Scale;
        window.FontWeight = value.IsTextBold ? FontWeights.SemiBold : FontWeights.Normal;
        ApplyMaterial();
    }

    private void ApplyMaterial()
    {
        DesktopAppearanceSettings palette = ResolvePalette(settings);
        DesktopWindowMaterial.Apply(
            new WindowInteropHelper(window),
            settings.Material,
            ParseColor(palette.BackgroundColor),
            settings.Opacity);
    }

    private static DesktopAppearanceSettings ResolvePalette(DesktopAppearanceSettings settings)
    {
        if (settings.UseCustomColors)
        {
            return settings;
        }

        ThemePreference resolvedTheme = settings.Theme == ThemePreference.System
            ? (IsSystemDarkTheme() ? ThemePreference.Dark : ThemePreference.Light)
            : settings.Theme;
        return DesktopAppearanceDefaults.ForTheme(resolvedTheme);
    }

    private static bool IsSystemDarkTheme()
    {
        object? value = Registry.GetValue(
            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "AppsUseLightTheme",
            1);
        return value is int lightTheme && lightTheme == 0;
    }

    private static Thickness GetContentPadding(DesktopDensity density)
    {
        return density switch
        {
            DesktopDensity.Compact => new Thickness(10),
            DesktopDensity.Spacious => new Thickness(18),
            _ => new Thickness(14),
        };
    }

    private static Color ParseColor(string value)
    {
        return (Color)ColorConverter.ConvertFromString(value);
    }

    private static SolidColorBrush CreateBrush(Color color, double opacity = 1)
    {
        color.A = (byte)Math.Round(Math.Clamp(opacity, 0, 1) * byte.MaxValue);
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static DropShadowEffect CreateTextEffect(
        bool isEnabled,
        DesktopAppearanceSettings palette)
    {
        Color background = ParseColor(palette.BackgroundColor);
        bool isDarkBackground = background.R + background.G + background.B < 384;
        return new DropShadowEffect
        {
            BlurRadius = 2,
            Color = isDarkBackground ? Colors.Black : Colors.White,
            Direction = 0,
            Opacity = isEnabled ? 0.9 : 0,
            ShadowDepth = 0,
        };
    }
}
