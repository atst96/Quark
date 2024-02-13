using Quark.Utils;

namespace Quark;

public class AppInstance
{
    public static AppInstance Instance { get; } = new();

    public string Id { get; } = IdUtil.RandomString(10);
}
