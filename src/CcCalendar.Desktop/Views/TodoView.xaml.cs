using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CcCalendar.Core.Todos;
using CcCalendar.Desktop.ViewModels;

namespace CcCalendar.Desktop.Views;

public partial class TodoView
{
    private Point dragStart;
    private FrameworkElement? dragSource;
    private bool dragStarted;

    public TodoView()
    {
        InitializeComponent();
    }

    public event Action<string>? TodoCreationRequested;

    public event Action<TodoItem>? TodoCompletionToggled;

    public event Action<TodoItem, TodoQuadrant>? TodoQuadrantChanged;

    public event Action<TodoItem, TodoStatus>? TodoStatusChanged;

    private void NewTodoKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            RequestCreate();
            e.Handled = true;
        }
    }

    private void NewTodoClick(object sender, RoutedEventArgs e) => RequestCreate();

    private void RequestCreate()
    {
        string title = NewTodoBox.Text.Trim();
        if (title.Length == 0)
        {
            NewTodoBox.Focus();
            return;
        }

        TodoCreationRequested?.Invoke(title);
        NewTodoBox.Clear();
        NewTodoBox.Focus();
    }

    private void TodoCardMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // 拖动结束后的松开不应再切换完成状态。
        if (!dragStarted && sender is FrameworkElement { DataContext: TodoItem item })
        {
            TodoCompletionToggled?.Invoke(item);
        }
    }

    private void TodoCardPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        dragStart = e.GetPosition(this);
        dragSource = sender as FrameworkElement;
        dragStarted = false;
    }

    // 拖动启动必须挂在视图根元素上：MouseMove 只会派发给光标下方的元素，
    // 挂在卡片上时，鼠标快速移出卡片后就再也收不到移动事件，DoDragDrop
    // 永远不会启动（表现为"四象限无法拖动"）。根元素的隧道事件保证任何
    // 位置的移动都能触发拖动判定。
    private void RootPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed
            || dragSource is not FrameworkElement { DataContext: TodoItem item })
        {
            return;
        }

        Point current = e.GetPosition(this);

        if (Math.Abs(current.X - dragStart.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(current.Y - dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        FrameworkElement source = dragSource;
        dragSource = null;
        dragStarted = true;
        DragDrop.DoDragDrop(source, item, DragDropEffects.Move);
    }

    private void RootPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // 松开时清理按下状态，防止后续无按键移动误判为拖动。
        dragSource = null;
        dragStarted = false;
    }

    private void TodoDragOver(object sender, DragEventArgs e)
    {
        // 不处理 DragOver 时 WPF 默认效果为"禁止"，Drop 永远不会触发。
        if (e.Data.GetDataPresent(typeof(TodoItem)))
        {
            e.Effects = DragDropEffects.Move;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }

        e.Handled = true;
    }

    private void TodoGiveFeedback(object sender, GiveFeedbackEventArgs e)
    {
        // 使用系统移动光标，避免源元素同时处理 MouseMove 干扰拖拽。
        e.UseDefaultCursors = true;
        e.Handled = true;
    }

    private void QuadrantDrop(object sender, DragEventArgs e)
    {
        if (DataContext is TodoWorkspaceViewModel workspace
            && sender is FrameworkElement { DataContext: TodoQuadrantGroupViewModel group }
            && e.Data.GetData(typeof(TodoItem)) is TodoItem item)
        {
            workspace.MoveToQuadrant(item, group.Quadrant);
            TodoQuadrantChanged?.Invoke(item, group.Quadrant);
        }
    }

    private void BoardColumnDrop(object sender, DragEventArgs e)
    {
        if (DataContext is TodoWorkspaceViewModel workspace
            && sender is FrameworkElement { DataContext: KanbanColumnViewModel column }
            && e.Data.GetData(typeof(TodoItem)) is TodoItem item)
        {
            workspace.MoveToStatus(item, column.Status);
            TodoStatusChanged?.Invoke(item, column.Status);
        }
    }

    private void MoveToQuadrantClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: TodoQuadrant quadrant, Parent: ContextMenu menu }
            && menu.PlacementTarget is FrameworkElement { DataContext: TodoItem item }
            && DataContext is TodoWorkspaceViewModel workspace)
        {
            workspace.MoveToQuadrant(item, quadrant);
            TodoQuadrantChanged?.Invoke(item, quadrant);
        }
    }
}
