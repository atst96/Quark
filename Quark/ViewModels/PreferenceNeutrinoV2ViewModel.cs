using Quark.Data.Settings;
using Quark.Mvvm;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Input;
using Quark.Services;
using System.Diagnostics;

namespace Quark.ViewModels;

internal partial class PreferenceNeutrinoV2ViewModel : ViewModelBase
{
    private DialogService _dialogService;

    public PreferenceNeutrinoV2ViewModel(DialogService dialogService)
    {
        this._dialogService = dialogService;
    }

    public void ApplyToViewModel(Settings settings)
    {
        var v2 = settings.NeutrinoV2;
        this.Directory = v2.Directory;
    }

    public void ApplyToSettings(Settings settings)
    {
        var v2 = settings.NeutrinoV2;
        v2.Directory = this.Directory;
    }

    // [ObservableProperty]
    private string? _directory;

    public string Directory
    {
        get => this._directory;
        set => this.SetProperty(ref this._directory, value);
    }

    [ObservableProperty]
    private bool _useGpu;

    [ObservableProperty]
    private int? _cpuThreads;

    private ICommand? _selectDirectoryCommand;

    public ICommand SelectDirectoryCommand => this._selectDirectoryCommand ??= this.AddCommand(async () =>
    {
        var path = await this._dialogService.SelectFolderAsync().ConfigureAwait(false);

        if (path == null)
            return;

        this.Directory = path;
    });
}
