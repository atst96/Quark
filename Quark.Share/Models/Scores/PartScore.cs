using System.Numerics;
using static Quark.Models.MusicXML.MusicXmlPhrase;

namespace Quark.Models.Scores;

public record PartScore(
    int BeginMeasureTime,
    LinkedList<TempoInfo> Tempos,
    LinkedList<TimeSignature> TimeSignatures,
    LinkedList<Frame> Phrases)
{
    private const int Unit = 1000 / 200;

    /// <summary>
    /// 指定範囲内に含まれる楽譜情報を取得する
    /// </summary>
    /// <param name="beginIdx"></param>
    /// <param name="endIdx"></param>
    /// <returns>開始位置/終了位置が範囲外であっても、区間として被っていれば本情報に含まれる。</returns>
    public PartScore GetRangeInfo(int beginIdx, int endIdx)
    {
        var tempos = GetRangeElement(this.Tempos, f => (int)f.Frame, beginIdx, endIdx);
        var timeSignatures = GetRangeElement(this.TimeSignatures, f => (int)f.Frame, beginIdx, endIdx);
        var notes = GetRangeElement(this.Phrases, f => f.BeginFrame, f => f.EndFrame, beginIdx, endIdx);
        int beginMeasureTime = (int)((timeSignatures.First?.Value.Frame ?? 0) / Unit);

        return new(beginMeasureTime, tempos, timeSignatures, notes);
    }

    private static LinkedList<TElement> GetRangeElement<TElement, TTime>(LinkedList<TElement> collection, Func<TElement, TTime> getTimeFunc, TTime beginTime, TTime endTime)
        where TElement : class
        where TTime : INumber<TTime>
    {
        var results = new LinkedList<TElement>();

        // 前方範囲外の情報
        TElement? oorElement = null;
        foreach (var element in collection)
        {
            TTime elementTime = getTimeFunc.Invoke(element);

            if (beginTime <= elementTime || elementTime >= endTime)
            {
                // 範囲内要素の場合
                if (oorElement is not null)
                {
                    if (elementTime != beginTime)
                    {
                        // 開始位置と一致しない場合は前方要素を追加する
                        results.AddLast(oorElement);
                    }
                    oorElement = null;
                }

                results.AddLast(element);
            }
            else if (elementTime > endTime)
            {
                // 後方範囲外の場合
                break;
            }
            else
            {
                // 前方範囲外の場合
                // 範囲外要素情報を保持しておく
                oorElement = element;
            }
        }

        if (oorElement is not null)
        {
            // 範囲外要素が処理されていない場合は先頭に追加する
            // ※リストが1件しかない場合などを想定。
            results.AddFirst(oorElement);
        }

        return results;
    }

    private static LinkedList<TElement> GetRangeElement<TElement, TTime>(LinkedList<TElement> collection, Func<TElement, TTime> beginTimeFunc, Func<TElement, TTime> endTimeFunc, TTime beginTime, TTime endTime)
        where TElement : class
        where TTime : INumber<TTime>
    {
        var results = new LinkedList<TElement>();

        // 前方範囲外の情報
        TElement? oorElement = null;
        foreach (var element in collection)
        {
            TTime elementBeginTime = beginTimeFunc.Invoke(element);
            TTime elementEndTime = endTimeFunc.Invoke(element);

            if (endTime >= elementBeginTime || elementEndTime <= beginTime)
            {
                // 範囲内要素の場合
                if (oorElement is not null)
                {
                    if (elementBeginTime != beginTime)
                    {
                        // 開始位置と一致しない場合は前方要素を追加する
                        results.AddLast(oorElement);
                    }
                    oorElement = null;
                }

                results.AddLast(element);
            }
            else if (elementEndTime > endTime)
            {
                // 後方範囲外の場合
                break;
            }
            else
            {
                // 前方範囲外の場合
                // 範囲外要素情報を保持しておく
                oorElement = element;
            }
        }

        if (oorElement is not null)
        {
            // 範囲外要素が処理されていない場合は先頭に追加する
            // ※リストが1件しかない場合などを想定。
            results.AddFirst(oorElement);
        }

        return results;
    }
}
