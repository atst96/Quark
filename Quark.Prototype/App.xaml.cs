using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Quark.DependencyInjection;
using Quark.Services;
using Quark.Utils;

namespace Quark;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    /// <summary>アプリケーション名</summary>
    public const string AppName = "Quark";

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

        this.ServiceProvider = new ServiceCollection()
            // 共有ライブラリのアセンブリ
            .RegisterSharedContext()
            // 現在のアセンブリ
            .RegisterContext()
            // Build
            .BuildServiceProvider();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        var serviceProvider = this.ServiceProvider!;

        // 設定情報を保存
        var settingService = serviceProvider.GetService<SettingService>()!;
        settingService.Save();

        base.OnExit(e);
    }
}
