using System.Numerics;
using System.Runtime.CompilerServices;

namespace Quark.Utils;

/// <summary>
/// 計算に関するユーティリティクラス
/// </summary>
public static class MathUtil
{
    public static T Sum<T>(this Span<T> values)
        where T : INumber<T>
    {
        T sum = T.Zero;

        foreach (var value in values)
            sum += value;

        return sum;
    }


    public static T Average<T>(this Span<T> values)
        where T : INumber<T>
    {
        T value = values.Sum();
        return value / T.CreateChecked(values.Length);
    }

    public static void Add<T>(this Span<T> values, T value)
        where T : INumber<T>
    {
        for (int i = 0; i < values.Length; ++i)
            values[i] += value;
    }

    /// <summary>
    /// 数値配列要素の最小値、最大値、平均値を求める。
    /// </summary>
    /// <typeparam name="T">数値型</typeparam>
    /// <param name="values">配列</param>
    /// <returns>(最小値、最大値、平均値)</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (T min, T max, T avg) MinMaxAvg<T>(this T[] values) where T : INumber<T>
        => MinMaxAvg((Span<T>)values);

    /// <summary>
    /// 数値配列要素の最小値、最大値、平均値を求める。
    /// </summary>
    /// <typeparam name="T">数値型</typeparam>
    /// <param name="values"><seealso cref="Span{T}"/></param>
    /// <returns>(最小値、最大値、平均値)</returns>
    public static (T min, T max, T avg) MinMaxAvg<T>(this Span<T> values) where T : INumber<T>
    {
        T value = values[0];
        (T min, T max, T avg) = (value, value, value);

        for (int idx = 1; idx < values.Length; ++idx)
        {
            value = values[idx];
            avg += value;

            if (min > value)
                min = value;

            if (max < value)
                max = value;
        }

        avg /= T.CreateChecked(values.Length);

        return (min, max, avg);
    }
}
