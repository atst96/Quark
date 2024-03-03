using Quark.DependencyInjection;
using Quark.Mvvm;
using Quark.Services;
using Quark.UI.Mvvm;

namespace Quark.ViewModels;

[Prototype]
public partial class MainWindowViewModel : ViewModelBase, IDialogServiceViewModel
{
    public DialogService DialogService { get; }

    public MainWindowViewModel(DialogService dialogService) : base()
    {
        this.DialogService = dialogService;
    }

    private Command? _showPreferenceWindowCommand;
    public Command ShowPreferenceWindowCommand => this._showPreferenceWindowCommand ??= this.AddCommand(
        async () => await this.DialogService.ShowSettingWindow());

    public string Message { get; } = "Hello World!";
}
