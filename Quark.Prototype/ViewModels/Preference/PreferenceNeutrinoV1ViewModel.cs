using Quark.Behaviors;
using System.Windows.Input;
using Quark.Mvvm;
using Quark.Data.Settings;
using Quark.Services;

namespace Quark.ViewModels.Preference;

/// <summary>
/// NEUTRINO(v1)設定画面のViewModel
/// </summary>
internal class PreferenceNeutrinoV1ViewModel : ViewModelBase
{
    public void ApplyToViewModel(Settings settings)
    {
        var v1 = settings.NeutrinoV1;
        this._directory = v1.Directory;
        this._useLegacyExe = v1.UseLegacyExe;
        this._useGpu = v1.UseGpu;
        this._cpuThreads = v1.CpuThreads;
    }

    public void ApplyToSettings(Settings settings)
    {
        var v1 = settings.NeutrinoV1;
        v1.Directory = this._directory;
        v1.UseLegacyExe = this._useLegacyExe;
        v1.UseGpu = this._useGpu;
        v1.CpuThreads = this._cpuThreads;
    }

    private string? _directory;
    public string? Directory
    {
        get => this._directory;
        set => this.RaisePropertyChangedIfSet(ref this._directory, value);
    }

    private bool? _useLegacyExe;
    public bool? UseLegacyExe
    {
        get => this._useLegacyExe;
        set
        {
            if (this.RaisePropertyChangedIfSet(ref this._useLegacyExe, value))
            {
                this.RaisePropertyChanged(nameof(this.SelectExeLabel));
            }
        }
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

    private ICommand? _directorySelectCommand;
    public ICommand DirectorySelectCommand => this._directorySelectCommand ??= this.AddCommand<FolderSelectionMessage>(msg =>
    {
        if (msg.Response is not null)
            this.Directory = msg.Response;
    });
}
