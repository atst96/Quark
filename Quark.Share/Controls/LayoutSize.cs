using SkiaSharp;

namespace Quark.Controls;

/// <summary>
/// レイアウト計算時のオブジェクトのサイズ
/// </summary>
/// <param name="Width">幅</param>
/// <param name="Height">高さ</param>
public record class LayoutSize(int Width, int Height)
{
    /// <summary>SKSizeを生成する。</summary>
    /// <returns></returns>
    public SKSize ToSKSize() => new(this.Width, this.Height);
}
