using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Quark.Utils;

public class DataConvertUtil
{
    /// <summary>
    /// バイト配列を構造体に変換する
    /// </summary>
    /// <typeparam name="T">変換後の型</typeparam>
    /// <param name="data">変換対象</param>
    /// <returns>変換後データ</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T> Cast<T>(Span<byte> data) where T : struct
        => Cast<byte, T>(data);

    /// <summary>
    /// バイト配列を構造体に変換する
    /// </summary>
    /// <typeparam name="T">変換後の型</typeparam>
    /// <param name="data">変換対象</param>
    /// <returns>変換後データ</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<TTo> Cast<TFront, TTo>(Span<TFront> data)
        where TFront : struct
        where TTo : struct
        => MemoryMarshal.Cast<TFront, TTo>(data);

    /// <summary>
    /// バイト配列を構造体に変換する
    /// </summary>
    /// <typeparam name="T">変換後の型</typeparam>
    /// <param name="data">変換対象</param>
    /// <returns>変換後データ</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] Convert<T>(Span<byte> data) where T : struct
        => Convert<byte, T>(data);

    /// <summary>
    /// バイト配列を構造体に変換する
    /// </summary>
    /// <typeparam name="T">変換後の型</typeparam>
    /// <param name="data">変換対象</param>
    /// <returns>変換後データ</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TTo[] Convert<TFront, TTo>(Span<TFront> data)
        where TFront : struct
        where TTo : struct
        => MemoryMarshal.Cast<TFront, TTo>(data).ToArray();
}
