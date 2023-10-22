using Microsoft.Extensions.DependencyInjection;
using Quark.DependencyInjection;

namespace Quark.Services;

/// <summary>
/// サービスロケータ
/// </summary>
public static class ServiceLocator
{
    /// <summary>ServiceProvider</summary>
    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    /// <summary>
    /// 初期化する
    /// </summary>
    /// <param name="action"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public static void Initialize(Action<IServiceCollection>? action = null)
    {
        if (ServiceProvider != null)
            throw new InvalidOperationException("ServiceLocator is already initialized.");

        var serviceCollection = new ServiceCollection();

        // 共有プロジェクト(Quark.Shard)のDI登録
        serviceCollection.RegisterContext();

        // 任意プロジェクトのDI登録
        action?.Invoke(serviceCollection);

        // ServiceProivider設定
        ServiceProvider = serviceCollection.BuildServiceProvider();
    }

    /// <summary>サービスを取得する</summary>
    public static T GetService<T>() where T: class
        => ServiceProvider.GetService<T>() ?? throw new Exception($"Type {typeof(T)} not found.");
}
