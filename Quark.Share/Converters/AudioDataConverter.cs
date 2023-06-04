namespace Quark.Converters;

/// <summary>
/// 音声データ変換クラス
/// </summary>
public static class AudioDataConverter
{
    /// <summary>
    /// 周波数値を12音階律のスケールに変換する
    /// </summary>
    /// <param name="frequency">周波数</param>
    /// <returns>12音階率</returns>
    public static double FrequencyToScale(double frequency)
    {
        // http://signalprocess.binarized.work/2019/03/26/convert_frequency_to_cent/
        return (12 * Math.Log2(frequency / 440)) + 69;
    }

    /// <summary>
    /// 周波数値を12音階律のスケールに変換する
    /// </summary>
    /// <param name="frequency">周波数</param>
    /// <returns>12音階率</returns>
    public static float FrequencyToScale(float frequency)
    {
        // http://signalprocess.binarized.work/2019/03/26/convert_frequency_to_cent/
        return (float)(12 * Math.Log2(frequency / 440)) + 69;
    }
}
