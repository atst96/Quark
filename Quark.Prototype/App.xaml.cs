using System;
using System.Diagnostics;
using System.Windows;
using Quark.DependencyInjection;
using Quark.Services;

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
        // ServiceLocatorwを初期化
        ServiceLocator.Initialize(sc => sc.RegisterContext());

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // 設定情報を保存
        var settingService = ServiceLocator.GetService<SettingService>();
        settingService.Save();

        base.OnExit(e);
    }
}
