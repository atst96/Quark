using System.Runtime.CompilerServices;

namespace Quark.ImageRender;

public class RenderScaleInfo
{
    /// <summary>
    /// スケーリング
    /// </summary>
    private readonly double _scale;

    public RenderScaleInfo(double displayScale)
    {
        this._scale = displayScale;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ToDisplayScaling(double value)
        => (int)Math.Round(value * this._scale);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public (int x, int y) ToDisplayScaling(double x, double y)
        => (this.ToDisplayScaling(x), this.ToDisplayScaling(y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ToDisplayScaling(int value)
        => (int)Math.Round(value * this._scale);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ToRenderImageScaling(double value)
        => (int)Math.Round(value / this._scale);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public (int, int) ToRenderImageScaling(double x, double y)
        => (this.ToRenderImageScaling(x), this.ToRenderImageScaling(y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ToRenderImageScaling(int value)
        => (int)Math.Round(value / this._scale);
}
