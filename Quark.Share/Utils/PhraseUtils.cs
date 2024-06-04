using System.Numerics;
using Quark.Projects.Tracks;

namespace Quark.Utils;
public static class PhraseUtils
{
    /// <summary>
    /// フレーズにおける
    /// </summary>
    /// <typeparam name="TPhrase"></typeparam>
    /// <typeparam name="TElement"></typeparam>
    /// <param name="BeginIndex"></param>
    /// <param name="EndIndex"></param>
    /// <param name="PhraseBeginFrameIdx"></param>
    public class PhraseValueRange<TPhrase, TElement>(TPhrase Phrase, int BeginIndex, int EndIndex, int PhraseBeginFrameIdx)
    public record PhraseValueRange<TPhrase, TElement>(TPhrase Phrase, int BeginIndex, int EndIndex, int PhraseBeginFrameIdx)
        where TElement : INumber<TElement>
    {
        public TPhrase Phrase { get; } = Phrase;

        public int BeginIndex { get; } = BeginIndex;

        public int EndIndex { get; } = EndIndex;

        public int Duration { get; } = EndIndex - BeginIndex + 1;

        public int PhraseBeginFrameIdx { get; } = PhraseBeginFrameIdx;

        public int AbsoluteBeginIndex { get; } = PhraseBeginFrameIdx + BeginIndex;

        public int AbsoluteEndIndex { get; } = PhraseBeginFrameIdx + EndIndex;
    }

    /// <summary>
    /// <paramref name="threashold"/>以下を除外した配列要素のインデックスの範囲をを列挙する
    /// </summary>
    /// <typeparam name="TElement"></typeparam>
    /// <typeparam name="TPhrase"></typeparam>
    /// <param name="values"></param>
    /// <param name="threashold"></param>
    /// <param name="dimension"></param>
    /// <param name="phraseBeginFrameIdx"></param>
    /// <returns></returns>
    public static IEnumerable<PhraseValueRange<TPhrase, TElement>> EnumerateAboveThresholdRanges<TPhrase, TElement>(TPhrase phrase, TElement[]? values, TElement threashold, int dimension, int phraseBeginFrameIdx)
        where TPhrase : INeutrinoPhrase
        where TElement : INumber<TElement>
    {
        if (values is null)
            yield break;

        int length = values.Length / dimension;

        bool isDetected = false;
        int detectBeginIdx = 0;

        for (int idx = 0; idx < length; ++idx)
        {
            if (values[idx * dimension] <= threashold)
            {
                if (isDetected)
                {
                    int endIdx = idx - 1;
                    if (endIdx > detectBeginIdx)
                    {
                        yield return new(phrase, detectBeginIdx, endIdx, phraseBeginFrameIdx);
                    }

                    isDetected = false;
                }

                continue;
            }

            if (!isDetected)
            {
                (isDetected, detectBeginIdx) = (true, idx);
            }
        }

        if (isDetected)
        {
            int endIndex = length - 1;
            if (endIndex > detectBeginIdx)
            {
                yield return new(phrase, detectBeginIdx, endIndex, phraseBeginFrameIdx);
            }
        }
    }
}
