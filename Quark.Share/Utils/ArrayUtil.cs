using System.Diagnostics.CodeAnalysis;

namespace Quark.Utils;

/// <summary>
/// 配列操作に関するUtilクラス
/// </summary>
public static class ArrayUtil
{
    /// <summary>
    /// 初期値を設定した配列を生成する。
    /// </summary>
    /// <typeparam name="T">配列の型</typeparam>
    /// <param name="length">要素数</param>
    /// <param name="initialValue">初期値</param>
    /// <returns></returns>
    public static T[] Create<T>(int length, T initialValue)
    {
        var data = new T[length];
        data.AsSpan().Fill(initialValue);
        return data;
    }

    /// <summary>
    /// <see cref="Nullable{T}"/>な配列を非Nullableな配列に変換する。
    /// </summary>
    /// <typeparam name="T">型(構造体)</typeparam>
    /// <param name="values">配列</param>
    /// <param name="ifNullValue">nullの場合に設定する値</param>
    /// <returns></returns>
    public static T[]? UnNullable<T>(T?[]? values, T ifNullValue)
        where T : struct
    {
        if (values == null)
            return null;

        T[] dest = new T[values.Length];

        for (int idx = 0; idx < dest.Length; ++idx)
            dest[idx] = values[idx].GetValueOrDefault(ifNullValue);

        return dest;
    }

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

    /// <summary>
    /// フラット化された配列をコピーする。
    /// </summary>
    /// <typeparam name="T">型情報</typeparam>
    /// <param name="src">コピー元</param>
    /// <param name="srcOffset">コピー元の要素番号</param>
    /// <param name="dest">コピー先</param>
    /// <param name="destOffset">コピー先の要素番号</param>
    /// <param name="elementCount">要素数</param>
    /// <param name="dimensions">次元数</param>
    public static void CopyTo<T>(this T[] src, int srcOffset, T[] dest, int destOffset, int elementCount, int dimensions)
            where T : struct
    {
        int offset = srcOffset * dimensions;
        src.AsSpan(offset, Math.Max(0, Math.Min(src.Length - offset, elementCount * dimensions))).CopyTo(dest.AsSpan(destOffset * dimensions));
    }
}
