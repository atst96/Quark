using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Quark.Drawing;
using Quark.Models.Scores;

namespace Quark.Utils;

/// <summary>
/// 譜面描画に関するUtilクラス
/// </summary>
public static class ScoreDrawingUtil
{
    /// <summary>全音符</summary>
    private const double Whole = 4d / 1;
    /// <summary>2分音符</summary>
    private const double Note2th = 4d / 2;
    /// <summary>4分音符</summary>
    private const double Note4th = 4d / 4;
    /// <summary>8分音符</summary>
    private const double Note8th = 4d / 8;
    /// <summary>16分音符</summary>
    private const double Note16th = 4d / 16;
    /// <summary>32分音符</summary>
    private const double Note32th = 4d / 32;
    /// <summary>64分音符</summary>
    private const double Note64th = 4d / 64;
    /// <summary>128分音符</summary>
    private const double Note128th = 4d / 128;

    private static readonly ImmutableArray<(double Coe, LineType Type)> NoteDurationListForLineType = ImmutableArray.Create(
        // 全音符
        (Whole, LineType.Whole),
        // 2分音符
        (Note2th, LineType.Note2th),
        // 4分音符
        (Note4th, LineType.Note4th),
        // 8分音符
        (Note8th, LineType.Note8th),
        // 16分音符
        (Note16th, LineType.Note16th),
        // 32分音符
        (Note32th, LineType.Note32th),
        // 64分音符
        (Note64th, LineType.Note64th),
        // 128分音符
        (Note128th, LineType.Note128th),

        // 三連符系
        // 2分三連符
        (ToTriplet(Note2th), LineType.Note2thTriplet),
        // 4分三連符
        (ToTriplet(Note4th), LineType.Note4thTriplet),
        // 8分三連符
        (ToTriplet(Note8th), LineType.Note8thTriplet),
        // 16分三連符
        (ToTriplet(Note16th), LineType.Note16thTriplet),
        // 32分三連符
        (ToTriplet(Note32th), LineType.Note32thTriplet),
        // 64分三連符
        (ToTriplet(Note64th), LineType.Note64thTriplet),

        // 付点系
        // 2分付点
        (ToDotted(Note2th), LineType.Note2thDotted),
        // 4分付点
        (ToDotted(Note4th), LineType.Note4thDotted),
        // 8分付点
        (ToDotted(Note8th), LineType.Note8thDotted),
        // 16分付点
        (ToDotted(Note16th), LineType.Note16thDotted),
        // 32分付点
        (ToDotted(Note32th), LineType.Note32thDotted),
        // 64分付点
        (ToDotted(Note64th), LineType.Note64thDotted)
    );

    /// <summary>音符の尺を付点付きにする</summary>
    /// <param name="duration">元の音符の尺</param>
    /// <returns>付点音符の尺</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ToDotted(double duration) => duration * 1.5d;

    /// <summary>音符の尺を三連符にする</summary>
    /// <param name="duration">元の音符の尺</param>
    /// <returns>三連符の尺</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ToTriplet(double duration) => duration * 4.0d / 3.0d;

    /// <summary>罫線の種別を計算する</summary>
    /// <param name="duration">元の音符の尺</param>
    /// <param name="count">小節内の繰り返し数</param>
    /// <returns>罫線の種別</returns>
    private static LineType? GetLineType(double duration, int count)
    {
        if (count == 0) // 最初は小節
            return LineType.Measure;

        // 小節上の位置
        // ※この処理内ではテンポ変更を考慮しなくてよい
        double positionInMeasure = duration * count;

        foreach (var noteDuration in NoteDurationListForLineType)
        {
            // 罫線の種別分繰り返す

            // 小節の先頭から指定位置までの尺が罫線種別の尺でぴったり割り切れるのなら、その罫線種別を返す
            double div = positionInMeasure / noteDuration.Coe;
            if (div == Math.Floor(div))
            {
                return noteDuration.Type;
            }
        }

        return null;
    }

