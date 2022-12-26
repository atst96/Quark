using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace World.NET.Utils;

internal static class VectorUtil
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<Vector<T>> ToVector<T>(this Span<T> span)
    where T : struct
    => MemoryMarshal.Cast<T, Vector<T>>(span);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector<T> Pow2<T>(this Vector<T> vec)
        where T : struct
        => Vector.Multiply(vec, vec);

    public static void Diff(Span<double> x0, Span<double> x1, double[] dest)
    {
        int size = Vector<double>.Count;
        var vecX0 = x0.Cast<double, Vector<double>>();
        var vecX1 = x1.Cast<double, Vector<double>>();

        int i;
        for (i = 0; i < vecX0.Length; ++i)
        {
            (vecX0[i] - vecX1[i]).CopyTo(dest, i * size);
        }

        for (i *= size; i < x0.Length; ++i)
        {
            dest[i] = x0[i] - x1[i];
        }
    }

    public static Vector<T> GetAlternativeVector<T>(T v0, T v1)
        where T : struct, INumber<T>
    {
        int size = Vector<T>.Count;
        var data = new T[size];

        for (int i = 0; i < size; ++i)
        {
            data[i] = i % 2 == 0 ? v0 : v1;
        }

        return new(data);
    }

    public static void AddToSelf(Span<double> self, Span<double> value)
    {
        int size = Vector<double>.Count;
        int length = self.Length / size;

        var vec_self = self.Cast<double, Vector<double>>();
        var vec_add = value.Cast<double, Vector<double>>();

        int i;
        for (i = 0; i < length; ++i)
        {
            (vec_self[i] + vec_add[i]).CopyTo(self[(i * size)..]);
        }

        for (i *= size; i < self.Length; ++i)
        {
            self[i] += value[i];
        }
    }

    public static void SubstractToSelf<T>(Span<T> self, T value)
        where T : struct, INumber<T>
    {
        int size = Vector<T>.Count;
        int length = self.Length / size;

        var vec_self = self.Cast<T, Vector<T>>();
        var vec_substract = new Vector<T>(value);

        int i;
        for (i = 0; i < length; ++i)
        {
            (vec_self[i] - vec_substract).CopyTo(self[(i * size)..]);
        }

        for (i *= size; i < self.Length; ++i)
        {
            self[i] -= value;
        }
    }

    public static T Sum<T>(Span<T> span)
        where T : struct, INumber<T>
    {
        T value = default;
        int size = Vector<T>.Count;
        int offset = 0;

        if (span.Length > 31)
        {
            // 要素数が32件以上であればベクトル計算する
            var vec = span.Cast<T, Vector<T>>();
            Vector<T> vec_sum = new();
            foreach (var v in vec)
            {
                vec_sum += v;
            }

            for (int i = 0; i < size; ++i)
            {
                value += vec_sum[i];
            }

            offset = vec.Length * size;
        }

        foreach (T v in span[offset..])
        {
            value += v;
        }

        return value;
    }
}
