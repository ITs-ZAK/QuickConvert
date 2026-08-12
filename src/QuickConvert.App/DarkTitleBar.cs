using System.Runtime.InteropServices;

namespace QuickConvert.App;

internal delegate int DwmAttributeSetter(
    nint handle,
    int attribute,
    ref int value,
    int size);

internal static class DarkTitleBar
{
    private const int CurrentDarkModeAttribute = 20;
    private const int LegacyDarkModeAttribute = 19;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(
        nint windowHandle,
        int attribute,
        ref int value,
        int valueSize);

    internal static bool TryApply(
        nint windowHandle,
        DwmAttributeSetter? setter = null)
    {
        if (windowHandle == nint.Zero)
            return false;

        setter ??= DwmSetWindowAttribute;
        var enabled = 1;
        try
        {
            if (setter(
                    windowHandle,
                    CurrentDarkModeAttribute,
                    ref enabled,
                    sizeof(int)) == 0)
                return true;

            return setter(
                windowHandle,
                LegacyDarkModeAttribute,
                ref enabled,
                sizeof(int)) == 0;
        }
        catch (Exception exception) when (
            exception is DllNotFoundException or
                EntryPointNotFoundException or
                BadImageFormatException)
        {
            return false;
        }
    }
}

