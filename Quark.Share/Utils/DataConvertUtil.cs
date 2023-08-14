using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Quark.Utils;

/// <summary>
/// バイナリデータの操作に関するユーティリティ
/// </summary>
public class DataConvertUtil
{
    /// <summary>
    /// 数値配列の型を返還する
    /// </summary>
    /// <typeparam name="TSrc">変換前の型</typeparam>
    /// <typeparam name="TDest">変換後の型</typeparam>
    /// <param name="src">変換対象の配列</param>
    /// <returns>変換後の配列</returns>
    public static TDest[] ConvertArray<TSrc, TDest>(TSrc[] src)
        where TSrc : INumber<TSrc>
        where TDest : INumber<TDest>
    {
        TDest[] dest = new TDest[src.Length];

        for (int idx = 0; idx < src.Length; ++idx)
            dest[idx] = TDest.CreateChecked(src[idx]);

        return dest;
    }

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
    /// 構造体をバイト配列に変換する
    /// </summary>
    /// <typeparam name="T">変換後の型</typeparam>
    /// <param name="data">変換対象</param>
    /// <returns>変換後データ</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] ConvertToByte<T>(Span<T> data) where T : struct
        => Convert<T, byte>(data);

    /// <summary>
    /// 構造体をバイト配列に変換する
    /// </summary>
    /// <typeparam name="T">変換後の型</typeparam>
    /// <param name="data">変換対象</param>
    /// <returns>変換後データ</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<byte> CastToByte<T>(Span<T> data) where T : struct
        => Cast<T, byte>(data);

    /// <summary>
    /// 構造体をバイト配列に変換する
    /// </summary>
    /// <typeparam name="T">変換後の型</typeparam>
    /// <param name="data">変換対象</param>
    /// <returns>変換後データ</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<byte> CastToByte<T>(T[] data) where T : struct
        => Cast<T, byte>(data);

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
