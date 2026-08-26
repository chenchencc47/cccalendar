using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CcCalendar.Core.Configuration;
using CcCalendar.Desktop.Desktop;

namespace CcCalendar.Desktop.Tests.Desktop;

/// <summary>
/// 桌面外观控制器测试：AllowsTransparency 分层窗口中 alpha=0 的像素会穿透鼠标
/// 点击（仅不透明内容如文字/今日底色可点）。当用户把不透明度设为 0 时，
/// 表面画笔必须保留最低 1/255 的 alpha，保证整个窗口区域仍然可以接收点击
/// （否则日历除今天外都无法选中/双击新增）。
/// </summary>
public sealed class DesktopAppearanceControllerTests
{
    [Fact]
    public void SurfaceBrushKeepsMinimumAlphaWhenOpacityIsZero()
    {
        RunOnSta(() =>
        {
            var window = new Window();
            var chrome = new Border();
            try
            {
                var controller = new DesktopAppearanceController(
                    window,
                    chrome,
                    new DesktopAppearanceSettings { Opacity = 0 });

                var brush = Assert.IsType<SolidColorBrush>(window.Resources["DesktopSurfaceBrush"]);
                Assert.True(brush.Color.A >= 1, "opacity=0 时表面画笔 alpha 必须至少为 1，否则分层窗口像素会穿透鼠标点击。");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void SurfaceBrushMapsNormalOpacityToAlpha()
    {
        RunOnSta(() =>
        {
            var window = new Window();
            var chrome = new Border();
            try
            {
                var controller = new DesktopAppearanceController(
                    window,
                    chrome,
                    new DesktopAppearanceSettings { Opacity = 0.5 });

                var brush = Assert.IsType<SolidColorBrush>(window.Resources["DesktopSurfaceBrush"]);
                Assert.Equal(128, brush.Color.A);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static void RunOnSta(Action action)
    {
        Exception? captured = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                captured = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        Assert.Null(captured is null ? null : captured);
    }
}
