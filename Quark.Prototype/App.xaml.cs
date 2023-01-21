using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Quark.Services;
using Quark.Utils;

namespace Quark;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private static App? _instance;
    public static App Instance => _instance ??= (App)Current;

    public IServiceProvider? ServiceProvider { get; private set; }

    /// <summary>
    /// 起動時
    /// </summary>
    /// <param name="e"></param>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var path = PathUtil.GetAbsolutePath(Config.SettingFile);
        var settingService = new SettingService(path);

        ServiceProvider = new ServiceCollection()
            // Service
            .AddSingleton(settingService)
            .AddSingleton<NeutrinoV1Service>()
            .AddSingleton<NeutrinoV2Service>()
            .AddSingleton<ProjectService>()
            // ViewModel
            .AddTransient<ViewModels.MainWindowViewModel>()
            .AddTransient<ViewModels.PreferenceWindowViewModel>()
            .AddTransient<ViewModels.NewProjectWindowViewModel>()
            // Build
            .BuildServiceProvider();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // 設定情報を保存
        var settingService = ServiceProvider!.GetService<SettingService>()!;
        settingService.Save();

        base.OnExit(e);
    }
}
