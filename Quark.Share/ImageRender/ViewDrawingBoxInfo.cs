using Quark.ImageRender;

namespace Quark.Controls;

/// <summary>
/// 音響情報を描画するための情報
/// スケーリング100%時の1pxを1msとする。
/// </summary>
public class ViewDrawingBoxInfo
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

    public int UnscaledDisplayScorePosY { get; }

    public int DisplayScorePosY { get; }

    public int UnscaledDisplayScoreHeight { get; }

    public int RenderDisplayScoreHeight { get; }

    /// <summary>1鍵あたりの高さ</summary>
    public int KeyHeight { get; }

    /// <summary>ルーラの高さ</summary>
    public int UnscaledRulerHeight { get; }

    /// <summary>描画時のルーラの高さ</summary>
    public int RenderRulerHeight { get; }

    public int DynamicsPosY { get; }

    public int DynamicsDisplayPosY { get; }

    public int UnscaledDynamicsHeight { get; }

    public int DynamicRenderHeight { get; }

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

    public ViewDrawingBoxInfo(RenderScaleInfo scaling, int scaledDisplayWidth, int scaledDisplayHeight, int keyHeight, int renderWidth, double widthStretch = 0.8f, double heightStretch = 1.0f)
    {
        this.Scaling = scaling;
        this.UnscaledDisplayWidth = scaling.ToRenderImageScaling(scaledDisplayWidth);
        this.UnscaledDisplayHeight = scaling.ToRenderImageScaling(scaledDisplayHeight);
        this.RenderDisplayWidth = scaledDisplayWidth;
        this.RenderDisplayHeight = scaledDisplayHeight;
        this.KeyHeight = keyHeight;
        this.UnscaledWidth = scaling.ToRenderImageScaling(renderWidth);
        this.RenderWidth = renderWidth;

        // ルーラの高さを計算(固定)
        this.UnscaledRulerHeight = RenderConfig.DefaultRulerHeight;
        this.RenderRulerHeight = scaling.ToDisplayScaling(this.UnscaledRulerHeight);

        // 譜面全体の大きさを計算
        this.UnscaledScoreHeight = keyHeight * RenderConfig.KeyCount;
        this.RenderScoreHeight = scaling.ToDisplayScaling(this.UnscaledScoreHeight);

        // 譜面描画の高さを計算
        this.UnscaledDisplayScorePosY = this.UnscaledRulerHeight;
        this.DisplayScorePosY = this.RenderRulerHeight;

        int minimumSocreHeight = 100;
        int extendAreaHeight = 150;


        {
            // スコア配置位置
            this.DisplayScorePosY = this.RenderRulerHeight;
            this.UnscaledDisplayScorePosY = this.UnscaledRulerHeight;

            int dmsh = scaling.ToDisplayScaling(minimumSocreHeight);
            int earhd = scaling.ToDisplayScaling(extendAreaHeight);

            int mainDisplayHeight = Math.Max(0, this.RenderDisplayHeight - this.RenderRulerHeight);

            // スコア描画高計算
            if (mainDisplayHeight < dmsh)
                this.RenderDisplayScoreHeight = mainDisplayHeight;
            else if (mainDisplayHeight <= (dmsh + earhd))
                this.RenderDisplayScoreHeight = dmsh;
            else
                this.RenderDisplayScoreHeight = mainDisplayHeight - earhd;

            this.UnscaledDisplayScoreHeight = scaling.ToRenderImageScaling(this.RenderDisplayScoreHeight);

            // ダイナミクス描画位置
            this.DynamicsPosY = this.UnscaledDisplayScorePosY + this.UnscaledDisplayScoreHeight;
            this.DynamicsDisplayPosY = this.DisplayScorePosY + this.RenderDisplayScoreHeight;

            // ダイナミクス描画高
            this.DynamicRenderHeight = Math.Max(0, mainDisplayHeight - this.RenderDisplayScoreHeight);
            this.UnscaledDynamicsHeight = scaling.ToRenderImageScaling(this.DynamicRenderHeight);
        }

        this.WidthStretch = widthStretch;
        this.HeightStretch = heightStretch;
    }

    public int GetRenderFrames()
        => (int)Math.Ceiling((decimal)this.RenderWidth / RenderConfig.FramePeriod / (decimal)this.WidthStretch);

    public int GetDrawScoreHeight(double height)
        => this.Scaling.ToRenderImageScaling(height) - this.UnscaledRulerHeight;
}
