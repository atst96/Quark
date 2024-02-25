using CommunityToolkit.Mvvm.ComponentModel;
using Quark.Mvvm;
using Quark.Services;
using Quark.Data.Settings;
using Quark.DependencyInjection;
using Quark.Components;
using System.Collections.Generic;
using System;
using System.Diagnostics;
using Quark.UI.Mvvm;

namespace Quark.ViewModels;

[Prototype]
internal partial class PreferenceWindowViewModel : ViewModelBase, IDialogServiceViewModel
{
    private Settings _settings;
    private SettingService _settingService;

    public DialogService DialogService { get; }

    public PreferenceNeutrinoV1ViewModel NeutrinoV1ViewModel { get; }

    public PreferenceNeutrinoV2ViewModel NeutrinoV2ViewModel { get; }


    public PreferenceWindowViewModel(DialogService dialogService, SettingService settingService)
    {
        this.DialogService = dialogService;
        this._settingService = settingService;
        this._settings = settingService.Settings;

        this.NeutrinoV1ViewModel = this.AddDisposable(new PreferenceNeutrinoV1ViewModel(dialogService));
        this.NeutrinoV2ViewModel = this.AddDisposable(new PreferenceNeutrinoV2ViewModel(dialogService));

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
        this._estimateMode = synthesis.EstimateMode;
        this._synthesisMode = synthesis.SynthesisMode;
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
        synthesis.EstimateMode = this._estimateMode;
        synthesis.SynthesisMode = this._synthesisMode;
    }

    /// <summary>直近に選択したフォルダ使用フラグ</summary>
    [ObservableProperty]
    private bool _useRecentDirectories;

    [ObservableProperty]
    private int? _cpuThreads;

    /// <summary>GPUオプション</summary>
    [ObservableProperty]
    private bool _useGpu;

    /// <summary>一括処理オプション</summary>
    [ObservableProperty]
    private bool _useBulkEstimate;

    public ItemValueViewModel<EstimateMode>[] EstimateModeNames { get; } = [
        new(EstimateMode.Fast, "速度優先"),
        new(EstimateMode.Quality, "品質優先"),
    ];

    [ObservableProperty]
    private EstimateMode _estimateMode;

    public ItemValueViewModel<SynthesisMode>[] SynthesisModeNames { get; } = [
        new(SynthesisMode.MostFast, "速度優先(最速)"),
        new(SynthesisMode.Fast, "速度優先"),
        new(SynthesisMode.Quality, "品質優先"),
    ];

    [ObservableProperty]
    private SynthesisMode _synthesisMode;
}
