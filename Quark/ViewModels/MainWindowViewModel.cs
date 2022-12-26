using JetBrains.Annotations;
using Quark.Views;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Input;

namespace Quark.ViewModels;
public class MainWindowViewModel : ViewModelBase
{
    private Interaction<PreferenceWindow, object?> Dlg = new();


    private ICommand _command;
    public ICommand Command => this._command ??= ReactiveCommand.Create(() =>
    {
        this.Dlg.Handle(new PreferenceWindow());
    });
}
