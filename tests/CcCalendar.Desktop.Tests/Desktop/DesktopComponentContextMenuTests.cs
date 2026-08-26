using System.Windows;
using System.Windows.Controls;
using CcCalendar.Desktop.Desktop;

namespace CcCalendar.Desktop.Tests.Desktop;

public sealed class DesktopComponentContextMenuTests
{
    [Fact]
    public void RefreshVisibilityChecksSkipsSeparators()
    {
        Exception? failure = null;
        bool calendarChecked = false;
        bool agendaChecked = true;
        var thread = new Thread(() =>
        {
            try
            {
                var calendar = new MenuItem
                {
                    Tag = DesktopComponentKind.Calendar,
                    IsCheckable = true,
                };
                var agenda = new MenuItem
                {
                    Tag = DesktopComponentKind.Agenda,
                    IsCheckable = true,
                };
                object[] items = [calendar, agenda, new Separator()];
                var actions = new DesktopComponentVisibilityActions(
                    _ => { },
                    kind => kind == DesktopComponentKind.Calendar);

                DesktopComponentContextMenu.RefreshVisibilityChecks(items, actions);
                calendarChecked = calendar.IsChecked;
                agendaChecked = agenda.IsChecked;
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(failure);
        Assert.True(calendarChecked);
        Assert.False(agendaChecked);
    }

    [Fact]
    public void AttachAddsMousePassthroughToggleAndRefreshesItsCheck()
    {
        Exception? failure = null;
        bool toggled = false;
        bool enabled = true;
        var thread = new Thread(() =>
        {
            try
            {
                var target = new Border();
                DesktopComponentContextMenu.Attach(
                    target,
                    DesktopComponentKind.Agenda,
                    new DesktopComponentVisibilityActions(_ => { }, _ => true),
                    () => { },
                    isMousePassthroughEnabled: () => enabled,
                    toggleMousePassthrough: () => toggled = true);

                var menu = Assert.IsType<ContextMenu>(target.ContextMenu);
                MenuItem passthrough = Assert.Single(
                    menu.Items.OfType<MenuItem>(),
                    item => Equals(item.Header, "鼠标穿透"));
                menu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent));
                Assert.True(passthrough.IsChecked);
                passthrough.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Assert.True(toggled);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(failure);
    }
}
