using System.Runtime.CompilerServices;

namespace Quark.Converters;

/// <summary>
/// 音声データ変換クラス
/// </summary>
public static class AudioDataConverter
{
    /// <summary>基本周波数(Hz)</summary>
    public const double StandardPitch = 440;

    /// <summary>基本周波数(440Hz)におけるスケール値</summary>
    public const int BaseScale = 69;

    /// <summary>
    /// 周波数値を12音階律のスケールに変換する
    /// </summary>
    /// <param name="frequency">周波数</param>
    /// <returns>12音階率</returns>
    public static double FrequencyToScale(double frequency)
        // http://signalprocess.binarized.work/2019/03/26/convert_frequency_to_cent/
        => (12 * Math.Log2(frequency / StandardPitch)) + BaseScale;

    /// <summary>
    /// 周波数値を12音階律のスケールに変換する
    /// </summary>
    /// <param name="frequency">周波数</param>
    /// <returns>12音階率</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float FrequencyToScale(float frequency)
        => (float)FrequencyToScale((double)frequency);

    /// <summary>
    /// 12音階率スケールを周波数に変換する
    /// </summary>
    /// <param name="scale">12音階率洲ケース</param>
    /// <returns></returns>
    public static double ScaleToFrequency(double scale)
        => Math.Pow(2, (scale - BaseScale) / 12) * StandardPitch;
}
