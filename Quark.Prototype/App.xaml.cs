using System;
using System.Diagnostics;
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

    public App() : base()
    {
        AppDomain.CurrentDomain.UnhandledException += this.OnUnhandledException;
    }

    /// <summary>
    /// 例外キャッチ時
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (!Debugger.IsAttached)
        {
            // 非デバッグ時はダイアログを表示してアプリケーションが突然落ちるのを防ぐ

            var exception = (Exception)e.ExceptionObject;

            System.Windows.Forms.TaskDialog.ShowDialog(new()
            {
                Caption = "Quark",
                Heading = "予期しないエラーでアプリケーションが終了しました",
                Text = exception.Message,
                Icon = System.Windows.Forms.TaskDialogIcon.Error,
                Expander = new()
                {
                    Text = exception.StackTrace,
                }
            });
        }
    }

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
