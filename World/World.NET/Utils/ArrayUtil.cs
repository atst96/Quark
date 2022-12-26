using System.Buffers;
using System.Runtime.CompilerServices;

namespace World.NET.Utils;

internal static class ArrayUtil
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] Rent<T>(int minimumLength)
        => ArrayPool<T>.Shared.Rent(minimumLength);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Return<T>(ref T[] array)
        => ArrayPool<T>.Shared.Return(array);
}
