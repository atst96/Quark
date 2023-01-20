using System;
using System.Runtime.CompilerServices;

namespace Quark.Controls;

internal class ScalingConverter
{
    /// <summary>
    /// スケーリング
    /// </summary>
    private double _scaling;

    public ScalingConverter(double displayScaling)
    {
        this._scaling = displayScaling;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ToDisplayScaling(double value)
        => (int)Math.Round(value * this._scaling);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ToDisplayScaling(int value)
        => (int)Math.Round(value * this._scaling);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ToRenderImageScaling(double value)
        => (int)Math.Round(value / this._scaling);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ToRenderImageScaling(int value)
        => (int)Math.Round(value / this._scaling);
}