    /// <summary>
    /// 罫線の位置を計算する
    /// </summary>
    /// <param name="score">譜面情報</param>
    /// <param name="beginTime">開始時間</param>
    /// <param name="endTime">終了時間</param>
    /// <param name="lineType">罫線種別</param>
    /// <returns></returns>
    public static VerticalLineInfo[] GetVerticalLines(PartScore score, decimal beginTime, decimal endTime, LineType lineType)
    {
        var list = new LinkedList<VerticalLineInfo>();

        var tempos = score.Tempos;

        var measureTimes = ScoreUtil.GetMeasureTimes(beginTime, endTime, score);

        var currentTempoNode = tempos.First!;
        while (currentTempoNode != null)
        {
            var nextTempoNode = currentTempoNode.Next;
            if (nextTempoNode is not null && measureTimes[0] > nextTempoNode.Value.Time)
            {
                currentTempoNode = nextTempoNode;
            }
            else
            {
                break;
            }
        }

        // 1音あたりの長さ比率を計算
        decimal noteDurationCoe = 0xFF & (ushort)lineType;
        if (lineType.HasFlag(LineType.Dotted))
        {
            // 付点音符は1.5倍する
            noteDurationCoe /= 1.5m;
        }
        else if (lineType.HasFlag(LineType.Triplet))
        {
            // 連符は1.3333...倍する
            noteDurationCoe = noteDurationCoe * 3.0m / 4.0m;
        }

        double lineDuration = GetLineTypeDuration(lineType);

        for (int idx = 1; idx < measureTimes.Length; ++idx)
        {
            decimal measureBeginTime = measureTimes[idx - 1];
            decimal measureDuration = measureTimes[idx - 0] - measureBeginTime;

            var nextTempoNode = currentTempoNode!.Next;
            if (nextTempoNode is not null && measureBeginTime > nextTempoNode.Value.Time)
            {
                currentTempoNode = nextTempoNode;
            }

            // 計算中の小節の長さ(拍の長さを足し合わせたもの)
            decimal calculateBeatDuration = 0.0m;

            // 音符1つあたりの尺(ms)
            decimal noteLength = 60 * 1000 * (4.0m / noteDurationCoe);

            // 線数
            int count = 0;

            while (calculateBeatDuration < measureDuration)
            {
                //  1小節の末尾まで繰り返す
                decimal currentPerTime = measureBeginTime + calculateBeatDuration;

                // 拍の長さ
                decimal beatDuration = CalcBeatDuration(currentPerTime, noteLength, ref currentTempoNode, ref nextTempoNode);

                list.AddLast(new VerticalLineInfo(currentPerTime, GetLineType(lineDuration, count) ?? lineType));

                // 現在時間に泊の尺を追加
                calculateBeatDuration += beatDuration;
                ++count;
            }
        }

        return list.ToArray();
    }

    /// <summary></summary>
    /// <param name="type">音符の種別</param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double GetLineTypeDuration(LineType type)
    {
        double duration = 4.0d / (0xFF & (ushort)type);

        if (type.HasFlag(LineType.Dotted))
            return ToDotted(duration);
        else if (type.HasFlag(LineType.Triplet))
            return ToTriplet(duration);
        else
            return duration;
    }

    /// <summary>1音の長さを計算する</summary>
    /// <param name="score">譜面情報</param>
    /// <param name="beginTime">現在時間</param>
    /// <param name="lineType">罫線種別</param>
    /// <returns></returns>
    public static decimal GetNoteDuration(PartScore score, decimal beginTime, LineType lineType)
    {
        // 直近のテンポ情報を取得する
        var currentTempoNode = score.Tempos.First!;
        while (currentTempoNode.Next is not null)
        {
            var nextNode = currentTempoNode.Next;
            if ((int)nextNode.Value.Time <= beginTime)
                currentTempoNode = nextNode;
            else
                break;
        }

        decimal noteLength = 60 * 1000 * (decimal)GetLineTypeDuration(lineType);
        var nextTempoNode = currentTempoNode.Next;

        // 拍の長さ
        return CalcBeatDuration(beginTime, noteLength, ref currentTempoNode, ref nextTempoNode);
    }



