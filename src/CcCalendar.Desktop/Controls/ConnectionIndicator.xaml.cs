using System.Windows;
using System.Windows.Controls;

namespace CcCalendar.Desktop.Controls;

/// <summary>
/// 连接状态指示器（P61-03）。把「团队连接」这类持续状态表达为「状态点 + 文案」，
/// 顶点用颜色与呼吸表达状态，文案给出来源与结果。
///
/// 三种状态（优先级从高到低）：
/// 1. <see cref="IsBusy"/> —— 正在连接，强调色 + 呼吸；
/// 2. <see cref="IsConnected"/> —— 已连接，成功色静点；
/// 3. 其余 —— 未连接，次要文字色静点。
/// </summary>
public partial class ConnectionIndicator : UserControl
{
    public static readonly DependencyProperty IsConnectedProperty =
        DependencyProperty.Register(
            nameof(IsConnected),
            typeof(bool),
            typeof(ConnectionIndicator),
            new PropertyMetadata(false, OnStateChanged));

    public static readonly DependencyProperty IsBusyProperty =
        DependencyProperty.Register(
            nameof(IsBusy),
            typeof(bool),
            typeof(ConnectionIndicator),
            new PropertyMetadata(false, OnStateChanged));

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text),
            typeof(string),
            typeof(ConnectionIndicator),
            new PropertyMetadata(string.Empty, OnTextChanged));

    /// <summary>已连接使用的令牌。</summary>
    public const string ConnectedBrushKey = "SuccessBrush";

    /// <summary>连接中使用的令牌。</summary>
    public const string BusyBrushKey = "AccentBrush";

    /// <summary>未连接使用的令牌。</summary>
    public const string DisconnectedBrushKey = "TextSecondaryBrush";

    public ConnectionIndicator()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplyState();
    }

    /// <summary>是否已连接。</summary>
    public bool IsConnected
    {
        get => (bool)GetValue(IsConnectedProperty);
        set => SetValue(IsConnectedProperty, value);
    }

    /// <summary>是否正在连接。</summary>
    public bool IsBusy
    {
        get => (bool)GetValue(IsBusyProperty);
        set => SetValue(IsBusyProperty, value);
    }

    /// <summary>状态文案。</summary>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>当前生效的令牌名，供测试断言状态映射。</summary>
    public string CurrentBrushKey => Dot.StateBrushKey ?? DisconnectedBrushKey;

    private static void OnStateChanged(DependencyObject element, DependencyPropertyChangedEventArgs args)
        => ((ConnectionIndicator)element).ApplyState();

    private static void OnTextChanged(DependencyObject element, DependencyPropertyChangedEventArgs args)
        => ((ConnectionIndicator)element).Label.Text = (string)args.NewValue;

    private void ApplyState()
    {
        Dot.IsPulsing = IsBusy;
        Dot.StateBrushKey = ResolveBrushKey();
        Dot.ApplyBrush();
    }

    private string ResolveBrushKey()
    {
        if (IsBusy)
        {
            return BusyBrushKey;
        }

        return IsConnected ? ConnectedBrushKey : DisconnectedBrushKey;
    }
}
