using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;

namespace Quark.Utils;

/// <summary>
/// 描画処理のUtilクラス
/// </summary>
public static class DrawingUtils
{
    public static PathGeometry CreateGeometry(params IEnumerable<PathFigure> figures)
        => new() { Figures = [.. figures] };

    /// <summary>
    /// PathFigureを生成する
    /// </summary>
    /// <param name="points"></param>
    /// <param name="beginIdx"></param>
    /// <param name="endIdx"></param>
    /// <returns></returns>
    public static PathFigure CreateFigure(Point[] points)
        => new()
        {
            IsClosed = false,
            StartPoint = points[0],
            Segments = [new PolyLineSegment(new ArraySegment<Point>(points)[1..])]
        };

    /// <summary>
    /// PathFigureを生成する
    /// </summary>
    /// <param name="points">描画位置情報</param>
    /// <param name="beginIdx"></param>
    /// <param name="endIdx"></param>
    /// <returns></returns>
    public static PathFigure CreateFigure(Point[] points, int beginIdx, int endIdx)
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
    public static Pen CreatePen(IBrush brush, double width)
        => new(brush, width, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);

    public static SolidColorBrush WithAlpha(this SolidColorBrush brush, double opacity)
        => new(brush.Color, opacity);
}
