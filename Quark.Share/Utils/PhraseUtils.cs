using System.Numerics;

namespace Quark.Utils;
public static class PhraseUtils
{
    /// <summary>
    /// フレーズにおける
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="BeginIndex"></param>
    /// <param name="EndIndex"></param>
    /// <param name="PhraseBeginFrameIdx"></param>
    /// <param name="Values"></param>
    public record PhraseValueRange<T>(int BeginIndex, int EndIndex, int PhraseBeginFrameIdx, T[] Values)
        where T : INumber<T>
    {
        public int TotalBeginIndex
            => this.PhraseBeginFrameIdx + this.BeginIndex;
        public int TotalEndIndex
            => this.PhraseBeginFrameIdx + this.EndIndex;
    }

    /// <summary>
    /// <paramref name="lower"/>以下を除外した要素のインデックスの範囲をを列挙する
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="values"></param>
    /// <param name="lower"></param>
    /// <param name="dimension"></param>
    /// <param name="phraseBeginFrameIdx"></param>
    /// <returns></returns>
    public static IEnumerable<PhraseValueRange<T>> EnumerateGreaterThanForLowerRanges<T>(T[] values, T lower, int dimension, int phraseBeginFrameIdx)
        where T : INumber<T>
    {
        int length = values.Length / dimension;

        bool isDetected = false;
        int detectBeginIdx = 0;

        for (int idx = 0; idx < length; ++idx)
        {
            if (values[idx * dimension] <= lower)
            {
                if (isDetected)
                {
                    int endIdx = idx - 1;
                    if (endIdx > detectBeginIdx)
                    {
                        yield return new(detectBeginIdx, endIdx, phraseBeginFrameIdx, values);
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
                yield return new(detectBeginIdx, endIndex, phraseBeginFrameIdx, values);
            }
        }
    }
}
