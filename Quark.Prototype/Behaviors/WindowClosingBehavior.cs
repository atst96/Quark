using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Microsoft.Xaml.Behaviors;

namespace Quark.Behaviors;

/// <summary>ウィンドウを閉じる処理の呼ばれるビヘイビア</summary>
public class WindowClosingBehavior : Behavior<Window>
{
    /// <summary>Closingイベント発火時に実行するコマンドを取得または設定する</summary>
    public ICommand Command
    {
        get => (ICommand)this.GetValue(CommandProperty);
        set => this.SetValue(CommandProperty, value);
    }

    /// <summary><seealso cref="Command"/>の依存関係プロパティ</summary>
    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(WindowClosingBehavior), new PropertyMetadata(null));

    /// <summary>ビヘイビア割り当て時</summary>
    protected override void OnAttached()
    {
        var window = this.AssociatedObject;
        window.Closing += this.OnClosing;
    }

    /// <summary>ビヘイビア割り当て解除時</summary>
    protected override void OnDetaching()
    {
        var window = this.AssociatedObject;
        window.Closing -= this.OnClosing;
    }

    /// <summary>ウィンドウを閉じるイベント発火時</summary>
    /// <param name="sender">イベント発火元</param>
    /// <param name="e">イベント</param>
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        var command = this.Command;
        if (command is not null)
        {
            var instance = new WindowCloseRequest();

            if (command.CanExecute(instance))
            {
                command.Execute(instance);
                if (instance.IsCancelled)
                {
                    e.Cancel = true;
                }
            }
        }
    }
}
