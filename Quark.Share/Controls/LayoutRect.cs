using System.Runtime.CompilerServices;
using SkiaSharp;

namespace Quark.Controls;

/// <summary>
/// レイアウト計算時に使用する矩形
/// </summary>
/// <param name="X">X座標</param>
/// <param name="Y">Y座標</param>
/// <param name="Width">オブジェクトの幅</param>
/// <param name="Height">オブジェクトの高さ</param>
public record class LayoutRect(int X, int Y, int Width, int Height)
{
    /// <summary>オブジェクトの座標</summary>
    public LayoutPoint Position { get; } = new LayoutPoint(X, Y);

    /// <summary>オブジェクトの大きさ</summary>
    public LayoutSize Size { get; } = new LayoutSize(Width, Height);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsContains(LayoutPoint point)
        => this.IsContains(point.X, point.Y);

    public bool IsContains(int x, int y)
        => this.X <= x && x <= (this.X + this.Width) && this.Y <= y && y <= (this.Y + this.Height);

    public bool IsContainsX(double x) => this.X <= x && x <= (this.X + this.Width);

    public bool IsContainsY(double y) => this.Y <= y && y <= (this.Y + this.Height);

    public int RelativeX(int x)
        => x - this.X;

    public double RelativeX(double x) => x - this.X;

    public double RelativeY(double y) => y - this.Y;

    public SKPoint ToSKPoint() => new(this.X, this.Y);

    public SKSize ToSKSize() => new(this.Width, this.Height);

    public SKRect ToSKRect() => SKRect.Create(this.X, this.Y, this.Width, this.Height);

    public LayoutRect Extent(LayoutRect? extentArea)
    {
        if (extentArea == null)
            return this;

        int x = Math.Min(this.X, extentArea.X);
        int y = Math.Min(this.Y, extentArea.Y);

        int r = Math.Max(this.X + this.Width, extentArea.X + extentArea.Width);
        int b = Math.Max(this.Y + this.Height, extentArea.Y + extentArea.Height);

        return new LayoutRect(x, y, r - x, b - y);
    }
}
