using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace Quark.Compatibles.Windows;

// TODO: P/Invoke部分をLibraryImportかCsWin32辺りに置き換える
internal static class NativeMethods
{
    public const uint WS_EX_NOACTIVATE = 0x08000000;
    public const uint WS_EX_TRANSPARENT = 0x00000020;
    public const int GWL_EXSTYLE = -20;

    [SupportedOSPlatformGuard("windows")]
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern SafeFileHandle CreateFile(
        [MarshalAs(UnmanagedType.LPTStr)] string filename,
        [MarshalAs(UnmanagedType.U4)] FileAccess access,
        [MarshalAs(UnmanagedType.U4)] FileShare share,
        IntPtr securityAttributes,
        [MarshalAs(UnmanagedType.U4)] FileMode creationDisposition,
        [MarshalAs(UnmanagedType.U4)] FileAttributes flagsAndAttributes,
        IntPtr templateFile);

    [SupportedOSPlatformGuard("windows")]
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern uint GetWindowLong(nint hWnd, int nIndex);

    [SupportedOSPlatform("windows")]
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern uint SetWindowLong(nint hwnd, int index, uint value);
}
