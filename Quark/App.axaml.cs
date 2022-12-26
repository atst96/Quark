using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Quark.ViewModels;
using Quark.Views;
using System;

namespace Quark;
public partial class App : Application
{
    public IServiceProvider Services { get; }

    public App() : base()
    {
        this.Services = new ServiceCollection()
            // ViewModel
            .AddTransient<MainWindowViewModel>()
            .AddTransient<PreferenceWindowViewModel>()
            // Build
            .BuildServiceProvider();
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = this.Services.GetService<MainWindowViewModel>(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
