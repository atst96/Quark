using System;

namespace Quark.Xaml.Messengers;

public class Messenger
{
    public event EventHandler<MessageBase?>? OnMessageReceived;
}
