using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using Quark.ImageRender;

namespace Quark.Controls;

/// <summary>
/// 音響情報を描画するための情報。
/// 本クラスで保持する値および計算は物理ピクセル単位とする。
/// ※論理100px・スケーリング120%の場合は、120pxtとなる。
/// </summary>
public class EditorRenderLayout
{
    /// <summary>画面スケーリング</summary>
    public RenderScaleInfo Scaling { get; }

    /// <summary>描画領域(幅)</summary>
    public int ScreenWidth { get; }

    /// <summary>描画領域(高さ)</summary>
    public int ScreenHeight { get; }

    /// <summary>描画設定</summary>
    public LayoutConfig Config { get; }

    /// <summary>編集モード</summary>
    public EditMode EditMode { get; }

    /// <summary>上部ルーラー領域</summary>
    public LayoutRect RulerArea { get; }

    /// <summary>ピアノロールの譜面部の描画領域</summary>
    public LayoutRect ScoreArea { get; }

    /// <summary>ピアノロールの鍵盤の描画領域</summary>
    public LayoutRect KeysArea { get; }

    /// <summary>ダイナミクスの描画有無</summary>
    [MemberNotNull(nameof(DynamicsArea))]
    public bool HasDynamicsArea { get; }

    /// <summary>ダイナミクスの描画領域</summary>
    public LayoutRect? DynamicsArea { get; } = null;

    /// <summary>譜面の描画サイズ(キャッシュ画像のサイズ)</summary>
    public LayoutRect ScoreImage { get; }

    /// <summary>鍵盤ひとつ辺りの高さ</summary>
    public int PhysicalKeyHeight { get; }

    /// <summary>横の拡大率</summary>
    public double WidthStretch { get; }

    /// <summary>縦の拡大率</summary>
    public double HeightStretch { get; }

    public static class RenderConfig
    {
        /// <summary>描画するキー数</summary>
        public const int KeyCount = 88;

        public const int DefaultRulerHeight = 24;
        public const int DefaultKeyWidth = 80;
        public const int DefaultExtentAreaHeight = 200;

        /// <summary>フレーム間隔(ms)</summary>
        public const int FramePeriod = 5;

        /// <summary>1秒あたりのフレーム数</summary>
        public const int FramesPerSecond = 1000 / FramePeriod;
    }

    public class LayoutConfig
    {
        public required int KeyAreaWidth { get; init; }

        public required int RulerHeight { get; init; }

        public required int ExtentAreaHeight { get; init; }
    }

    public EditorRenderLayout(RenderScaleInfo scaling, int screenWidth, int screenHeight, int keyHeight, int renderWidth, EditMode editMode, double widthStretch = 0.8f, double heightStretch = 1.0f)
    {
        this.Scaling = scaling;

        // TODO: ここから新しいプロパティ
        int width = this.ScreenWidth = screenWidth;
        int height = this.ScreenHeight = screenHeight;

        this.EditMode = editMode;
        var config = this.Config = /* config; */ new()
        {
            ExtentAreaHeight = scaling.ToUnscaled(RenderConfig.DefaultExtentAreaHeight),
            RulerHeight = scaling.ToUnscaled(RenderConfig.DefaultRulerHeight),
            KeyAreaWidth = scaling.ToUnscaled(RenderConfig.DefaultKeyWidth),
        };

        int rulerHeight = config.RulerHeight;
        int keysWidth = config.KeyAreaWidth;

        int mainAreaX = keysWidth;
        int mainAreaY = rulerHeight;
        int mainAreaWidth = UnderZero(width - mainAreaX);

        this.RulerArea = new(mainAreaX, 0, UnderZero(mainAreaWidth), scaling.ToUnscaled(rulerHeight));

        int extentHeight = 0;
        if (editMode == EditMode.AudioFeatures)
        {
            int remainingHeight = height - mainAreaY;
            extentHeight = Math.Min(config.ExtentAreaHeight, remainingHeight / 2);

            this.DynamicsArea = new(mainAreaX, UnderZero(height - extentHeight), mainAreaWidth, extentHeight);
        }

        int mainAreaHeight = height - mainAreaY - extentHeight;
        this.KeysArea = new(0, rulerHeight, keysWidth, mainAreaHeight);
        this.ScoreArea = new(mainAreaX, mainAreaY, mainAreaWidth, mainAreaHeight);

        this.HasDynamicsArea = this.DynamicsArea != null;

        var physicalKeyHeight = this.PhysicalKeyHeight = scaling.ToUnscaled(keyHeight);
        this.ScoreImage = new(-1, -1, this.ScoreArea.Width, physicalKeyHeight * RenderConfig.KeyCount);

        // TODO: ここから廃止予定のプロパティ

        //int minimumSocreHeight = 100;
        //int extendAreaHeight = 150;

        //{
        //    int dmsh = scaling.ToDisplayScaling(minimumSocreHeight);
        //    int earhd = scaling.ToDisplayScaling(extendAreaHeight);

        //    int mainDisplayHeight = Math.Max(0, this.ScreenHeight - this.ScoreArea.Y);

        //    // スコア描画高計算
        //    if (mainDisplayHeight < dmsh)
        //        this.RenderDisplayScoreHeight = mainDisplayHeight;
        //    else if (mainDisplayHeight <= (dmsh + earhd))
        //        this.RenderDisplayScoreHeight = dmsh;
        //    else
        //        this.RenderDisplayScoreHeight = mainDisplayHeight - earhd;
        //}

        this.WidthStretch = widthStretch;
        this.HeightStretch = heightStretch;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T UnderZero<T>(T value) where T : INumber<T>
        => value < T.Zero ? T.Zero : value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetRenderFrames()
        => this.GetRenderTimes() / RenderConfig.FramePeriod;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetRenderTimes()
        => (int)Math.Ceiling(this.ScoreArea.Width / (decimal)this.WidthStretch);

    /// <summary>
    /// 尺(<paramref name="durationMs"/>)を描画時の横幅(物理px)に変換する。
    /// </summary>
    /// <param name="durationMs">尺(ミリ秒)</param>
    /// <returns>描画幅(ms)</returns>
    public int GetRenderPosXFromTime(int durationMs)
        => (int)this.Scaling.ToUnscaled(durationMs * this.WidthStretch);

    /// <summary>
    /// 指定された座標が編集エリアに含まれるかどうかを取得する。
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    public bool IsContainsEditArea(LayoutPoint point)
        => this.ScoreArea.IsContains(point)
            || (this.HasDynamicsArea && this.DynamicsArea.IsContains(point));

    /// <summary>
    /// 指定された座標(Y)が編集エリアに含まれるかどうかを取得する。
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    public bool IsContainsEditAreaY(int y)
        => this.ScoreArea.IsContainsY(y)
            || (this.HasDynamicsArea && this.DynamicsArea.IsContainsY(y));
}