    /// <summary>1音の長さを計算する</summary>
    /// <param name="score">譜面情報</param>
    /// <param name="beginTime">現在時間</param>
    /// <param name="lineType">罫線種別</param>
    /// <param name="includeTime">尺計算に含める時間</param>
    /// <returns></returns>
    public static decimal GetNoteDuration(PartScore score, decimal beginTime, LineType lineType, decimal includeTime)
    {
        // 直近のテンポ情報を取得する
        var currentTempoNode = score.Tempos.First!;

        decimal totalDuration = 0m;
        //int noteCount = 0;

        decimal noteLength = 60 * 1000 * (decimal)GetLineTypeDuration(lineType);

        // TODO: 計算量が多そうなので最適化する
        do
        {
            // 現在の開始時間を算出
            decimal currentBeginTime = beginTime + totalDuration;

            // テンポリストのノードを調整
            while (currentTempoNode.Next is not null)
            {
                var nextNode = currentTempoNode.Next;
                if ((int)nextNode.Value.Time <= (int)currentBeginTime)
                    currentTempoNode = nextNode;
                else
                    break;
            }

            var nextTempoNode = currentTempoNode.Next;

            // 拍の長さ
            decimal singleNoteDuration = CalcBeatDuration(currentBeginTime, noteLength, ref currentTempoNode, ref nextTempoNode);
            totalDuration += singleNoteDuration;
            //++noteCount;
        }
        while ((int)(beginTime + totalDuration) < (int)includeTime);

        // return (totalDuration, noteCount);
        return totalDuration;
    }

    /// <summary>1拍の長さを計算する</summary>
    /// <param name="beginTime">開始時刻</param>
    /// <param name="noteLength">1拍の長さ</param>
    /// <param name="currentTempoNode">現在のテンポ情報</param>
    /// <param name="syncNextTempoNode">同期用次テンポ情報</param>
    /// <returns></returns>
    private static decimal CalcBeatDuration(decimal beginTime, decimal noteLength, ref LinkedListNode<TempoInfo> currentTempoNode, ref LinkedListNode<TempoInfo>? syncNextTempoNode)
    {
        decimal beatDuration = 0m;

        decimal progress = 0.0m;
        while (progress <= 1.0m)
        {
            // 拍の末尾まで繰り返す
            // 同じ拍内で複数回のテンポ変更がある場合を考慮している
            decimal currentTempo = (decimal)currentTempoNode.Value.Tempo;

            // 解析中の拍の長さの残りの部分を予測する
            decimal candidateRemaining = noteLength * (1.0m - progress) / currentTempo;

            var nextTempoNode = currentTempoNode.Next;
            if (nextTempoNode is null || (beginTime + candidateRemaining) <= nextTempoNode.Value.Time)
            {
                // 以下のいずれかの条件
                // ・次のテンポ変更がない
                // ・予測した拍の終わり時間が次のテンポ変更時間以前

                // 拍の長さの解析を終わる
                beatDuration += candidateRemaining;
                progress = 1.0m;
                break;
            }

            decimal nextTime = nextTempoNode.Value.Time;

            // 現在時間から変更された位置までを拍の長さに含める
            decimal changeDuration = nextTime - beginTime;
            beatDuration += changeDuration;

            // 変更された区間が現在の拍の始まりから何%にあたるかを計算し、解析率に加える
            decimal changeCoe = changeDuration * currentTempo / noteLength;
            progress += changeCoe;

            beginTime = nextTime;
            currentTempoNode = nextTempoNode;
            syncNextTempoNode = nextTempoNode.Next;
        }

        return beatDuration;
    }
}
