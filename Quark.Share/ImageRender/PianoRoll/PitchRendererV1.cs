using System.Diagnostics;
using Quark.Converters;
using Quark.Extensions;
using Quark.Projects.Tracks;
using Quark.Utils;
using SkiaSharp;
using static Quark.Controls.ViewDrawingBoxInfo;

namespace Quark.ImageRender.Score;

/// <summary>
/// 音程を描画する
/// </summary>
internal class PitchRendererV1
{
    private readonly RenderInfoCommon _renderInfo;

    public PitchRendererV1(RenderInfoCommon renderInfo)
    {
        this._renderInfo = renderInfo;
    }

    public void Render(SKCanvas g)
    {
        var ri = this._renderInfo;

        var rangeScoreInfo = ri.RangeScoreRenderInfo;
        if (rangeScoreInfo == null)
            return;

        var rangeInfo = ri.RenderRange;

        var renderInfo = ri.PartRenderInfo;

        var scaling = renderInfo.Scaling;

        const int Period = RenderConfig.FramePeriod;

        if (this._renderInfo.Track is not NeutrinoV1Track track)
            return;

        // 描画領域
        (int width, int height) = (renderInfo.UnscaledWidth, renderInfo.UnscaledScoreHeight);
        int keyHeight = renderInfo.KeyHeight;

        // 描画開始・終了位置
        int beginTime = rangeInfo.BeginTime;
        int endTime = rangeInfo.EndTime;

        // フレームの描画範囲
        int beginFrameIdx = beginTime / RenderConfig.FramePeriod;
        int endFrameIdx = beginFrameIdx + rangeInfo.FramesCount;

        // 描画対象のフレーズ情報
        var targetPhrases = track.Phrases
            .Where(p => beginTime <= p.EndTime && p.BeginTime <= endTime);

        var pitches = targetPhrases
            .Where(p => p.F0 is not null)
            .SelectMany(p => PhraseUtils.EnumerateGreaterThanForLowerRanges(p.F0!, 0, 1, p.BeginTime / Period))
            .OrderBy(i => i.PhraseBeginFrameIdx + i.BeginIndex)
            .GroupingAdjacentRange(i => i.TotalBeginIndex, i => i.TotalEndIndex);

        float pitchOffset = (float)keyHeight / 2;

        int offsetMs = beginFrameIdx * Period - beginTime;

        foreach (var pitchGroup in pitches)
        {
            var points = new SKPoint[pitchGroup.Last().TotalEndIndex - pitchGroup.First().TotalBeginIndex + 1];
            int pointsIdx = 0;

            foreach (var pitch in pitchGroup)
            {
                // 描画開始／終了インデックス
                (int beginIdx, int endIdx) = DrawUtil.GetDrawRange(
                    pitch.PhraseBeginFrameIdx + pitch.BeginIndex, pitch.EndIndex - pitch.BeginIndex + 1,
                    beginFrameIdx, endFrameIdx, 0);

                if (beginIdx >= endIdx)
                {
                    Debug.WriteLine($"Beg: {beginIdx}, End: {endIdx}");
                    continue;
                }

                int f = beginIdx - pitch.PhraseBeginFrameIdx;

                for (int idx = 0, length = endIdx - beginIdx; idx < length; ++idx)
                {
                    int frameIdx = idx + f;
                    points[pointsIdx++] = new SKPoint(
                        scaling.ToDisplayScaling((offsetMs + (frameIdx + pitch.PhraseBeginFrameIdx) * Period - beginTime) * renderInfo.WidthStretch),
                        scaling.ToDisplayScaling(height - pitchOffset - (float)AudioDataConverter.FrequencyToScale(pitch.Values[frameIdx]) * keyHeight));
                }
            }

            if (pointsIdx > 0)
            {
                g.DrawPoints(SKPointMode.Polygon, points[0..pointsIdx], new SKPaint { Color = SKColors.Red, StrokeWidth = 1.5f, IsAntialias = true });
            }
        }
    }
}
