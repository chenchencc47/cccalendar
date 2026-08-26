using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using System.Windows;
using CcCalendar.Desktop.Converters;

namespace CcCalendar.Desktop.Tests.Views;

/// <summary>
/// WPF 运行时测试的共享宿主：整个测试程序集只创建一个 STA 线程和一个
/// Application 实例（WPF 限制每个 AppDomain 仅允许一个 Application），
/// 所有 UI 运行时测试的动作在该线程上串行执行，避免 xUnit 并行运行
/// 不同测试类时争抢 Application 构造函数。
/// </summary>
internal static class WpfRuntimeHost
{
    private static readonly BlockingCollection<Action> Work = [];
    private static readonly object Gate = new();
    private static Thread? hostThread;

    public static void Run(Action action)
    {
        lock (Gate)
        {
            if (hostThread is null)
            {
                hostThread = new Thread(Loop)
                {
                    Name = "WpfRuntimeHost",
                    IsBackground = true,
                };
                hostThread.SetApartmentState(ApartmentState.STA);
                hostThread.Start();
            }
        }

        Exception? error = null;
        using var done = new ManualResetEventSlim(false);
        Work.Add(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                error = exception;
            }
            finally
            {
                done.Set();
            }
        });
        done.Wait();

        if (error is not null)
        {
            ExceptionDispatchInfo.Capture(error).Throw();
        }
    }

    private static void Loop()
    {
        Application app = new() { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        if (app.Resources.MergedDictionaries.Count == 0)
        {
            // 与 App.xaml 等价：先合并主题字典，再注册转换器。
            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/cccalendar;component/Themes/Theme.xaml"),
            });
            app.Resources["EnumEqualsConverter"] = new EnumEqualsConverter();
            app.Resources["ColorStringToBrushConverter"] = new ColorStringToBrushConverter();
        }

        foreach (Action work in Work.GetConsumingEnumerable())
        {
            work();
        }
    }
}
