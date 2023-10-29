using Quark.Behaviors;
using System.Windows.Input;
using Quark.Mvvm;
using Quark.Data.Settings;

namespace Quark.ViewModels.Preference;

internal class PreferenceNeutrinoV2ViewModel : ViewModelBase
{
    public void ApplyToViewModel(Settings settings)
    {
        var v2 = settings.NeutrinoV2;
        this._directory = v2.Directory;
    }

    public void ApplyToSettings(Settings settings)
    {
        var v2 = settings.NeutrinoV2;
        v2.Directory = this._directory;
    }

    private string? _directory;

    public string? Directory
    {
        get => this._directory;
        set => this.RaisePropertyChangedIfSet(ref this._directory, value);
    }

    private bool _useGpu;
    public bool UseGpu
    {
        get => this._useGpu;
        set => this.RaisePropertyChangedIfSet(ref this._useGpu, value);
    }

    private int? _cpuThreads;
    public int? CpuThreads
    {
        get => this._cpuThreads;
        set => this.RaisePropertyChangedIfSet(ref this._cpuThreads, value);
    }

    private ICommand? _directorySelectCommand;
    public ICommand DirectorySelectCommand => this._directorySelectCommand ??= this.AddCommand<FolderSelectionMessage>(msg =>
    {
        if (msg.Response is not null)
            this.Directory = msg.Response;
    });
}
