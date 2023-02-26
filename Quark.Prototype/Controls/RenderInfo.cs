using System;

namespace Quark.Controls;

/// <summary>
/// スケーリング100%時の1pxを1msとする。
/// </summary>
internal class RenderInfo
{
    public RenderScaleInfo Scaling { get; }
    /// <summary>
    /// スケーリング100%時の画像サイズ(幅)
    /// </summary>
    public int ImageWidth { get; }
    /// <summary>
    /// スケーリング100%時の画像サイズ(高さ)
    /// </summary>
    public int ImageHeight { get; }
    public int RenderWidth { get; }
    public int RenderHeight { get; }

    public int KeyHeight { get; }

    public int RulerHeight { get; }

    public int RenderRulerHeight { get; }

    public int ScoreWidth { get; }
    public int ScoreHeight { get; }

    public int ScoreRenderWidth { get; }
    public int ScoreRenderHeight { get; }

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

    public RenderInfo(RenderScaleInfo scaling, int keyHeight, int renderWidth, double widthStretch = 0.8f, double heightStretch = 1.0f)
    {
        this.Scaling = scaling;
        this.RenderWidth = renderWidth;
        this.ImageWidth = scaling.ToRenderImageScaling(renderWidth);
        this.KeyHeight = keyHeight;
        this.ImageHeight = this.KeyHeight * RenderConfig.KeyCount;
        this.RenderHeight = scaling.ToDisplayScaling(this.ImageHeight);
        this.RulerHeight = RenderConfig.DefaultRulerHeight;
        this.RenderRulerHeight = scaling.ToDisplayScaling(this.RulerHeight);
        this.ScoreWidth = this.ImageWidth;
        this.ScoreHeight = this.KeyHeight * RenderConfig.KeyCount;
        this.ScoreRenderWidth = this.RenderWidth;
        this.ScoreRenderHeight = scaling.ToDisplayScaling(this.ScoreHeight);
        this.WidthStretch = widthStretch;
        this.HeightStretch = heightStretch;
    }

    public int GetRenderFrames()
        => (int)Math.Ceiling((decimal)this.RenderWidth / RenderConfig.FramePeriod / (decimal)this.WidthStretch);

    public int GetDrawScoreHeight(double height)
        => this.Scaling.ToRenderImageScaling(height) - this.RulerHeight;
}
