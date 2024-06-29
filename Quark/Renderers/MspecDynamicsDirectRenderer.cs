using System;
using System.Numerics;
using Avalonia;
using Avalonia.Media;
using Quark.ImageRender;
using Quark.Projects.Tracks;
using Quark.Utils;
using Quark.Extensions;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Quark.Renderers;

/// <summary>
/// ダイナミクス値(生の値)の描画クラス
/// </summary>
/// <typeparam name="TPhrase">フレーズの型</typeparam>
/// <typeparam name="TNumber">値の型</typeparam>
internal class MspecDynamicsDirectRenderer<TPhrase, TNumber> : DynamicsRenderer
    where TPhrase : IMspecDynamicsPhrase<TNumber>
    where TNumber : IFloatingPointIeee754<TNumber>
{
    /// <summary>トラック</summary>
    private IMspecDynamicsPhraseTrack<TPhrase, TNumber> _track;

    /// <summary>下限値</summary>
    private readonly (TNumber Raw, TNumber Linear) _lower;
    /// <summary>上限値</summary>
    private readonly (TNumber Raw, TNumber Linear) _upper;

    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="track"></param>
    public MspecDynamicsDirectRenderer(IMspecDynamicsPhraseTrack<TPhrase, TNumber> track)
    {
        TNumber lower = TNumber.CreateTruncating(-6.0);
        TNumber upper = TNumber.CreateTruncating(1.0);

        this._track = track;
        this._lower = (lower, TNumber.Zero);
        this._upper = (upper, ToLinear(upper - lower));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T ToLinear<T>(T value) where T : IFloatingPointIeee754<T>
        => NeutrinoUtil.MspecToLinear(value);

    /// <inheritdoc/>
    public override void Render(DrawingContext drawingContext, RenderInfoCommon renderInfo)
    {
        const int dimensions = 100;

        var area = renderInfo.ScreenLayout.DynamicsArea;
        if (area == null)
            return;

        // 背景の塗りつぶし
        drawingContext.DrawRectangle(Brushes.Black, null, new Rect(0, 0, area.Width, area.Height));

        var rangeScoreInfo = renderInfo.RangeScoreRenderInfo;
        var rangeInfo = renderInfo.RenderRange;

        var renderLayout = renderInfo.ScreenLayout;

        var scaling = renderLayout.Scaling;

        TNumber lower = this._lower.Raw;

        // 描画開始・終了位置
        int beginTime = rangeInfo.BeginTime;
        int endTime = rangeInfo.EndTime;

        // フレームの描画範囲
        int beginFrameIdx = NeutrinoUtil.MsToFrameIndex(beginTime);
        int endFrameIdx = beginFrameIdx + rangeInfo.FramesCount;
        int frames = endFrameIdx - beginFrameIdx;

        (int renderWidth, int renderHeight) = area.Size;

        // 描画対象のフレーズ情報
        var phrases = this._track.Phrases;
        var dynamicsGroups = phrases
            .WithinRange(beginTime, endTime).Where(p => p.Mspec is { Length: > 0 })
            .EnumerateAboveThresholdRanges(p => p.Mspec!, lower, dimensions);

        int offsetMs = NeutrinoUtil.FrameIndexToMs(beginFrameIdx) - beginTime;

        var list = new List<(Point[] avg, Point[] min, Point[] max)>(phrases.Length);
        var list2 = new List<DrawPointRangeInfo>();

        // 描画情報
        double penWidth = 1.5;
        var origWaveformPen = DrawingUtils.CreatePen(Brushes.Red, penWidth);
        var editedWaveformPen = DrawingUtils.CreatePen(Brushes.Magenta, penWidth);
        var editingWaveformPen = DrawingUtils.CreatePen(Brushes.Blue, penWidth);

        double GetY(TNumber value)
            => double.CreateTruncating(TNumber.One - ((value - this._lower.Linear) / this._upper.Linear)) * renderHeight;

        foreach (var dynamics in dynamicsGroups)
        {
            int count = dynamics.Duration;
            var editedPoints = new Point[count];
            var origPoints = new Point[count];
            var minPoints = new Point[count];
            var maxPoints = new Point[count];
            int pointsIdx = 0;

            var phrase = dynamics.Phrase;
            TNumber[] origMspec = phrase.Mspec!;
            TNumber[]? editingDynamics = phrase.EditingDynamics;
            TNumber[]? editedDynamics = phrase.EditedDynamics!;

            // 描画開始／終了インデックス
            (int beginIdx, int endIdx) = DrawUtil.GetDrawRange(dynamics.AbsoluteBeginIndex, dynamics.Duration, beginFrameIdx, endFrameIdx, 0);

            if (beginIdx >= endIdx)
                continue;

            RenderStartInfo? beginTiming = null;

            int f = beginIdx - dynamics.PhraseBeginFrameIdx;

            int length = endIdx - beginIdx;
            for (int idx = 0; idx < length; ++idx, ++pointsIdx)
            {
                int frameIdx = idx + f;

                (TNumber min, TNumber max, TNumber average) = origMspec.AsSpan(frameIdx * dimensions, dimensions).MinMaxAvg();

                RenderValueType currentRenderType;
                TNumber target;

                {
                    // 未編集の値
                    TNumber origValue = origMspec.AsSpan(frameIdx * dimensions, dimensions).Average();

                    if (editingDynamics != null && !TNumber.IsNaN(editingDynamics[idx]))
                        (currentRenderType, target) = (RenderValueType.Editing, editingDynamics[idx]);
                    else if (!TNumber.IsNaN(editedDynamics[idx]))
                        (currentRenderType, target) = (RenderValueType.Edited, editedDynamics[idx]);
                    else
                        (currentRenderType, target) = (RenderValueType.NotEdit, average);

                    float x = renderLayout.GetRenderPosXFromTime(offsetMs + NeutrinoUtil.FrameIndexToMs(frameIdx + dynamics.PhraseBeginFrameIdx) - beginTime);

                    origPoints[pointsIdx] = new(x, GetY(ToLinear(average - lower)));
                    editedPoints[pointsIdx] = new(x, GetY(ToLinear(target - lower)));
                    minPoints[pointsIdx] = new(x, GetY(ToLinear(min - lower)));
                    maxPoints[pointsIdx] = new(x, GetY(ToLinear(max - lower)));
                }

                if (beginTiming != null)
                {
                    if (beginTiming.RenderType != currentRenderType)
                    {
                        int begin = beginTiming.BeginPointIdx;
                        list2.Add(new(beginTiming.RenderType, editedPoints, begin, pointsIdx) { OrigPoints = origPoints });

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
                list2.Add(new(beginTiming.RenderType, editedPoints, beginTiming.BeginPointIdx, length) { OrigPoints = origPoints });

            if (pointsIdx > 0)
            {
                var range = 0..pointsIdx;
                list.Add((origPoints[range], minPoints[range], maxPoints[range]));
            }
        }

        {
            int offsetX = renderLayout.GetRenderPosXFromTime(offsetMs);

            var rangeBrush = new Pen(new SolidColorBrush(Colors.LightGray, .4));
            var origBrush = new Pen(new SolidColorBrush(Colors.SkyBlue));
            var origBeforeBrush = new Pen(new SolidColorBrush(Colors.SkyBlue, .4));
            var editedBrush = new Pen(Brushes.DeepSkyBlue);
            var editingBrush = new Pen(Brushes.DeepPink);

            // 最小値・最大値を描画
            drawingContext.DrawGeometry(null, rangeBrush,
                DrawingUtils.CreateGeometry([
                    ..list.Select(p => DrawingUtils.CreateFigure(p.min)),
                    ..list.Select(p => DrawingUtils.CreateFigure(p.max))
                ]));

            if (list.Count > 0)
            {
                DrawPolyline(drawingContext, list2, RenderValueType.Editing, editingBrush);
                if (this.IsDrawOriginal)
                    DrawBeforeChangePolyline(drawingContext, list2, origBeforeBrush);
                DrawPolyline(drawingContext, list2, RenderValueType.Edited, editedBrush);
                DrawPolyline(drawingContext, list2, RenderValueType.NotEdit, origBrush);
            }
        }
        //}
    }
}
