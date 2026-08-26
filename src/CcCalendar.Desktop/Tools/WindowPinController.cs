using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CcCalendar.Desktop.Tools;

public sealed record WindowPinTarget(nint Handle, int ProcessId, string Title, bool IsTopmost);

public interface IWindowPinBackend
{
    IReadOnlyList<WindowPinTarget> Enumerate();

    void SetTopmost(nint handle, bool topmost);
}

public sealed class WindowPinController : ObservableObject
{
    private readonly IWindowPinBackend backend;
    private readonly int currentProcessId;
    private WindowPinTarget? selectedTarget;

    public WindowPinController(IWindowPinBackend backend, int currentProcessId)
    {
        this.backend = backend ?? throw new ArgumentNullException(nameof(backend));
        this.currentProcessId = currentProcessId;
        Targets = [];
        RefreshCommand = new RelayCommand(Refresh);
        ToggleCommand = new RelayCommand(ToggleSelected, () => SelectedTarget is not null);
    }

    public ObservableCollection<WindowPinTarget> Targets { get; }

    public WindowPinTarget? SelectedTarget
    {
        get => selectedTarget;
        set
        {
            if (SetProperty(ref selectedTarget, value))
            {
                OnPropertyChanged(nameof(ToggleActionText));
                ToggleCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string ToggleActionText => SelectedTarget?.IsTopmost == true ? "取消置顶" : "置顶";

    public IRelayCommand RefreshCommand { get; }

    public IRelayCommand ToggleCommand { get; }

    public void Refresh()
    {
        WindowPinTarget[] targets = [.. backend.Enumerate()
            .Where(target => target.ProcessId != currentProcessId)
            .Where(target => !string.IsNullOrWhiteSpace(target.Title))
            .OrderBy(target => target.Title, StringComparer.CurrentCultureIgnoreCase)];
        Targets.Clear();
        foreach (WindowPinTarget target in targets)
        {
            Targets.Add(target);
        }

        SelectedTarget = Targets.FirstOrDefault();
    }

    public void ToggleSelected()
    {
        if (SelectedTarget is null)
        {
            return;
        }

        bool topmost = !SelectedTarget.IsTopmost;
        backend.SetTopmost(SelectedTarget.Handle, topmost);
        int index = Targets.IndexOf(SelectedTarget);
        WindowPinTarget updated = SelectedTarget with { IsTopmost = topmost };
        Targets[index] = updated;
        SelectedTarget = updated;
    }
}

public sealed class WindowsWindowPinBackend : IWindowPinBackend
{
    private const int ExtendedStyleIndex = -20;
    private const long TopmostStyle = 0x00000008L;
    private static readonly nint Topmost = new(-1);
    private static readonly nint NotTopmost = new(-2);
    private const uint NoMove = 0x0002;
    private const uint NoSize = 0x0001;
    private const uint NoActivate = 0x0010;

    public IReadOnlyList<WindowPinTarget> Enumerate()
    {
        var targets = new List<WindowPinTarget>();
        EnumWindows((handle, _) =>
        {
            if (!IsWindowVisible(handle))
            {
                return true;
            }

            int length = GetWindowTextLength(handle);
            if (length == 0)
            {
                return true;
            }

            var titleBuffer = new char[length + 1];
            int copied = GetWindowText(handle, titleBuffer, titleBuffer.Length);
            uint threadId = GetWindowThreadProcessId(handle, out uint processId);
            if (copied <= 0 || threadId == 0)
            {
                return true;
            }

            bool isTopmost = (GetWindowLongPtr(handle, ExtendedStyleIndex).ToInt64() & TopmostStyle) != 0;
            targets.Add(new WindowPinTarget(
                handle,
                (int)processId,
                new string(titleBuffer, 0, copied),
                isTopmost));
            return true;
        }, nint.Zero);
        return targets;
    }

    public void SetTopmost(nint handle, bool topmost)
    {
        if (!SetWindowPos(
            handle,
            topmost ? Topmost : NotTopmost,
            0,
            0,
            0,
            0,
            NoMove | NoSize | NoActivate))
        {
            throw new InvalidOperationException("无法更新窗口置顶状态。");
        }
    }

    private delegate bool EnumWindowsCallback(nint handle, nint parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsCallback callback, nint parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint handle);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(
        nint handle,
        [Out] char[] text,
        int maximumCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(nint handle);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint handle, out uint processId);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(nint handle, int index);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        nint handle,
        nint insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);
}
