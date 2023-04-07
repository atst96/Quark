using Quark.Models.Scores;

namespace Quark.Utils;

/// <summary>
/// 楽譜処理に関するUtilクラス
/// </summary>
public static class ScoreUtil
{
    /// <summary>
    /// 譜面情報から描画開始・終了位置の小節位置を解析する
    /// </summary>
    /// <param name="beginTime">描画開始位置</param>
    /// <param name="endTime">描画終了位置</param>
    /// <param name="score">譜面情報</param>
    /// <returns></returns>
    public static decimal[] GetMeasureTimes(decimal beginTime, decimal endTime, PartScore score)
    {
        var measureTimeList = new LinkedList<decimal>();

        var tempos = score.Tempos;
        var timeSignatures = score.TimeSignatures;

        var currentTempoNode = tempos.First!;
        var currentTimeSignatureNode = timeSignatures.First!;

        for (decimal currentMeasureTime = score.BeginMeasureTime; currentMeasureTime <= endTime;)
        {
            var nextTempoNode = currentTempoNode.Next;
            if (nextTempoNode is not null && (int)nextTempoNode.Value.Time <= (int)currentMeasureTime)
            {
                // 次の拍変更に到達
                currentTempoNode = nextTempoNode;
                nextTempoNode = nextTempoNode.Next;
            }

            var nextTimeSignatureNode = currentTimeSignatureNode.Next;
            if (nextTimeSignatureNode is not null && (int)nextTimeSignatureNode.Value.Time <= (int)currentMeasureTime)
            {
                // テンポ変更に到達
                currentTimeSignatureNode = nextTimeSignatureNode;
                currentMeasureTime = nextTimeSignatureNode.Value.Time;
            }

            var timeSignature = currentTimeSignatureNode.Value;

            const decimal BeatTypeLength4th = 4m;

            // 1小節分の4分音符あたりの長さ(ミリ秒)
            // 4拍子÷拍子記号の分母×拍数
            // 4/4拍子 → 1.0x4、 6/8拍子 → 0.5x8、 1/2拍子 → 2.0x1
            decimal beatsPer4thMs = 60 * 1000 * BeatTypeLength4th * timeSignature.Beats / timeSignature.BeatType;

            // 余分な計算の回避処置
            if (currentMeasureTime < beginTime)
            {
                // 最初の小節から描画位置まで複数小節ある場合は、計算回数を減らすためにその間の計算を飛ばす

                // 1小節分の長さ(ms)を予測する
                decimal skipDurationPerMeasure = beatsPer4thMs / (decimal)currentTempoNode.Value.Tempo;

                //  描画開始位置、テンポ変更、拍子変更のうち一番早いイベントの時間を探す
                decimal earliestTime = beginTime;

                if (nextTempoNode is not null && beginTime > nextTempoNode.Value.Time)
                    earliestTime = nextTempoNode.Value.Time;

                if (nextTimeSignatureNode is not null && beginTime > nextTimeSignatureNode.Value.Time)
                    earliestTime = nextTimeSignatureNode.Value.Time;

                // 一番早いイベントから計算を飛ばせる小節数が1つ以上あれば現在時間に加える
                decimal skipCount = Math.Floor((earliestTime - currentMeasureTime) / skipDurationPerMeasure);
                if (skipCount > 0)
                    currentMeasureTime += skipCount * skipDurationPerMeasure;
            }

            // 現在の小節がどの程度解析できたかを示す値(0～1.0)
            decimal measureProgress = 0.0m;
            // 現在の譜面上の時刻(ms)
            decimal currentTime = currentMeasureTime;

            // 現在の小節の長さ
            decimal duration = 0.0m;

            while (measureProgress <= 1.0m)
            {
                // 小節の末尾まで繰り返す
                // 同じ小節内で複数回のテンポ変更がある場合を考慮している

                decimal currentTempo = (decimal)currentTempoNode.Value.Tempo;

                // 解析中の小節の長さの残りの部分を予測する
                decimal candidateRemaining = beatsPer4thMs * (1.0m - measureProgress) / currentTempo;

                if (nextTempoNode is null || (currentTime + candidateRemaining) <= nextTempoNode.Value.Time)
                {
                    // 以下のいずれかの条件
                    // ・次のテンポ変更がない
                    // ・予測した小節の終わり時間が次のテンポ変更時間以前

                    // 小節の長さの解析をおしまいにする
                    duration += candidateRemaining;
                    measureProgress = 1.0m;
                    break;
                }

                decimal nextTime = nextTempoNode.Value.Time;

                // 現在時間から変更された位置までを小節の長さに含める
                decimal changedDuration = nextTime - currentTime;
                duration += changedDuration;

                // 変更された区間が現在の小節の始まりから何%にあたるかを計算し、解析率に加える
                decimal changeCoe = changedDuration / (beatsPer4thMs / currentTempo);
                measureProgress += changeCoe;

                currentTime = nextTime;
                currentTempoNode = nextTempoNode;
                nextTempoNode = nextTempoNode.Next;
            }

            // 小節リスト追加する
            decimal measureEndTime = currentMeasureTime + duration;
            if (beginTime < measureEndTime || (beginTime <= currentMeasureTime && currentMeasureTime <= endTime))
            {
                // 以下のいずれかの場合
                // ・小節の開始～終了時間が描画開始位置を跨いでいる
                // ・現在の小節の開始時間が描画範囲内
                measureTimeList.AddLast(currentMeasureTime);

                if (endTime < measureEndTime)
                {
                    // 小節の終了時間が描画範囲外の場合は終了時間を追加する
                    measureTimeList.AddLast(measureEndTime);
                }
            }

            // 現在時間に小節の尺を追加
            currentMeasureTime = measureEndTime;
        }

        return measureTimeList.ToArray();
    }
}
