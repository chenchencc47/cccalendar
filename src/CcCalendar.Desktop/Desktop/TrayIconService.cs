using System.Drawing;
using System.Windows.Forms;
using CcCalendar.Core.Configuration;

namespace CcCalendar.Desktop.Desktop;

public sealed class TrayIconService : IDisposable
{
    private readonly ContextMenuStrip menu;
    private readonly NotifyIcon notifyIcon;
    private readonly Icon applicationIcon;
    private bool isDisposed;

    public TrayIconService(
        Action showMainWindow,
        Action toggleQuickPanel,
        Action toggleDesktopCalendar,
        Action toggleDesktopAgenda,
        Action toggleDesktopTodo,
        IReadOnlyList<DesktopBehaviorMenuTarget> behaviorTargets,
        Action disableAllMousePassthrough,
        Action exitApplication)
    {
        ArgumentNullException.ThrowIfNull(showMainWindow);
        ArgumentNullException.ThrowIfNull(toggleQuickPanel);
        ArgumentNullException.ThrowIfNull(toggleDesktopCalendar);
        ArgumentNullException.ThrowIfNull(toggleDesktopAgenda);
        ArgumentNullException.ThrowIfNull(toggleDesktopTodo);
        ArgumentNullException.ThrowIfNull(behaviorTargets);
        ArgumentNullException.ThrowIfNull(disableAllMousePassthrough);
        ArgumentNullException.ThrowIfNull(exitApplication);

        menu = new ContextMenuStrip();
        menu.Items.Add("打开 cccalendar", null, (_, _) => showMainWindow());
        menu.Items.Add("快速面板", null, (_, _) => toggleQuickPanel());
        menu.Items.Add(
            "恢复所有桌面组件鼠标操作（Ctrl+Alt+Shift+P）",
            null,
            (_, _) => disableAllMousePassthrough());
        var componentMenu = new ToolStripMenuItem("桌面组件");
        componentMenu.DropDownItems.Add("桌面日历", null, (_, _) => toggleDesktopCalendar());
        componentMenu.DropDownItems.Add("桌面日程", null, (_, _) => toggleDesktopAgenda());
        componentMenu.DropDownItems.Add("桌面待办", null, (_, _) => toggleDesktopTodo());
        menu.Items.Add(componentMenu);
        var behaviorMenu = new ToolStripMenuItem("桌面行为");
        foreach (DesktopBehaviorMenuTarget target in behaviorTargets)
        {
            behaviorMenu.DropDownItems.Add(CreateBehaviorMenu(target));
        }

        behaviorMenu.DropDownItems.Add(new ToolStripSeparator());
        behaviorMenu.DropDownItems.Add(
            "关闭全部鼠标穿透",
            null,
            (_, _) => disableAllMousePassthrough());
        menu.Items.Add(behaviorMenu);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => exitApplication());
        applicationIcon = ApplicationIconLoader.Load(SystemInformation.SmallIconSize.Width);
        notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = menu,
            Icon = applicationIcon,
            Text = "cccalendar",
            Visible = true,
        };
        notifyIcon.MouseUp += (_, args) =>
        {
            if (args.Button == MouseButtons.Left)
            {
                toggleQuickPanel();
            }
        };
        notifyIcon.DoubleClick += (_, _) => showMainWindow();
        notifyIcon.BalloonTipClicked += (_, _) => showMainWindow();
    }

    public void ShowNotification(string title, string message)
    {
        if (isDisposed)
        {
            return;
        }

        notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
        notifyIcon.BalloonTipTitle = title;
        notifyIcon.BalloonTipText = message;
        notifyIcon.ShowBalloonTip(5000);
    }

    public void Hide()
    {
        if (!isDisposed)
        {
            notifyIcon.Visible = false;
        }
    }

    private static ToolStripMenuItem CreateBehaviorMenu(DesktopBehaviorMenuTarget target)
    {
        var targetMenu = new ToolStripMenuItem(target.Title);
        var positionLock = new ToolStripMenuItem("锁定位置");
        var mousePassthrough = new ToolStripMenuItem("鼠标穿透");
        var edgeAutoHide = new ToolStripMenuItem("贴边隐藏");
        var layerMenu = new ToolStripMenuItem("窗口层级");
        var desktopLayer = new ToolStripMenuItem("桌面底层");
        var normalLayer = new ToolStripMenuItem("普通");
        var topmostLayer = new ToolStripMenuItem("始终置顶");

        positionLock.Click += (_, _) => target.Target.TogglePositionLock();
        mousePassthrough.Click += (_, _) => target.Target.ToggleMousePassthrough();
        edgeAutoHide.Click += (_, _) => target.Target.ToggleEdgeAutoHide();
        desktopLayer.Click += (_, _) => target.Target.SetLayer(DesktopWindowLayer.Desktop);
        normalLayer.Click += (_, _) => target.Target.SetLayer(DesktopWindowLayer.Normal);
        topmostLayer.Click += (_, _) => target.Target.SetLayer(DesktopWindowLayer.Topmost);

        layerMenu.DropDownItems.Add(desktopLayer);
        layerMenu.DropDownItems.Add(normalLayer);
        layerMenu.DropDownItems.Add(topmostLayer);
        targetMenu.DropDownItems.Add(positionLock);
        targetMenu.DropDownItems.Add(mousePassthrough);
        targetMenu.DropDownItems.Add(layerMenu);
        targetMenu.DropDownItems.Add(edgeAutoHide);
        targetMenu.DropDownOpening += (_, _) =>
        {
            DesktopWindowBehaviorSettings settings = target.Target.BehaviorSettings;
            positionLock.Checked = settings.IsPositionLocked;
            mousePassthrough.Checked = settings.IsMousePassthrough;
            edgeAutoHide.Checked = settings.IsEdgeAutoHideEnabled;
            desktopLayer.Checked = settings.Layer == DesktopWindowLayer.Desktop;
            normalLayer.Checked = settings.Layer == DesktopWindowLayer.Normal;
            topmostLayer.Checked = settings.Layer == DesktopWindowLayer.Topmost;
        };
        return targetMenu;
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        notifyIcon.Visible = false;
        notifyIcon.Dispose();
        applicationIcon.Dispose();
        menu.Dispose();
    }
}
