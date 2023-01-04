using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Quark.Behaviors;
using Quark.Data.Settings;
using Quark.Mvvm;
using Quark.Services;

namespace Quark.ViewModels;

/// <summary>
/// 設定ウィンドウのViewModel
/// </summary>
internal class PreferenceWindowViewModel : ViewModelBase
{
    private SettingService _settingService;

    public PreferenceWindowViewModel(SettingService settingService) : base()
    {
        this._settingService = settingService;
        this._settings = settingService.Settings;

        this.NeutrinoDirectory = this._settings.Neutrino.Directory;
    }

    private ICommand? _neutrinoDirectorySelectCommand;
    private Settings _settings;

    public ICommand NeutrinoDirectorySelectCommand => this._neutrinoDirectorySelectCommand ??= new DelegateCommand<FolderSelectionMessage>(msg =>
    {
        var directory = msg.Response;

        if (directory is not null)
        {
            // 設定変更
            this._settings.Neutrino.Directory = directory;

            // 表示変更
            this.NeutrinoDirectory = directory;
            this.RaisePropertyChanged(nameof(NeutrinoDirectory));
        }
    });

    public string? NeutrinoDirectory { get; private set; }

    private ICommand _closeCommand;
    public ICommand CloseCommand => this._closeCommand ??= new DelegateCommand<CancelEventArgs>(a =>
    {
        this._settingService.Save();
    });
}
