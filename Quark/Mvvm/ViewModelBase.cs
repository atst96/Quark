using System;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Quark.Mvvm;

public class ViewModelBase : ObservableObject, IDisposable
{
    protected DisposableCollection DisposableCollection { get; } = new();

    protected ViewModelBase() : base()
    {
    }

    public Command AddCommand(Action execute)
        => this.AddDisposable(new DelegateCommand(execute));

    public Command AddCommand(Func<bool> canExecute, Action execute)
        => this.AddDisposable(new DelegateCommand(execute, canExecute));

    public Command<T> AddCommand<T>(Action<T> execute)
        => this.AddDisposable(new DelegateCommand<T>(execute));

    public Command<T> AddCommand<T>(Predicate<T> canExecute, Action<T> execute)
        => this.AddDisposable(new DelegateCommand<T>(execute, canExecute));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected T AddDisposable<T>(T command)
        where T : IDisposable
    {
        this.DisposableCollection.Add(command);
        return command;
    }

    public void Dispose()
    {
        this.DisposableCollection.Dispose();
    }

    ~ViewModelBase()
    {
        ((IDisposable)this).Dispose();
    }
}
