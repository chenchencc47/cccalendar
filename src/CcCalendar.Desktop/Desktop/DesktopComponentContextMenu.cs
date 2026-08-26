using System.Collections;
using System.Windows;
using System.Windows.Controls;

namespace CcCalendar.Desktop.Desktop;

public static class DesktopComponentContextMenu
{
    public static void Attach(
        FrameworkElement target,
        DesktopComponentKind currentKind,
        DesktopComponentVisibilityActions actions,
        Action editSize,
        Func<bool>? isTopCenterPinned = null,
        Action? toggleTopCenterPinned = null,
        Func<bool>? isAutoHideEnabled = null,
        Action? toggleAutoHide = null,
        Action? quickCreateRequested = null,
        string? quickCreateHeader = null,
        Func<bool>? isMousePassthroughEnabled = null,
        Action? toggleMousePassthrough = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(actions);
        var menu = new ContextMenu();
        if (quickCreateRequested is not null)
        {
            var createItem = new MenuItem { Header = quickCreateHeader ?? "新增..." };
            createItem.Click += (_, _) => quickCreateRequested();
            menu.Items.Add(createItem);
            menu.Items.Add(new Separator());
        }

        AddItem(menu, "显示日历", DesktopComponentKind.Calendar, actions);
        AddItem(menu, "显示日程", DesktopComponentKind.Agenda, actions);
        AddItem(menu, "显示待办", DesktopComponentKind.Todo, actions);
        menu.Items.Add(new Separator());
        MenuItem? topCenterItem = null;
        MenuItem? autoHideItem = null;
        if (currentKind == DesktopComponentKind.Calendar)
        {
            topCenterItem = new MenuItem { Header = "固定在顶部中央", IsCheckable = true };
            topCenterItem.Click += (_, _) => toggleTopCenterPinned?.Invoke();
            menu.Items.Add(topCenterItem);
            autoHideItem = new MenuItem { Header = "顶部自动隐藏", IsCheckable = true };
            autoHideItem.Click += (_, _) => toggleAutoHide?.Invoke();
            menu.Items.Add(autoHideItem);
        }

        var sizeItem = new MenuItem { Header = "设置宽高..." };
        sizeItem.Click += (_, _) => editSize();
        menu.Items.Add(sizeItem);
        MenuItem? mousePassthroughItem = null;
        if (isMousePassthroughEnabled is not null && toggleMousePassthrough is not null)
        {
            mousePassthroughItem = new MenuItem
            {
                Header = "鼠标穿透",
                IsCheckable = true,
            };
            mousePassthroughItem.Click += (_, _) => toggleMousePassthrough();
            menu.Items.Add(mousePassthroughItem);
        }
        menu.Opened += (_, _) =>
        {
            RefreshVisibilityChecks(menu.Items, actions);

            if (topCenterItem is not null)
            {
                topCenterItem.IsChecked = isTopCenterPinned?.Invoke() == true;
            }

            if (autoHideItem is not null)
            {
                autoHideItem.IsChecked = isAutoHideEnabled?.Invoke() == true;
            }

            if (mousePassthroughItem is not null)
            {
                mousePassthroughItem.IsChecked = isMousePassthroughEnabled?.Invoke() == true;
            }
        };
        target.ContextMenu = menu;
        target.PreviewMouseRightButtonUp += (_, e) =>
        {
            menu.PlacementTarget = target;
            menu.IsOpen = true;
            e.Handled = true;
        };
    }

    public static void RefreshVisibilityChecks(
        IEnumerable items,
        DesktopComponentVisibilityActions actions)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(actions);
        foreach (MenuItem item in items.OfType<MenuItem>())
        {
            if (item.Tag is DesktopComponentKind kind)
            {
                item.IsChecked = actions.IsVisible(kind);
            }
        }
    }

    private static void AddItem(
        ContextMenu menu,
        string header,
        DesktopComponentKind kind,
        DesktopComponentVisibilityActions actions)
    {
        var item = new MenuItem { Header = header, IsCheckable = true, Tag = kind };
        item.Click += (_, _) => actions.Toggle(kind);
        menu.Items.Add(item);
    }
}
