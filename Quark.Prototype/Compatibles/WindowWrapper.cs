using System.Windows.Forms;

namespace Quark.Compatibles;

public class WindowWrapper : IWin32Window
{
    public nint Handle { get; set; }

    public WindowWrapper(nint handle)
    {
        this.Handle = handle;
    }

    public static WindowWrapper FromHandle(nint handle) => new(handle);
}
