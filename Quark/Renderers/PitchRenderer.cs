using System;
using System.Collections.Generic;
using System.Diagnostics;
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

internal static class PitchRenderer
{
    /// <summary>
    /// ピッチのレンダラを生成する
    /// </summary>
    /// <typeparam name="T">ピッチの型情報</typeparam>
    /// <param name="track">トラック</param>
    /// <returns></returns>
    public static IPitchRenderer Create(INeutrinoTrack track)
        => track switch
        {
            NeutrinoV1Track v1 => new PitchRenderer<double>(v1),
            NeutrinoV2Track v2 => new PitchRenderer<float>(v2),
            _ => throw new NotSupportedException(),
        };
}

internal class PitchRenderer<T> : IPitchRenderer
    where T : IFloatingPointIeee754<T>
{
    public IF0PhraseTrack<T> _track;

    public PitchRenderer(IF0PhraseTrack<T> track)
    {
        this._track = track;
    }

    /// <summary>
    /// ピッチを描画する
    /// </summary>
    /// <param name="drawingContext">描画コンテキスト</param>
    /// <param name="renderInfo"></param>
    public void Render(DrawingContext drawingContext, RenderInfoCommon renderInfo)
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
            .EnumerateAboveThresholdRanges();

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
            T[] f0 = phrase.F0!;
            T[] editedF0 = phrase.EditedF0!;
            T[] editingF0 = phrase.EditingF0!;

            // 実際に描画開始／終了するフレームのインデックス
            (int renderFrameBeginIdx, int renderFrameEndIdx) = DrawUtil.GetDrawRange(
                pitchRange.AbsoluteBeginIndex, pitchRange.Duration,
                beginFrameIdx, endFrameIdx, 0);

            if (renderFrameBeginIdx >= renderFrameEndIdx)
            {
                Debug.WriteLine($"Beg: {renderFrameBeginIdx}, End: {renderFrameEndIdx}");
                continue;
            }

            CurrentRenderInfo? beginTiming = null;

            int f = renderFrameBeginIdx - pitchRange.PhraseBeginFrameIdx;

            int length = renderFrameEndIdx - renderFrameBeginIdx;
            if (length > 0)
            {
                for (int idx = 0; idx < length; ++idx, ++pointsIdx)
                {
                    int frameIdx = idx + f;
                    int x = layout.GetRenderPosXFromTime(frameToMsOffset + NeutrinoUtil.FrameIndexToMs(frameIdx + pitchRange.PhraseBeginFrameIdx) - rangeBeginTime);

                    LineRenderType currentRenderType;
                    T[] target;

                    if (editingF0 != null && !T.IsNaN(editingF0[frameIdx]))
                        // 編集中
                        (currentRenderType, target) = (LineRenderType.Editing, editingF0);
                    else if (!T.IsNaN(editedF0[frameIdx]))
                        // 編集済み
                        (currentRenderType, target) = (LineRenderType.Edited, editedF0);
                    else
                        // 未編集
                        (currentRenderType, target) = (LineRenderType.NotEdit, f0);

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
            DrawPitches(drawingContext, list, LineRenderType.Editing, editingWaveformPen);
            DrawPitches(drawingContext, list, LineRenderType.Edited, editedWaveformPen);
            DrawPitches(drawingContext, list, LineRenderType.NotEdit, origWaveformPen);
        }
    }

    /// <summary>
    /// ピッチを描画する
    /// </summary>
    /// <param name="drawingContext">描画コンテキスト</param>
    /// <param name="ranges"></param>
    /// <param name="type">描画するタイプ</param>
    /// <param name="pen">描画用の<see cref="Pen"/></param>
    private static void DrawPitches(DrawingContext drawingContext, IReadOnlyList<DrawPointRangeInfo> ranges, LineRenderType type, Pen pen)
    {
        var figures = new List<PathFigure>(ranges.Count);

        for (int idx = 0; idx < ranges.Count; ++idx)
        {
            var range = ranges[idx];

            if (range.Type != type)
                continue;

            (_, var points, int beginIdx, int endIdx) = range;

            // 前後のピッチと隣接している場合は、連続して描画できるように前後のピッチも含める
            if (idx > 0)
            {
                // 前のピッチと隣接している場合は、前のピッチも含めて描画する
                var prev = ranges[idx - 1];
                if (prev.EndFrameIdx == beginIdx)
                    --beginIdx;
            }

            if (idx < (ranges.Count - 1))
            {
                // 後のピッチと隣接している場合は、後のピッチも含めて描画する
                var next = ranges[idx + 1];
                if ((endIdx + 1) == next.BeginFrameIdx)
                    ++endIdx;
            }

            figures.Add(CreateFigure(points, beginIdx, endIdx));
        }

        if (figures.Count > 0)
            drawingContext.DrawGeometry(null, pen, new PathGeometry() { Figures = [.. figures] });
    }

    /// <summary>
    /// PathFigureを生成する
    /// </summary>
    /// <param name="points"></param>
    /// <param name="beginIdx"></param>
    /// <param name="endIdx"></param>
    /// <returns></returns>
    protected static PathFigure CreateFigure(Point[] points, int beginIdx, int endIdx)
        => new()
        {
            IsClosed = false,
            StartPoint = points[beginIdx],
            Segments = [new PolyLineSegment(new ArraySegment<Point>(points)[(beginIdx + 1)..endIdx])]
        };

    /// <summary>
    /// 波形描画用の<see cref="Pen"/>を生成する。
    /// </summary>
    /// <param name="brush">描画色ブラシ</param>
    /// <param name="width">描画幅</param>
    /// <returns></returns>
    protected static Pen CreatePen(IBrush brush, double width)
        => new(brush, width, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);

    private record CurrentRenderInfo(int BeginPointIdx, LineRenderType RenderType);
}
