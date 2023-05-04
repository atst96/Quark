using System;
using System.Windows;

namespace Quark.Controls;

/// <summary>
/// スケーリング100%時の1pxを1msとする。
/// </summary>
internal class RenderInfo
{
    public RenderScaleInfo Scaling { get; }

    public int UnscaledDisplayWidth { get; }
    public int UnscaledDisplayHeight { get; }

    public int RenderDisplayWidth { get; }
    public int RenderDisplayHeight { get; }

    /// <summary>スケーリング100%時の描画幅</summary>
    public int UnscaledWidth { get; }

    public int RenderWidth { get; }

    /// <summary>スケーリング100%時の譜面描画高</summary>
    public int UnscaledScoreHeight { get; }

    public int RenderScoreHeight { get; }

    /// <summary>1鍵あたりの高さ</summary>
    public int KeyHeight { get; }

    /// <summary>ルーラの高さ</summary>
    public int UnscaledRulerHeight { get; }

    /// <summary>描画時のルーラの高さ</summary>
    public int RenderRulerHeight { get; }

    public double WidthStretch { get; }

    public double HeightStretch { get; }

    public static class RenderConfig
    {
        /// <summary>描画するキー数</summary>
        public const int KeyCount = 88;

        public const int DefaultRulerHeight = 24;

        /// <summary>フレーム間隔(ms)</summary>
        public const int FramePeriod = 5;

        /// <summary>1秒あたりのフレーム数</summary>
        public const int FramesPerSecond = 1000 / FramePeriod;
    }

    public record Size(int Width, int Height)
    {
        /// <summary>分解代入</summary>
        /// <param name="width"></param>
        /// <param name="height"></param>
        public void Deconstruct(out int width, out int height)
            => (width, height) = (this.Width, this.Height);
    }

    public RenderInfo(RenderScaleInfo scaling, int keyHeight, int renderWidth, double widthStretch = 0.8f, double heightStretch = 1.0f)
    {
        this.Scaling = scaling;
        this.UnscaledDisplayWidth = scaling.ToRenderImageScaling(scaledDisplayWidth);
        this.UnscaledDisplayHeight = scaling.ToRenderImageScaling(scaledDisplayHeight);
        this.RenderDisplayWidth = scaledDisplayWidth;
        this.RenderDisplayHeight = scaledDisplayHeight;
        this.KeyHeight = keyHeight;
        this.UnscaledWidth = scaling.ToRenderImageScaling(renderWidth);
        this.UnscaledScoreHeight = keyHeight * RenderConfig.KeyCount;
        this.RenderWidth = renderWidth;
        this.RenderScoreHeight = scaling.ToDisplayScaling(this.UnscaledScoreHeight);
        this.UnscaledRulerHeight = RenderConfig.DefaultRulerHeight;
        this.RenderRulerHeight = scaling.ToDisplayScaling(this.UnscaledRulerHeight);
        this.WidthStretch = widthStretch;
        this.HeightStretch = heightStretch;
    }

    public int GetRenderFrames()
        => (int)Math.Ceiling((decimal)this.RenderWidth / RenderConfig.FramePeriod / (decimal)this.WidthStretch);

    public int GetDrawScoreHeight(double height)
        => this.Scaling.ToRenderImageScaling(height) - this.UnscaledRulerHeight;
}
