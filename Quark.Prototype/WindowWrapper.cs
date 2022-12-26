using System.Windows.Forms;

namespace Quark;

public class WindowWrapper : IWin32Window
{
    public nint Handle { get; set; }

    public WindowWrapper(nint handle)
    {
        this.Handle = handle;
    }
}
