using CcCalendar.Core.Configuration;
using CcCalendar.Desktop.Desktop;

namespace CcCalendar.Desktop.Tests.Desktop;

public sealed class DesktopAppearanceSettingsStateTests
{
    [Fact]
    public void SelectedTargetUpdatesIndependentlyAndNormalizesInputs()
    {
        var workbench = new DesktopAppearanceSettings();
        var calendar = new DesktopAppearanceSettings { Theme = ThemePreference.Dark };
        var state = new DesktopAppearanceSettingsState(
            workbench,
            calendar,
            new DesktopAppearanceSettings(),
            new DesktopAppearanceSettings());

        state.SelectTarget(DesktopAppearanceTarget.Calendar);
        state.SetOpacity(0);
        state.SetCornerRadius(40);
        state.SetScale(1.2);
        state.SetMaterial(DesktopBackgroundMaterial.Acrylic);
        bool accepted = state.SetColor(DesktopColorRole.HolidayText, "#d93330");
        bool rejected = state.SetColor(DesktopColorRole.HolidayText, "red");

        Assert.True(accepted);
        Assert.False(rejected);
        Assert.Equal(workbench, state.GetSettings(DesktopAppearanceTarget.Workbench));
        Assert.Equal(
            calendar with
            {
                Opacity = 0,
                CornerRadius = 24,
                Scale = 1.2,
                Material = DesktopBackgroundMaterial.Acrylic,
                HolidayTextColor = "#D93330",
                UseCustomColors = true,
            },
            state.GetSettings(DesktopAppearanceTarget.Calendar));
    }

    [Fact]
    public void DarkDefaultsUseRestrainedProductivityPalette()
    {
        DesktopAppearanceSettings settings = DesktopAppearanceDefaults.ForTheme(
            ThemePreference.Dark);

        Assert.Equal("#22252A", settings.BackgroundColor);
        Assert.Equal("#F0F2F4", settings.DateTextColor);
        Assert.Equal("#383D45", settings.SeparatorColor);
        Assert.False(settings.UseCustomColors);
    }
}
