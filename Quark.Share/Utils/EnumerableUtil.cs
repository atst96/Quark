using System.Runtime.CompilerServices;

namespace Quark.Utils;

public class EnumerableUtil
{

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<T> ToEnumerable<T>(T value)
        => Enumerable.Repeat(value, 1);


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<T> ToEnumerable<T>(ref T value)
        => Enumerable.Repeat(value, 1);
}
