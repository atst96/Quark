using SkiaSharp;

namespace Quark.Controls;

/// <summary>
/// レイアウト計算時の座標
/// </summary>
/// <param name="X">X座標</param>
/// <param name="Y">Y座標</param>
public record class LayoutPoint(int X, int Y)
{
    /// <summary>
    /// オフセット値を加えた座標を取得する。
    /// </summary>
    /// <param name="x">X方向のオフセット値</param>
    /// <param name="y">Y方向のオフセット値</param>
    /// <returns></returns>
    public LayoutPoint GetOffset(int x, int y) => new(this.X + x, this.Y + y);

    /// <summary>SKRectを生成する。</summary>
    /// <returns></returns>
    public SKPoint ToSKPoint() => new(this.X, this.Y);
}
