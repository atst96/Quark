using System;

namespace Quark.Controls;

internal class RenderInfo
{
    public ScalingConverter Scaling { get; }
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

    public float WidthStretch { get; }
    public float HeightStretch { get; }

    public static class RenderConfig
    {
        /// <summary>描画するキー数</summary>
        public const int KeyCount = 88;

        /// <summary>初期のキー描画幅</summary>
        public const int DefaultKeyHeight = 12;

        public const int DefaultRulerHeight = 24;
    }

    public RenderInfo(ScalingConverter scaling, int renderWidth, float widthStretch = 0.8f, float heightStretch = 1.0f)
    {
        this.Scaling = scaling;
        this.RenderWidth = renderWidth;
        this.ImageWidth = scaling.ToRenderImageScaling(renderWidth);
        this.KeyHeight = RenderConfig.DefaultKeyHeight;
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
        => (int)Math.Ceiling((double)this.RenderWidth / this.WidthStretch);
}
