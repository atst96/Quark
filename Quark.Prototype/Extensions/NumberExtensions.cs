using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Quark.Extensions;

/// <summary>
/// 数値に関する拡張メソッド
/// </summary>
internal static class NumberExtensions
{
    /// <summary>
    /// コレクションのうち次に大きい数値を返却する
    /// </summary>
    /// <typeparam name="T">数値型</typeparam>
    /// <param name="collection">数値コレクション</param>
    /// <param name="value">現在値</param>
    /// <returns>現在値より大きな値があればそれを、なければ現在値を返す</returns>
    public static T GetNextUpper<T>(this IEnumerable<T> collection, T value)
        where T : struct, INumber<T>
    {
        var uppers = collection.Where(i => i > value);
        return uppers.Any() ? uppers.Min() : value;
    }

    /// <summary>
    /// コレクションのうち次に小さい数値を返却する
    /// </summary>
    /// <typeparam name="T">数値型</typeparam>
    /// <param name="collection">数値コレクション</param>
    /// <param name="value">現在値</param>
    /// <returns>現在より小さい値があればそれを、なければ現在値を返す</returns>
    public static T GetNextLower<T>(this IEnumerable<T> collection, T value)
        where T : struct, INumber<T>
    {
        var lowers = collection.Where(i => i < value);
        return lowers.Any() ? lowers.Max() : value;
    }
}
