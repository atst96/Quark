using System.Numerics;
using System.Runtime.CompilerServices;

namespace Quark.Utils;

public static class SignalUtil
{
    private static class GenericMath<T>
        where T : IBinaryFloatingPointIeee754<T>
    {
        public static readonly T Base10 = T.CreateChecked(10);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T ToLogScale<T>(T value)
        where T : IBinaryFloatingPointIeee754<T>
        => value == T.Zero ? T.Zero : T.Pow(GenericMath<T>.Base10, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T ToLinearScale<T>(T value)
        where T : IFloatingPointIeee754<T>
        => value == T.Zero ? T.Zero : T.Log10(value);
}
