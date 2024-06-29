using System.Numerics;
using System.Runtime.CompilerServices;

namespace Quark.Converters;

/// <summary>
/// 音声データ変換クラス
/// </summary>
public static class AudioDataConverter
{
    /// <summary>ジェネリクスの数値</summary>
    private static class GenericValue<T> where T : INumber<T>
    {
        /// <summary>2</summary>
        public static readonly T N2 = T.CreateChecked(2d);

        /// <summary>1オクターブあたりの音階数</summary>
        public static readonly T OctaveTones12 = T.CreateChecked(12d);

        /// <summary>基本周波数(Hz)</summary>
        public static readonly T BaseFrequency = T.CreateChecked(440d);

        /// <summary>12平均律のベースのピッチ</summary>
        public static readonly T BasePitch12 = T.CreateChecked(69d);
    }

    // http://signalprocess.binarized.work/2019/03/26/convert_frequency_to_cent/
    // 12 * Log(frequency / 440Hz) * BaseScale(69)
    /// <summary>
    /// 周波数値を12平均律に変換する
    /// </summary>
    /// <param name="frequency">周波数</param>
    /// <returns>12平均律</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T FrequencyToPitch12<T>(T frequency) where T : IFloatingPointIeee754<T>
        => (GenericValue<T>.OctaveTones12 * T.Log2(frequency / GenericValue<T>.BaseFrequency)) + GenericValue<T>.BasePitch12;

    /// <summary>
    /// 12平均律を周波数に変換する
    /// </summary>
    /// <param name="pitch">12音階率</param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Pitch12ToFrequency<T>(T pitch) where T : IFloatingPointIeee754<T>
        => T.Pow(GenericValue<T>.N2, (pitch - GenericValue<T>.BasePitch12) / GenericValue<T>.OctaveTones12) * GenericValue<T>.BaseFrequency;
}
