using Microsoft.Extensions.DependencyInjection;

namespace Quark.DependencyInjection;

/// <summary>
/// 依存性注入のヘルパークラス
/// </summary>
public static class DependencyInjectionHelper
{
    public static T RegisterSharedContext<T>(this T services)
        where T : IServiceCollection
        => services.RegisterContext();
}
