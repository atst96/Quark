using System.ComponentModel;
using System.Windows.Input;
using Quark.Behaviors;
using Quark.Data.Settings;
using Quark.DependencyInjection;
using Quark.Mvvm;
using Quark.Services;
using Quark.ViewModels.Preference;

namespace Quark.ViewModels;

/// <summary>
/// 設定ウィンドウのViewModel
/// </summary>
[Prototype]
internal class PreferenceWindowViewModel : ViewModelBase
{
    private Settings _settings;
    private SettingService _settingService;

    public PreferenceNeutrinoV1ViewModel NeutrinoV1ViewModel { get; }

    public PreferenceNeutrinoV2ViewModel NeutrinoV2ViewModel { get; }

    public PreferenceWindowViewModel(SettingService settingService) : base()
    {
        this._settingService = settingService;
        this._settings = settingService.Settings;

        this.NeutrinoV1ViewModel = this.AddDisposable(new PreferenceNeutrinoV1ViewModel());
        this.NeutrinoV2ViewModel = this.AddDisposable(new PreferenceNeutrinoV2ViewModel());

        this.SettingsApplyToViewModel();
    }

    /// <summary>
    /// 設定情報を読み込んでViewModelに反映する
    /// </summary>
    private void SettingsApplyToViewModel()
    {
        var settings = this._settings;

        this._useRecentDirectories = settings.Recents.UseRecentDirectories;

        this.NeutrinoV1ViewModel.ApplyToViewModel(settings);
        this.NeutrinoV2ViewModel.ApplyToViewModel(settings);

        // 音声合成設定
        var synthesis = settings.Synthesis;
        this._cpuThreads = synthesis.CpuThreads;
        this._useGpu = synthesis.UseGpu;
        this._useBulkEstimate = synthesis.UseBulkEstimate;
    }

    /// <summary>
    /// 画面の内容を設定情報に反映する
    /// </summary>
    private void ApplyToSettings()
    {
        var settings = this._settings;

        settings.Recents.UseRecentDirectories = this._useRecentDirectories;

        this.NeutrinoV1ViewModel.ApplyToSettings(settings);
        this.NeutrinoV2ViewModel.ApplyToSettings(settings);

        // 音声合成設定
        var synthesis = settings.Synthesis;
        synthesis.CpuThreads = this._cpuThreads;
        synthesis.UseGpu = this._useGpu;
        synthesis.UseBulkEstimate = this._useBulkEstimate;
    }

    private bool _useRecentDirectories;

    /// <summary>直近に選択したフォルダ使用フラグ</summary>
    public bool UseRecentDirectories
    {
        get => this._useRecentDirectories;
        set => this.RaisePropertyChangedIfSet(ref this._useRecentDirectories, value);
    }

    private ICommand? _closeCommand;
    public ICommand CloseCommand => this._closeCommand ??= this.AddCommand<CancelEventArgs>(a =>
    {
        // 設定情報を反映
        this.ApplyToSettings();

        // 設定情報を保存
        this._settingService.Save();
    });

    private int? _cpuThreads;
    public int? CpuThreads
    {
        get => this._cpuThreads;
        set => this.RaisePropertyChangedIfSet(ref this._cpuThreads, value);
    }

    private bool _useGpu;
    /// <summary>GPUオプション</summary>
    public bool UseGpu
    {
        get => this._useGpu;
        set => this.RaisePropertyChangedIfSet(ref this._useGpu, value);
    }

    private bool _useBulkEstimate;
    /// <summary>一括処理オプション</summary>
    public bool UseBulkEstimate
    {
        get => this._useBulkEstimate;
        set => this.RaisePropertyChangedIfSet(ref this._useBulkEstimate, value);
    }
}
