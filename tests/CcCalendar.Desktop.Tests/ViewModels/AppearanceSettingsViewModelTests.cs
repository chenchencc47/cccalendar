using CcCalendar.Core.Configuration;
using CcCalendar.Desktop.Desktop;
using CcCalendar.Desktop.ViewModels;

namespace CcCalendar.Desktop.Tests.ViewModels;

public sealed class AppearanceSettingsViewModelTests
{
    [Fact]
    public void EditingSelectedTargetAppliesOnlyThatTargetsSettings()
    {
        var applied = new List<(DesktopAppearanceTarget Target, DesktopAppearanceSettings Settings)>();
        var viewModel = new AppearanceSettingsViewModel(
            new DesktopAppearanceSettings(),
            new DesktopAppearanceSettings(),
            new DesktopAppearanceSettings(),
            new DesktopAppearanceSettings(),
            (target, settings) => applied.Add((target, settings)));

        viewModel.SelectedTarget = DesktopAppearanceTarget.Calendar;
        viewModel.OpacityPercent = 60;
        viewModel.Material = DesktopBackgroundMaterial.Mica;

        Assert.All(applied, item => Assert.Equal(DesktopAppearanceTarget.Calendar, item.Target));
        Assert.Equal(DesktopBackgroundMaterial.Mica, applied[^1].Settings.Material);
        Assert.Equal(0.6, applied[^1].Settings.Opacity);
        Assert.Equal(
            new DesktopAppearanceSettings(),
            viewModel.GetSettings(DesktopAppearanceTarget.Workbench));
    }

    [Fact]
    public void HiddenStartupSettingUsesItsDedicatedCallback()
    {
        bool? updated = null;
        var viewModel = new AppearanceSettingsViewModel(
            new(),
            new(),
            new(),
            new(),
            startWithMainWindowHidden: false,
            startWithMainWindowHiddenChanged: value => updated = value);

        viewModel.StartWithMainWindowHidden = true;

        Assert.True(updated);
        Assert.True(viewModel.StartWithMainWindowHidden);
    }

    [Fact]
    public void OpacityPercentRoundsFractionalInputToWholePercent()
    {
        var viewModel = new AppearanceSettingsViewModel(
            new DesktopAppearanceSettings(),
            new DesktopAppearanceSettings(),
            new DesktopAppearanceSettings(),
            new DesktopAppearanceSettings());

        viewModel.OpacityPercent = 10.730593607306027;

        Assert.Equal(11, viewModel.OpacityPercent);
        Assert.Equal(0.11, viewModel.GetSettings(DesktopAppearanceTarget.Workbench).Opacity);
    }

    [Fact]
    public void ColorPaletteSelectionUpdatesTheSelectedColor()
    {
        var viewModel = new AppearanceSettingsViewModel(
            new DesktopAppearanceSettings(),
            new DesktopAppearanceSettings(),
            new DesktopAppearanceSettings(),
            new DesktopAppearanceSettings());

        AppearanceColorOptionViewModel option = viewModel.ColorOptions
            .Single(item => item.Role == DesktopColorRole.TodayBackground);
        option.TogglePaletteCommand.Execute(null);
        option.PaletteOptions.Single(item => item.Color == "#C43D4B").SelectCommand.Execute(null);

        Assert.Equal("#C43D4B", option.Value);
        Assert.False(option.IsPaletteOpen);

        option.Red = 1;
        option.Green = 128;
        option.Blue = 255;
        Assert.Equal("#0180FF", option.Value);
    }
}
