﻿namespace Quark.Utils;

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
}