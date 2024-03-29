﻿using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Input;
using Quark.Components;
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

    /// <summary>推論オプションの選択肢名</summary>
    public IReadOnlyDictionary<EstimateMode, string> EsimateModeNames { get; } = new Dictionary<EstimateMode, string>
    {
        [EstimateMode.Fast] = "速度優先",
        [EstimateMode.Quality] = "品質優先",
    };

    private EstimateMode _estimateMode = EstimateMode.Fast;
    /// <summary>推論品質</summary>
    public EstimateMode EstimateMode
    {
        get => this._estimateMode;
        set => this.RaisePropertyChangedIfSet(ref this._estimateMode, value);
    }

    /// <summary>音声品質オプションの項目名</summary>
    public IReadOnlyDictionary<SynthesisMode, string> SynthesisModeNames { get; } = new Dictionary<SynthesisMode, string>
    {
        [SynthesisMode.MostFast] = "速度優先(最速)",
        [SynthesisMode.Fast] = "速度優先",
        [SynthesisMode.Quality] = "品質優先",
    };

    private SynthesisMode _synthesisMode = SynthesisMode.Fast;
    /// <summary>音声品質</summary>
    public SynthesisMode SynthesisMode
    {
        get => this._synthesisMode;
        set => this.RaisePropertyChangedIfSet(ref this._synthesisMode, value);
    }
}
