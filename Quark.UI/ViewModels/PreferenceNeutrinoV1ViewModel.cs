using Quark.Data.Settings;
using Quark.Mvvm;
using Quark.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Input;

namespace Quark.ViewModels;

internal partial class PreferenceNeutrinoV1ViewModel : ViewModelBase
{
    private DialogService _dialogService;

    public void ApplyToViewModel(Settings settings)
    {
        var v1 = settings.NeutrinoV1;
        this._directory = v1.Directory;
        this._useLegacyExe = v1.UseLegacyExe;
    }

    public void ApplyToSettings(Settings settings)
    {
        var v1 = settings.NeutrinoV1;
        v1.Directory = this._directory;
        v1.UseLegacyExe = this._useLegacyExe;
    }

    public PreferenceNeutrinoV1ViewModel(DialogService dialogService)
    {
        this._dialogService = dialogService;
    }

    [ObservableProperty]
    private string? _directory;

    [ObservableProperty]
    private bool? _useLegacyExe;

    partial void OnUseLegacyExeChanged(bool? value)
    {
        this.OnPropertyChanged(nameof(SelectExeLabel));
    }

    public string SelectExeLabel
    {
        get
        {
            if (this._useLegacyExe == null)
            {
                return NeutrinoV1Service.IsLegacy()
                    ? "(自動選択) レガシー版が使用されます。"
                    : "(自動選択) 現行版が使用されます。";
            }
            else
            {
                return this._useLegacyExe.Value
                    ? "レガシー版が使用されます。"
                    : "現行版が使用されます。";
            }
        }
    }

    private ICommand? _selectDirectoryCommand;

    public ICommand SelectDirectoryCommand => this._selectDirectoryCommand ??= this.AddCommand(async () =>
    {
        var path = await this._dialogService.SelectFolderAsync().ConfigureAwait(false);

        if (path == null)
            return;

        this.Directory = path;
    });
}
