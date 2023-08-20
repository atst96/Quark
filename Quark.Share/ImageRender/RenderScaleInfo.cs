using System.Runtime.CompilerServices;
using Quark.Utils;

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

    /// <summary>
    /// スケール化する。(100px/120% -> 83px)
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public double ToScaled(double value)
        => value / this._scale;

    /// <summary>
    /// スケール化を解除する。(100px/120% -> 120px)
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public int ToUnscaled(int value)
        => (int)(value * this._scale);

    /// <summary>
    /// スケール化解除する。(100.0px/120% -> 120.0px)
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public double ToUnscaled(double value)
        => value * this._scale;
}
