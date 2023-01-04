using Livet.Behaviors.Messaging;
using Livet.Messaging;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;

namespace Quark.Behaviors;

internal class FolderBrowserDialogInteractionMessageAction : InteractionMessageAction<FrameworkElement>
{
    protected override void InvokeAction(InteractionMessage anyMessage)
    {
        if (anyMessage is not FolderSelectionMessage message)
        {
            return;
        }

        nint hwnd = new WindowInteropHelper(Window.GetWindow(this.AssociatedObject)).Handle;

        using var dialog = new FolderBrowserDialog()
        {
            UseDescriptionForTitle = true,
        };

        if (message.Description is not null)
            dialog.Description = message.Description;

        if (message.InitalDirectory is not null)
            dialog.InitialDirectory = message.InitalDirectory;

        message.Response = dialog.ShowDialog(new WindowWrapper(hwnd)) == DialogResult.OK
            ? dialog.SelectedPath
            : null;
    }
}
