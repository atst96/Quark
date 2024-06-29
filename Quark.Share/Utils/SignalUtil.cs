using System.Numerics;
using System.Runtime.CompilerServices;

namespace Quark.Utils;

public static class SignalUtil
{
    /// <summary>定数</summary>
    private static class TConstantValue<T> where T : IFloatingPointIeee754<T>
    {
        /// <summary>基数の定数(10)</summary>
        public static T Base10 = T.CreateChecked(10);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T ToLogScale<T>(T value) where T : IFloatingPointIeee754<T>
        => value == T.Zero ? T.Zero : T.Pow(TConstantValue<T>.Base10, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T ToLinearScale<T>(T value) where T : IFloatingPointIeee754<T>
        => value == T.Zero ? T.Zero : T.Log10(value);
}
