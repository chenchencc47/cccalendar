using System.Globalization;
using CcCalendar.Core.Configuration;

namespace CcCalendar.Desktop.Desktop;

public sealed class DesktopAppearanceSettingsState
{
    private readonly Dictionary<DesktopAppearanceTarget, DesktopAppearanceSettings> settingsByTarget;

    public DesktopAppearanceSettingsState(
        DesktopAppearanceSettings workbench,
        DesktopAppearanceSettings calendar,
        DesktopAppearanceSettings agenda,
        DesktopAppearanceSettings todo)
    {
        settingsByTarget = new Dictionary<DesktopAppearanceTarget, DesktopAppearanceSettings>
        {
            [DesktopAppearanceTarget.Workbench] = workbench,
            [DesktopAppearanceTarget.Calendar] = calendar,
            [DesktopAppearanceTarget.Agenda] = agenda,
            [DesktopAppearanceTarget.Todo] = todo,
        };
    }

    public DesktopAppearanceTarget SelectedTarget { get; private set; }

    public DesktopAppearanceSettings Current => settingsByTarget[SelectedTarget];

    public void SelectTarget(DesktopAppearanceTarget target)
    {
        SelectedTarget = target;
    }

    public DesktopAppearanceSettings GetSettings(DesktopAppearanceTarget target)
    {
        return settingsByTarget[target];
    }

    public void Update(Func<DesktopAppearanceSettings, DesktopAppearanceSettings> update)
    {
        ArgumentNullException.ThrowIfNull(update);
        settingsByTarget[SelectedTarget] = update(Current);
    }

    public void SetOpacity(double opacity)
    {
        Update(settings => settings with { Opacity = Math.Clamp(opacity, 0, 1) });
    }

    public void SetCornerRadius(double cornerRadius)
    {
        Update(settings => settings with { CornerRadius = Math.Clamp(cornerRadius, 0, 24) });
    }

    public void SetScale(double scale)
    {
        Update(settings => settings with { Scale = Math.Clamp(scale, 0.8, 1.4) });
    }

    public void SetMaterial(DesktopBackgroundMaterial material)
    {
        Update(settings => settings with { Material = material });
    }

    public void SetTheme(ThemePreference theme)
    {
        DesktopAppearanceSettings current = Current;
        DesktopAppearanceSettings defaults = DesktopAppearanceDefaults.ForTheme(theme);
        settingsByTarget[SelectedTarget] = defaults with
        {
            Material = current.Material,
            Opacity = current.Opacity,
            CornerRadius = current.CornerRadius,
            Scale = current.Scale,
            FontFamily = current.FontFamily,
            FontSize = current.FontSize,
            IsTextBold = current.IsTextBold,
            Density = current.Density,
            ShowDateEvents = current.ShowDateEvents,
            ShowDetailedEvents = current.ShowDetailedEvents,
            ShowDateSeparators = current.ShowDateSeparators,
            HighlightCurrentMonth = current.HighlightCurrentMonth,
            ShowTextOutline = current.ShowTextOutline,
        };
    }

    public void RestoreDefaults()
    {
        settingsByTarget[SelectedTarget] = DesktopAppearanceDefaults.ForTheme(Current.Theme);
    }

    public bool SetColor(DesktopColorRole role, string value)
    {
        if (!TryNormalizeColor(value, out string normalized))
        {
            return false;
        }

        Update(settings => SetColor(settings, role, normalized));
        return true;
    }

    private static DesktopAppearanceSettings SetColor(
        DesktopAppearanceSettings settings,
        DesktopColorRole role,
        string value)
    {
        DesktopAppearanceSettings updated = role switch
        {
            DesktopColorRole.Background => settings with { BackgroundColor = value },
            DesktopColorRole.PrimaryText => settings with { PrimaryTextColor = value },
            DesktopColorRole.SecondaryText => settings with { SecondaryTextColor = value },
            DesktopColorRole.DateText => settings with { DateTextColor = value },
            DesktopColorRole.LunarText => settings with { LunarTextColor = value },
            DesktopColorRole.WeekendText => settings with { WeekendTextColor = value },
            DesktopColorRole.HolidayText => settings with { HolidayTextColor = value },
            DesktopColorRole.DayOffBackground => settings with { DayOffBackgroundColor = value },
            DesktopColorRole.DayOffText => settings with { DayOffTextColor = value },
            DesktopColorRole.WorkdayBackground => settings with { WorkdayBackgroundColor = value },
            DesktopColorRole.WorkdayText => settings with { WorkdayTextColor = value },
            DesktopColorRole.TodayBackground => settings with { TodayBackgroundColor = value },
            DesktopColorRole.TodayText => settings with { TodayTextColor = value },
            DesktopColorRole.Separator => settings with { SeparatorColor = value },
            _ => throw new ArgumentOutOfRangeException(nameof(role)),
        };
        return updated with { UseCustomColors = true };
    }

    private static bool TryNormalizeColor(string value, out string normalized)
    {
        normalized = string.Empty;
        if (value.Length != 7 || value[0] != '#')
        {
            return false;
        }

        if (!int.TryParse(value.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
        {
            return false;
        }

        normalized = value.ToUpperInvariant();
        return true;
    }
}
