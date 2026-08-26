using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media;
using CcCalendar.Core.Configuration;

namespace CcCalendar.Desktop.Desktop;

internal static class DesktopWindowMaterial
{
    public static void Apply(
        WindowInteropHelper window,
        DesktopBackgroundMaterial material,
        Color background,
        double opacity)
    {
        if (window.Handle == nint.Zero)
        {
            return;
        }

        int backdropType = material switch
        {
            DesktopBackgroundMaterial.Mica => 2,
            DesktopBackgroundMaterial.Acrylic => 3,
            _ => 1,
        };
        int backdropResult = DwmSetWindowAttribute(
            window.Handle,
            SystemBackdropTypeAttribute,
            ref backdropType,
            Marshal.SizeOf<int>());
        bool hasSystemBackdrop = backdropResult == 0;

        var policy = new AccentPolicy
        {
            AccentState = material switch
            {
                DesktopBackgroundMaterial.Mica when !hasSystemBackdrop => AccentState.EnableHostBackdrop,
                DesktopBackgroundMaterial.Acrylic => AccentState.EnableAcrylicBlurBehind,
                _ => AccentState.Disabled,
            },
            GradientColor = ToAbgr(background, opacity),
        };
        int policySize = Marshal.SizeOf<AccentPolicy>();
        nint policyPointer = Marshal.AllocHGlobal(policySize);
        try
        {
            Marshal.StructureToPtr(policy, policyPointer, fDeleteOld: false);
            var data = new WindowCompositionAttributeData
            {
                Attribute = WindowCompositionAttribute.AccentPolicy,
                Data = policyPointer,
                SizeOfData = policySize,
            };
            SetWindowCompositionAttribute(window.Handle, ref data);
        }
        finally
        {
            Marshal.FreeHGlobal(policyPointer);
        }
    }

    private static int ToAbgr(Color color, double opacity)
    {
        int alpha = (int)Math.Round(Math.Clamp(opacity, 0, 1) * byte.MaxValue);
        return (alpha << 24) | (color.B << 16) | (color.G << 8) | color.R;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowCompositionAttribute(
        nint windowHandle,
        ref WindowCompositionAttributeData data);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        nint windowHandle,
        int attribute,
        ref int attributeValue,
        int attributeSize);

    private const int SystemBackdropTypeAttribute = 38;

    private enum AccentState
    {
        Disabled = 0,
        EnableAcrylicBlurBehind = 4,
        EnableHostBackdrop = 5,
    }

    private enum WindowCompositionAttribute
    {
        AccentPolicy = 19,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public AccentState AccentState;
        public int AccentFlags;
        public int GradientColor;
        public int AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public WindowCompositionAttribute Attribute;
        public nint Data;
        public int SizeOfData;
    }
}
