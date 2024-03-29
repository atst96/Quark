﻿using System.Diagnostics;
using Quark.Converters;
using Quark.Extensions;
using Quark.Projects.Tracks;
using Quark.Utils;
using SkiaSharp;

namespace Quark.ImageRender.Score;

/// <summary>
/// 音程を描画する
/// </summary>
internal class PitchRendererV2
{
    public PitchRendererV2()
    {
    }

    public void Render(SKCanvas g, RenderInfoCommon ri)
    {
        var rangeScoreInfo = ri.RangeScoreRenderInfo;
        if (rangeScoreInfo == null)
            return;

        var rangeInfo = ri.RenderRange;
        var renderLayout = ri.ScreenLayout;

        if (ri.Track is not NeutrinoV2Track track)
            return;

        // 描画領域
        (int width, int height) = renderLayout.ScoreImage.Size;
        int keyHeight = renderLayout.PhysicalKeyHeight;

        // 描画開始・終了位置
        int beginTime = rangeInfo.BeginTime;
        int endTime = rangeInfo.EndTime;

        // フレームの描画範囲
        int beginFrameIdx = NeutrinoUtil.MsToFrameIndex(beginTime);
        int endFrameIdx = beginFrameIdx + rangeInfo.FramesCount;

        // 描画対象のフレーズ情報
        var targetPhrases = track.Phrases
            .Where(p => beginTime <= p.EndTime && p.BeginTime <= endTime);

        var pitches = targetPhrases
            .Where(p => p.F0 is not null)
            .SelectMany(p => PhraseUtils.EnumerateGreaterThanForLowerRanges(p, p.F0!, 0, 1, NeutrinoUtil.MsToFrameIndex(p.BeginTime)))
            .OrderBy(i => i.PhraseBeginFrameIdx + i.BeginIndex)
            .GroupingAdjacentRange(i => i.TotalBeginIndex, i => i.TotalEndIndex);

        float pitchOffset = (float)keyHeight / 2;

        int offsetMs = NeutrinoUtil.FrameIndexToMs(beginFrameIdx) - beginTime;

        foreach (var pitchGroup in pitches)
        {
            int count = pitchGroup.Last().TotalEndIndex - pitchGroup.First().TotalBeginIndex + 1;
            var origPoints = new SKPoint[count];
            var editedPoints = new SKPoint[count];
            int pointsIdx = 0;

            foreach (var pitch in pitchGroup)
            {
                var phrase = pitch.Phrase;
                float[] f0 = phrase.F0!;
                float[] editedF0 = phrase.GetEditingF0()!;

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
                    int x = renderLayout.GetRenderPosXFromTime(offsetMs + NeutrinoUtil.FrameIndexToMs(frameIdx + pitch.PhraseBeginFrameIdx) - beginTime);

                    origPoints[pointsIdx] = new SKPoint(x,
                        height - pitchOffset - ((float)AudioDataConverter.FrequencyToPitch12(f0[frameIdx]) * keyHeight));

                    editedPoints[pointsIdx] = new SKPoint(x,
                        height - pitchOffset - ((float)AudioDataConverter.FrequencyToPitch12(editedF0[frameIdx]) * keyHeight));

                    ++pointsIdx;
                }
            }

            if (pointsIdx > 0)
            {
                var range = 0..pointsIdx;
                g.DrawPoints(SKPointMode.Polygon, origPoints[range], new SKPaint { Color = SKColors.OrangeRed.WithAlpha(150), StrokeWidth = 1.2f, IsAntialias = true });
                g.DrawPoints(SKPointMode.Polygon, editedPoints[range], new SKPaint { Color = SKColors.Red, StrokeWidth = 1.2f, IsAntialias = true });
            }
        }
    }
}
