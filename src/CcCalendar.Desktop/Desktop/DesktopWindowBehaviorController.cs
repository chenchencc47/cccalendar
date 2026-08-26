using System.Windows;
using CcCalendar.Core.Configuration;

namespace CcCalendar.Desktop.Desktop;

public sealed class DesktopWindowBehaviorController
{
    private const double EdgeThreshold = 12;
    private const double RevealSize = 4;

    private readonly DesktopWindowBehaviorState state;
    private readonly Window window;
    private DesktopEdge attachedEdge;
    private DesktopWindowPosition shownPosition;
    private bool isHiddenAtEdge;

    public DesktopWindowBehaviorController(
        Window window,
        DesktopWindowBehaviorSettings settings)
    {
        ArgumentNullException.ThrowIfNull(window);
        this.window = window;
        state = new DesktopWindowBehaviorState(settings);
        window.SourceInitialized += (_, _) => ApplyNativeBehavior();
        window.IsVisibleChanged += (_, _) =>
        {
            if (window.IsVisible)
            {
                window.Dispatcher.BeginInvoke(ApplyNativeBehavior);
            }
        };
        window.MouseEnter += (_, _) => RevealFromEdge();
        window.MouseLeave += (_, _) => HideAtEdge();
        ApplyManagedBehavior();
    }

    public DesktopWindowBehaviorSettings Settings => state.ToSettings();

    public bool CanMove => !Settings.IsPositionLocked;

    public void TogglePositionLock()
    {
        state.TogglePositionLock();
        ApplyManagedBehavior();
    }

    public void ToggleMousePassthrough()
    {
        state.ToggleMousePassthrough();
        DesktopWindowNativeBehavior.ApplyMousePassthrough(
            window,
            Settings.IsMousePassthrough);
    }

    public void DisableMousePassthrough()
    {
        state.DisableMousePassthrough();
        DesktopWindowNativeBehavior.ApplyMousePassthrough(window, enabled: false);
    }

    public void SetLayer(DesktopWindowLayer layer)
    {
        state.SetLayer(layer);
        DesktopWindowNativeBehavior.ApplyLayer(window, layer);
    }

    public void ToggleEdgeAutoHide()
    {
        state.ToggleEdgeAutoHide();
        if (Settings.IsEdgeAutoHideEnabled)
        {
            AttachToCurrentEdge();
            HideAtEdge();
        }
        else
        {
            RevealFromEdge();
            attachedEdge = DesktopEdge.None;
        }
    }

    public void HandleMoveCompleted()
    {
        if (Settings.IsEdgeAutoHideEnabled)
        {
            AttachToCurrentEdge();
        }
    }

    public void ReapplyNativeBehavior()
    {
        ApplyNativeBehavior();
    }

    public void InitializeEdgeAutoHide()
    {
        if (!Settings.IsEdgeAutoHideEnabled)
        {
            return;
        }

        AttachToCurrentEdge();
        HideAtEdge();
    }

    private void ApplyManagedBehavior()
    {
        window.ResizeMode = Settings.IsPositionLocked
            ? ResizeMode.NoResize
            : ResizeMode.CanResizeWithGrip;
    }

    private void ApplyNativeBehavior()
    {
        DesktopWindowNativeBehavior.ApplyMousePassthrough(
            window,
            Settings.IsMousePassthrough);
        DesktopWindowNativeBehavior.ApplyLayer(window, Settings.Layer);
    }

    private void AttachToCurrentEdge()
    {
        if (isHiddenAtEdge)
        {
            return;
        }

        DesktopWindowBounds bounds = GetWindowBounds();
        DesktopEdge edge = DesktopEdgeHideLayout.FindEdge(
            bounds,
            GetWorkAreaBounds(),
            EdgeThreshold);
        attachedEdge = edge;
        if (edge != DesktopEdge.None)
        {
            shownPosition = new DesktopWindowPosition(bounds.Left, bounds.Top);
        }
    }

    private void HideAtEdge()
    {
        if (!Settings.IsEdgeAutoHideEnabled
            || attachedEdge == DesktopEdge.None
            || isHiddenAtEdge)
        {
            return;
        }

        DesktopWindowPosition hiddenPosition = DesktopEdgeHideLayout.GetHiddenPosition(
            attachedEdge,
            GetWindowBounds(),
            GetWorkAreaBounds(),
            RevealSize);
        SetPosition(hiddenPosition);
        isHiddenAtEdge = true;
    }

    private void RevealFromEdge()
    {
        if (!isHiddenAtEdge)
        {
            return;
        }

        SetPosition(shownPosition);
        isHiddenAtEdge = false;
    }

    private DesktopWindowBounds GetWindowBounds()
    {
        return new DesktopWindowBounds(window.Left, window.Top, window.Width, window.Height);
    }

    private static DesktopWindowBounds GetWorkAreaBounds()
    {
        Rect workArea = SystemParameters.WorkArea;
        return new DesktopWindowBounds(
            workArea.Left,
            workArea.Top,
            workArea.Width,
            workArea.Height);
    }

    private void SetPosition(DesktopWindowPosition position)
    {
        window.Left = position.Left;
        window.Top = position.Top;
    }
}
