﻿using System;
using System.Runtime.CompilerServices;

namespace Quark.Controls;

internal class RenderScaleInfo
{
    /// <summary>
    /// スケーリング
    /// </summary>
    private double _scale;

    public RenderScaleInfo(double displayScale)
    {
        this._scale = displayScale;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ToDisplayScaling(double value)
        => (int)Math.Round(value * this._scale);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ToDisplayScaling(int value)
        => (int)Math.Round(value * this._scale);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ToRenderImageScaling(double value)
        => (int)Math.Round(value / this._scale);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ToRenderImageScaling(int value)
        => (int)Math.Round(value / this._scale);
}