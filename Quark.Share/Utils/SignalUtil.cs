using System.Numerics;
using System.Runtime.CompilerServices;

namespace Quark.Utils;

public static class SignalUtil
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T ToLogScale<T>(T value)
        where T : IBinaryFloatingPointIeee754<T>
        => value == T.Zero ? T.Zero : T.Pow(T.CreateChecked(10), value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T ToLinearScale<T>(T value)
        where T : IFloatingPointIeee754<T>
        => value == T.Zero ? T.Zero : T.Log10(value);
}
