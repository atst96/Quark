using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using Quark.ImageRender;
using Quark.Utils;

namespace Quark.Renderers;

/// <summary>
/// 描画処理の基底クラス
/// </summary>
internal abstract class RendererBase : IVisualRenderer
{
    /// <inheritdoc/>
    public bool IsDrawOriginal { get; set; }

    /// <inheritdoc/>
    public virtual void Render(DrawingContext drawingContext, RenderInfoCommon renderInfo) => throw new System.NotImplementedException();

    /// <summary>
    /// 指定<paramref name="type"/>の数値を描画する
    /// </summary>
    /// <param name="drawingContext">描画コンテキスト</param>
    /// <param name="ranges"></param>
    /// <param name="type">描画するタイプ</param>
    /// <param name="pen">描画用の<see cref="Pen"/></param>
    public static void DrawPolyline(DrawingContext drawingContext, IReadOnlyList<DrawPointRangeInfo> ranges, RenderValueType type, Pen pen)
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

            figures.Add(DrawingUtils.CreateFigure(points, beginIdx, endIdx));
        }

        if (figures.Count > 0)
            drawingContext.DrawGeometry(null, pen, DrawingUtils.CreateGeometry(figures));
    }

    /// <summary>
    /// 変更前の数値を描画する
    /// </summary>
    /// <param name="drawingContext">描画コンテキスト</param>
    /// <param name="ranges"></param>
    /// <param name="pen">描画用の<see cref="Pen"/></param>
    public static void DrawBeforeChangePolyline(DrawingContext drawingContext, IReadOnlyList<DrawPointRangeInfo> ranges, Pen pen)
    {
        var figures = new List<PathFigure>(ranges.Count);

        foreach (var range in ranges.Where(i => i.OrigPoints != null).GroupBy(i => i.OrigPoints!))
        {
            // 描画対象の開始位置
            int beginIdx = -1;

            var items = range.ToArray();
            for (int idx = 0; idx < items.Length; ++idx)
            {
                var item = items[idx];

                if (beginIdx == -1)
                {
                    // 描画開始位置が未検出

                    // 現在の描画情報が編集済み以外であれば対象とする
                    if (item.Type != RenderValueType.NotEdit)
                    {
                        beginIdx = item.BeginFrameIdx;
                        if (idx > 0)
                        {
                            // 前の区間と隣接している場合は、直前の最後の値を含めて描画する
                            if (items[idx - 1].EndFrameIdx == beginIdx)
                                --beginIdx;
                        }
                    }
                    continue;
                }
                else
                {
                    // 何かヒットした要素が存在する場合
                    if (item.Type == RenderValueType.NotEdit)
                    {
                        // 編集済みの区間が終わった場合は描画情報を追加する
                        int endIdx = item.BeginFrameIdx;
                        figures.Add(DrawingUtils.CreateFigure(range.Key, beginIdx, endIdx));

                        beginIdx = -1;
                    }
                    else
                    {
                        // 編集済みの区間が続いていれば何もしない
                    }
                }
            }

            if (beginIdx != -1)
            {
                // 未処理の区間が残っている場合は描画情報を追加する
                figures.Add(DrawingUtils.CreateFigure(range.Key, beginIdx, items[^1].EndFrameIdx));
            }
        }

        if (figures.Count > 0)
            drawingContext.DrawGeometry(null, pen, DrawingUtils.CreateGeometry(figures));
    }
}
