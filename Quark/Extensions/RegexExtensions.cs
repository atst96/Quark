using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Quark.Extensions;

internal static class RegexExtensions
{
    /// <summary>
    /// </summary>
    /// <param name="m"></param>
    /// <param name="key"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetValue(this Match m, string key) 
        => m.Groups[key].Value;
}
