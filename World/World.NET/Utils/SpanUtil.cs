using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace World.NET.Utils;

public static class SpanUtil
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<TTo> Cast<TFrom, TTo>(this Span<TFrom> span)
        where TFrom : struct
        where TTo : struct
        => MemoryMarshal.Cast<TFrom, TTo>(span);
}
