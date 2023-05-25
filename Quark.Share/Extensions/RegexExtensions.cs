using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Quark.Extensions;

/// <summary>
/// 正規表現に関する拡張メソッド
/// </summary>
public static class RegexExtensions
{
    /// <summary>キャプチャされたグループの値を取得する</summary>
    /// <param name="match"></param>
    /// <param name="groupName"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetValue(this Match match, string groupName)
        => match.Groups[groupName].Value;

    /// <summary>キャプチャされたグループの値を取得する</summary>
    /// <typeparam name="T">型</typeparam>
    /// <param name="match"></param>
    /// <param name="groupName"></param>
    /// <returns></returns>
    public static T GetValue<T>(this Match match, string groupName) where T : INumberBase<T>
        => T.Parse(match.Groups[groupName].Value, null);
}
