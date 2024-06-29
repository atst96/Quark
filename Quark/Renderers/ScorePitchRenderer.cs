using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Avalonia;
using Avalonia.Media;
using Quark.Converters;
using Quark.Extensions;
using Quark.ImageRender;
using Quark.Projects.Tracks;
using Quark.Utils;

namespace Quark.Renderers;

internal class ScorePitchRenderer<TPhrase, TNumber> : PitchRenderer
    where TPhrase : IF0Phrase<TNumber>
    where TNumber : IFloatingPointIeee754<TNumber>
{
    public IF0PhraseTrack<TPhrase, TNumber> _track;

    public ScorePitchRenderer(IF0PhraseTrack<TPhrase, TNumber> track)
    {
        this._track = track;
    }

    /// <summary>
    /// ピッチを描画する
    /// </summary>
    /// <param name="drawingContext">描画コンテキスト</param>
    /// <param name="renderInfo"></param>
    public override void Render(DrawingContext drawingContext, RenderInfoCommon renderInfo)
    {
        var renderRange = renderInfo.RenderRange;
        var layout = renderInfo.ScreenLayout;

        (int width, int height) = layout.ScoreImage.Size;
        float keyHeight = layout.PhysicalKeyHeight;

        // 描画範囲の開始・終了時間
        (int rangeBeginTime, int rangeEndTime) = (renderRange.BeginTime, renderRange.EndTime);

        // 描画範囲のフレーム
        int beginFrameIdx = NeutrinoUtil.MsToFrameIndex(rangeBeginTime);
        int endFrameIdx = beginFrameIdx + renderRange.FramesCount;

        // 描画情報
        double penWidth = 1.5;
        var backgroundPen = CreatePen(new SolidColorBrush(Colors.Black, 0.1), 3);
        var origWaveformPen = CreatePen(Brushes.Red, penWidth);
        var editedWaveformPen = CreatePen(Brushes.Magenta, penWidth);
        var editingWaveformPen = CreatePen(Brushes.Blue, penWidth);

        // 描画するピッチを取得する
        // 対象の各フレーズのピッチの配列において、ピッチが閾値(>0)を超える配列の範囲を列挙する。
        var pitchRanges = this._track.Phrases
            .WithinRange(rangeBeginTime, rangeEndTime).Where(p => p.F0 is { Length: > 0 })
            .EnumerateAboveThresholdRangesF0<TPhrase, TNumber>();

        // 縦位置の補正値。キーの中央を基準にする
        float pitchRenderYOffset = (float)keyHeight / 2;

        // フレーム(5ms)単位とミリ秒単位のずれを計算
        int frameToMsOffset = NeutrinoUtil.FrameIndexToMs(beginFrameIdx) - rangeBeginTime;

        var list = new List<DrawPointRangeInfo>();

        foreach (var pitchRange in pitchRanges)
        {
            var points = new Point[pitchRange.Duration];
            int pointsIdx = 0;
            int vScrollPosition = renderInfo.VScrollPosition;

            var phrase = pitchRange.Phrase;
            TNumber[] f0 = phrase.F0!;
            TNumber[] editedF0 = phrase.EditedF0!;
            TNumber[] editingF0 = phrase.EditingF0!;

            // 実際に描画開始／終了するフレームのインデックス
            (int renderFrameBeginIdx, int renderFrameEndIdx) = DrawUtil.GetDrawRange(
                pitchRange.AbsoluteBeginIndex, pitchRange.Duration,
                beginFrameIdx, endFrameIdx, 0);

            if (renderFrameBeginIdx >= renderFrameEndIdx)
                continue;

            RenderStartInfo? beginTiming = null;

            int f = renderFrameBeginIdx - pitchRange.PhraseBeginFrameIdx;

            int length = renderFrameEndIdx - renderFrameBeginIdx;
            if (length > 0)
            {
                for (int idx = 0; idx < length; ++idx, ++pointsIdx)
                {
                    int frameIdx = idx + f;
                    int x = layout.GetRenderPosXFromTime(frameToMsOffset + NeutrinoUtil.FrameIndexToMs(frameIdx + pitchRange.PhraseBeginFrameIdx) - rangeBeginTime);

                    RenderValueType currentRenderType;
                    TNumber[] target;

                    if (editingF0 != null && !TNumber.IsNaN(editingF0[frameIdx]))
                        // 編集中
                        (currentRenderType, target) = (RenderValueType.Editing, editingF0);
                    else if (!TNumber.IsNaN(editedF0[frameIdx]))
                        // 編集済み
                        (currentRenderType, target) = (RenderValueType.Edited, editedF0);
                    else
                        // 未編集
                        (currentRenderType, target) = (RenderValueType.NotEdit, f0);

                    points[pointsIdx] = new Point(x,
                        height - pitchRenderYOffset - (float.CreateTruncating(AudioDataConverter.FrequencyToPitch12(target[frameIdx])) * keyHeight) - vScrollPosition);

                    if (beginTiming != null)
                    {
                        if (beginTiming.RenderType != currentRenderType)
                        {
                            int begin = beginTiming.BeginPointIdx;
                            list.Add(new(beginTiming.RenderType, points, begin, pointsIdx));

                            beginTiming = new(pointsIdx, currentRenderType);
                            continue;
                        }
                    }
                    else
                    {
                        beginTiming = new(pointsIdx, currentRenderType);
                        continue;
                    }
                }

                if (beginTiming != null)
                    list.Add(new(beginTiming.RenderType, points, beginTiming.BeginPointIdx, length));
            }
        }

        if (list.Count > 0)
        {
            // DrawPitch2(drawingContext, list, backgroundPen);
            DrawPolyline(drawingContext, list, RenderValueType.Editing, editingWaveformPen);
            DrawPolyline(drawingContext, list, RenderValueType.Edited, editedWaveformPen);
            DrawPolyline(drawingContext, list, RenderValueType.NotEdit, origWaveformPen);
        }
    }

    /// <summary>
    /// 波形描画用の<see cref="Pen"/>を生成する。
    /// </summary>
    /// <param name="brush">描画色ブラシ</param>
    /// <param name="width">描画幅</param>
    /// <returns></returns>
    protected static Pen CreatePen(IBrush brush, double width)
        => new(brush, width, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
}
