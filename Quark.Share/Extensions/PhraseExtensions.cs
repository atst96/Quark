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

    public static IEnumerable<PhraseValueRange<IF0Phrase<T>, T>> EnumerateAboveThresholdRanges<T>(this IEnumerable<IF0Phrase<T>> source)
        where T : IFloatingPointIeee754<T>
        => source.SelectMany(p => PhraseUtils.EnumerateAboveThresholdRanges(p, p.F0, T.Zero, 1, NeutrinoUtil.MsToFrameIndex(p.BeginTime)));
}
