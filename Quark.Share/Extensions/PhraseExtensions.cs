using System.Numerics;
using Quark.Projects.Tracks;
using Quark.Utils;
using static Quark.Utils.PhraseUtils;

namespace Quark.Extensions;

/// <summary>
/// フレーズに関する拡張メソッド
/// </summary>
public static class PhraseExtensions
{
    /// <summary>
    /// <paramref name="beginTime"/>から<paramref name="endTime"/>までの範囲に含まれるフレーズを列挙する
    /// </summary>
    /// <typeparam name="T">フレーズ</typeparam>
    /// <param name="source">列挙元</param>
    /// <param name="beginTime">開始時間</param>
    /// <param name="endTime">終了時間</param>
    /// <returns>フレーズ情報</returns>
    public static IEnumerable<T> WithinRange<T>(this IEnumerable<T> source, int beginTime, int endTime) where T : INeutrinoPhrase
        => source.Where(p => beginTime <= p.EndTime && p.BeginTime <= endTime);

    /// <summary>
    /// フレーズで定義されている値配列のうち、閾値を超える範囲を列挙する
    /// </summary>
    /// <typeparam name="TPhrase">フレーズの型</typeparam>
    /// <typeparam name="TNumber">値の型</typeparam>
    /// <param name="source">フレーズ情報</param>
    /// <param name="selector">フレーズ内の値のセレクタ</param>
    /// <param name="threshold">閾値</param>
    /// <param name="dimensions">値の1フレームあたりの次元数</param>
    /// <returns></returns>
    public static IEnumerable<PhraseValueRange<TPhrase, TNumber>> EnumerateAboveThresholdRanges<TPhrase, TNumber>(
            this IEnumerable<TPhrase> source, Func<TPhrase, TNumber[]> selector, TNumber threshold, int dimensions = 1)
        where TPhrase : INeutrinoPhrase
        where TNumber : IFloatingPointIeee754<TNumber>
        => source.SelectMany(p => PhraseUtils.EnumerateAboveThresholdRanges(p, selector(p), threshold, dimensions, NeutrinoUtil.MsToFrameIndex(p.BeginTime)));

    /// <summary>
    /// フレーズで定義されているF0値の配列のうち、閾値を超える範囲を列挙する
    /// </summary>
    /// <typeparam name="TPhrase">フレーズの型</typeparam>
    /// <typeparam name="TNumber">値の型</typeparam>
    /// <param name="source">フレーズ情報</param>
    /// <returns></returns>
    public static IEnumerable<PhraseValueRange<TPhrase, TNumber>> EnumerateAboveThresholdRangesF0<TPhrase, TNumber>(this IEnumerable<TPhrase> source)
        where TPhrase : IF0Phrase<TNumber>
        where TNumber : IFloatingPointIeee754<TNumber>
        => source.SelectMany(p => PhraseUtils.EnumerateAboveThresholdRanges(p, p.F0, TNumber.Zero, 1, NeutrinoUtil.MsToFrameIndex(p.BeginTime)));
}
