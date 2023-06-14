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

        this.NeutrinoDirectory = this._settings.NeutrinoV1.Directory;

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

        this.NeutrinoV1ViewModel.ApplyToViewModel(settings);
        this.NeutrinoV2ViewModel.ApplyToViewModel(settings);
    }

    /// <summary>
    /// 画面の内容を設定情報に反映する
    /// </summary>
    private void ApplyToSettings()
    {
        var settings = this._settings;

        this.NeutrinoV1ViewModel.ApplyToSettings(settings);
        this.NeutrinoV2ViewModel.ApplyToSettings(settings);
    }

    public string? NeutrinoDirectory { get; private set; }

    private ICommand? _closeCommand;
    public ICommand CloseCommand => this._closeCommand ??= this.AddCommand<CancelEventArgs>(a =>
    {
        // 設定情報を反映
        this.ApplyToSettings();

        // 設定情報を保存
        this._settingService.Save();
    });
}
