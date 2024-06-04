using Avalonia;

namespace Quark.Renderers;

/// <summary>
/// 座標配列の描画範囲情報
/// </summary>
/// <param name="Type">描画タイプ</param>
/// <param name="Points">描画座標</param>
/// <param name="BeginFrameIdx">描画開始インデックス</param>
/// <param name="EndFrameIdx">描画終了インデックス</param>
public readonly record struct DrawPointRangeInfo(
    LineRenderType Type, Point[] Points, int BeginFrameIdx, int EndFrameIdx);
