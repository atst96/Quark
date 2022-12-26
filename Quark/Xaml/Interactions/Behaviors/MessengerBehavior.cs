using Avalonia;
using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;
using Quark.Xaml.Messengers;

namespace Quark.Xaml.Interactions.Behaviors;

public class MessengerBehavior : Behavior<IControl>
{
    /// <summary>
    /// Defines the <see cref="Messenger"/> property.
    /// </summary>
    public static readonly StyledProperty<Messenger?> MessengerProperty =
        AvaloniaProperty.Register<MessengerBehavior, Messenger?>(nameof(Messenger), default);


    protected override void OnPropertyChanged<T>(AvaloniaPropertyChangedEventArgs<T> change)
    {
        base.OnPropertyChanged(change);

        // Messenger変更時
        if (change.Property == MessengerProperty)
        {
            var oldMessenger = change.OldValue.GetValueOrDefault<Messenger>();
            var newMessenger = change.NewValue.GetValueOrDefault<Messenger>();

            if (oldMessenger is not null)
            {
                oldMessenger.OnMessageReceived -= this.OnMessageReceived;
            }

            if (newMessenger is not null)
            {
                newMessenger.OnMessageReceived += this.OnMessageReceived;
            }
        }
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();

        var messenger = this.Messenger;
        if (messenger is not null)
        {
            messenger.OnMessageReceived -= this.OnMessageReceived;
        }
    }

    /// <summary>
    /// メッセージ受信時
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnMessageReceived(object? sender, MessageBase? e)
    {

    }

    /// <summary>
    /// メッセージ名
    /// </summary>
    public string? MessageName { get; set; }

    /// <summary>
    /// メッセンジャー
    /// </summary>
    public Messenger? Messenger => this.GetValue(MessengerProperty);
}
