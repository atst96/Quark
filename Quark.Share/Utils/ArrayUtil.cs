using System.Diagnostics.CodeAnalysis;

namespace Quark.Utils;

/// <summary>
/// 配列操作に関するUtilクラス
/// </summary>
public static class ArrayUtil
{
    /// <summary>
    /// 要素数が<paramref name="segmentCount"/>*<paramref name="dimension"/>となる配列を生成し、各セグメントの最初の要素を<paramref name="initValue"/>で初期化する。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="segmentCount"></param>
    /// <param name="dimension"></param>
    /// <param name="initValue"></param>
    /// <returns></returns>
    public static T[] CreateAndInitSegmentFirst<T>(int segmentCount, int dimension, T initValue)
    {
        var values = new T[segmentCount * dimension];

        for (int idx = 0; idx < values.Length; idx += dimension)
            values[idx] = initValue;

        return values;
    }


    /// <summary>
    /// 配列がnullまたは空かどうかを判定する。
    /// </summary>
    /// <typeparam name="T">型</typeparam>
    /// <param name="array">検証対象の配列</param>
    /// <returns>指定した配列がnullまたは空かどうかのフラグ</returns>
    public static bool IsNullOrEmpty<T>([NotNullWhen(false)] this T[]? array)
        => array == null || array.Length == 0;

    /// <summary>
    /// 配列をコピーする。
    /// </summary>
    /// <typeparam name="T">配列の型</typeparam>
    /// <param name="array">配列</param>
    /// <returns></returns>
    [return: NotNullIfNotNull(nameof(array))]
    public static T[]? Clone<T>(this T[]? array)
        where T : struct
    {
        if (array == null)
            return null;

        return array.AsSpan().ToArray();
    }
}
