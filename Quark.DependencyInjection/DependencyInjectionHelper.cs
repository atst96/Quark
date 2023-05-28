using System;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Quark.DependencyInjection;

/// <summary>
/// 依存性注入のヘルパークラス
/// </summary>
public static class DependencyInjectionHelper
{
    /// <summary>
    /// 現在のアセンブリ内の対象クラスを登録する
    /// </summary>
    /// <typeparam name="T"><paramref name="service"/>の型</typeparam>
    /// <param name="service">ServiceCollection</param>
    /// <returns></returns>
    public static T RegisterContext<T>(this T service)
           where T : IServiceCollection
        => RegisterForAssembly(service, Assembly.GetCallingAssembly());

    /// <summary>
    /// <paramref name="assembly"/>に指定したアセンブリ内で定義されている対象クラスを登録する
    /// </summary>
    /// <typeparam name="T"><paramref name="services"/>の型</typeparam>
    /// <param name="services">ServiceCollection</param>
    /// <param name="assembly">検索対象のアセンブリ</param>
    /// <returns></returns>
    public static T RegisterForAssembly<T>(this T services, Assembly assembly)
           where T : IServiceCollection
    {
        // アセンブリ内のクラスを列挙する
        var definedTypes = assembly.DefinedTypes
            .Where(t => t.IsClass && !t.IsSubclassOf(typeof(Attribute)));

        foreach (var type in definedTypes)
        {
            foreach (var attr in type.GetCustomAttributes().OfType<IDependencyInjectionScope>())
            {
                if (attr is SingletonAttribute)
                    RegisterSingleton(services, type, attr);
                else if (attr is PrototypeAttribute)
                    RegisterPrototype(services, type, attr);
            }
        }

        return services;
    }

    /// <summary>
    /// シングルトンとして登録する
    /// </summary>
    /// <param name="services"></param>
    /// <param name="type"></param>
    /// <param name="attr"></param>
    private static void RegisterSingleton(IServiceCollection services, Type type, IDependencyInjectionScope attr)
    {
        var implementationType = attr.ImplementationFor;
        if (implementationType != null && type.IsSubclassOf(implementationType))
            services.AddSingleton(type, implementationType);
        else
            services.AddSingleton(type);
    }

    /// <summary>
    /// 非シングルトンとして登録する
    /// </summary>
    /// <param name="services"></param>
    /// <param name="type"></param>
    /// <param name="attr"></param>
    private static void RegisterPrototype(IServiceCollection services, Type type, IDependencyInjectionScope attr)
    {
        var implementationType = attr.ImplementationFor;
        if (implementationType != null && type.IsSubclassOf(implementationType))
            services.AddTransient(type, implementationType);
        else
            services.AddTransient(type);
    }
}
