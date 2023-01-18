using System;

namespace Quark.Utils;

internal static class GuidUtil
{
    public static string GetStringGuid() => Guid.NewGuid().ToString("B");
}
