using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Quark.Extensions;
using Quark.Models.Scores;
using Quark.Projects.Tracks;
using Quark.Utils;
using SkiaSharp;
using static Quark.Controls.RenderInfo;

namespace Quark.Controls;

/// <summary>
/// PlotEditor.xaml の相互作用ロジック
/// 
/// MEMO:
/// 　タイムリニア式の描画方法で実装する。
/// 　　→ そのうち拍／小節リニアにも対応する。(Cubaseとかは拍／小節リニアがデフォルトになっている）
/// 　縦横の拡大率=100%、テンポ=100、4/4拍子の小節の場合に描画する間隔が340pxとなるようにする。
/// </summary>
public partial class PlotEditor : UserControl
{
    private const int FrameUnit = 1000 / 200;

    private const int KeyCount = 88;

    private const double MaxVScrollHeight = 1000;
    private double MaxHScrollHeight = 1000;

    private SKPaint _whiteKeyPaint = new SKPaint { Color = new SKColor(255, 255, 255) };
    private SKPaint _blackKeyPaint = new SKPaint { Color = new SKColor(230, 230, 230) };
    private SKPaint _whiteKeyGridPaint = new SKPaint { Color = new SKColor(230, 230, 230), StrokeWidth = 1 };

    public int KeyHeight = 12;
    private SKBitmap _renderImage;
    private SKBitmap _rulerImage;

    private bool _isLoaded = false;
    private long _framesCount = -1;
    private List<Class1> _pitches;
    private List<Class1> _dynamics;
    private PartScore _score;
    private PartScore _currentViewScore;
    private RenderScaleInfo _scaling;
    private RenderInfo _renderInfo;

    /// <summary>横伸長率の初期値</summary>
    private const double DefaultScaleX = 0.125;

    /// <summary>横伸長率のリスト</summary>
    public static readonly double[] HorizontalZoomLevels =
    {
        1.0, 0.8, 0.6, 0.4, 0.3, 0.2, 0.125,
        0.1, 0.08, 0.06, 0.04, 0.03, 0.02, 0.0125, 0.01,
    };

    private int _rulerHeight = 24;

    private SKPaint lyricsTypography = new(new SKFont(SKTypeface.FromFamilyName("MS UI Gothic"), 12));

    public PlotEditor()
    {
        this.InitializeComponent();

        this._scaling = new RenderScaleInfo(VisualTreeHelper.GetDpi(this).DpiScaleX);
    }

    /// <summary>
    /// コントロール読み込み完了時
    /// </summary>
    /// <param name="sender">イベント発火元</param>
    /// <param name="e">イベント情報</param>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        window.DpiChanged += this.OnDpiChanged;
    }

    /// <summary>
    /// 画面のDPI変更時
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnDpiChanged(object sender, DpiChangedEventArgs e)
    {
        this._scaling = new RenderScaleInfo(e.NewDpi.DpiScaleX);
        this.OnLayoutChanged();
    }

    /// <summary>トラック情報</summary>
    internal NeutrinoV1Track? Track
    {
        get => (NeutrinoV1Track)this.GetValue(TrackProperty);
        set => this.SetValue(TrackProperty, value);
    }

    /// <summary><see cref="Track"/>プロパティ</summary>
    public static readonly DependencyProperty TrackProperty =
        DependencyProperty.Register(nameof(Track), typeof(NeutrinoV1Track), typeof(PlotEditor), new PropertyMetadata(null, OnTrackPropertyChanged));

    /// <summary>シークバーの選択位置</summary>
    public TimeSpan SelectionTime
    {
        get => (TimeSpan)this.GetValue(SelectionTimeProperty);
        set => this.SetValue(SelectionTimeProperty, value);
    }

    /// <summary><see cref="SelectionTime"/>の依存関係プロパティ</summary>
    public static readonly DependencyProperty SelectionTimeProperty =
        DependencyProperty.Register(nameof(SelectionTime), typeof(TimeSpan), typeof(PlotEditor), new PropertyMetadata(TimeSpan.Zero, OnSelectionTimePropertyChanged));

    /// <summary>
    /// <seealso cref="SelectionTime"/>プロパティ変更時
    /// </summary>
    private static void OnSelectionTimePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((PlotEditor)d).RelocateSeekBar((TimeSpan)e.NewValue, (TimeSpan)e.OldValue);

    /// <summary>
    /// スクロール追従の有効／無効
    /// </summary>
    public bool IsAutoScroll
    {
        get => (bool)this.GetValue(IsAutoScrollProperty);
        set => this.SetValue(IsAutoScrollProperty, value);
    }

    /// <summary>
    /// <seealso cref="IsAutoScroll"/>の依存関係プロパティ
    /// </summary>
    public static readonly DependencyProperty IsAutoScrollProperty =
        DependencyProperty.Register(nameof(IsAutoScroll), typeof(bool), typeof(PlotEditor), new PropertyMetadata(false));

    /// <summary>横伸長率の一時変数</summary>
    private double _tempScaleX = DefaultScaleX;

    /// <summary>横方向のズームレベル</summary>
    public double ScaleX
    {
        get => (double)this.GetValue(ScaleXProperty);
        set => this.SetValue(ScaleXProperty, this._tempScaleX = value);
    }

    /// <summary><seealso cref="ScaleX"/>の依存関係プロパティ</summary>
    public static readonly DependencyProperty ScaleXProperty =
        DependencyProperty.Register(nameof(ScaleX), typeof(double), typeof(PlotEditor), new PropertyMetadata(DefaultScaleX, OnScaleXChanged));

    /// <summary>縦方向のズームレベル</summary>
    public double ScaleY
    {
        get => (double)this.GetValue(ScaleYProperty);
        set => this.SetValue(ScaleYProperty, value);
    }

    /// <summary><seealso cref="ScaleY"/>の依存関係プロパティ</summary>
    public static readonly DependencyProperty ScaleYProperty =
        DependencyProperty.Register(nameof(ScaleY), typeof(double), typeof(PlotEditor), new PropertyMetadata(1.0d, OnScaleYChanged));

    /// <summary>
    /// <seealso cref="Track"/>プロパティ変更時
    /// </summary>
    /// <param name="d">プロパティ変更時</param>
    /// <param name="e">イベント情報</param>
    private static void OnTrackPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PlotEditor editor)
        {
            if (e.NewValue is NeutrinoV1Track track)
            {
                editor.LoadTrack(track);
            }

            editor.UpdateScrollLayout();
            editor.RelocateSeekBar();
        }
    }

    /// <summary>
    /// <seealso cref="ScaleX"/>プロパティ変更時
    /// </summary>
    /// <param name="d">プロパティ変更要素</param>
    /// <param name="e">イベント情報</param>
    private static void OnScaleXChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var editor = (PlotEditor)d;
        if (editor.UpdateInternalScaleX((double)e.NewValue))
        {
            editor.OnLayoutChanged();
        }
    }

    /// <summary>
    /// 内部的に保持している横伸長率を更新する
    /// </summary>
    /// <param name="newScale"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool UpdateInternalScaleX(double newScale)
    {
        if (this._tempScaleX == newScale)
        {
            return false;
        }

        this._tempScaleX = newScale;
        return true;
    }

    /// <summary>
    /// <seealso cref="ScaleY"/>プロパティ変更時
    /// </summary>
    /// <param name="d">プロパティ変更要素</param>
    /// <param name="e">イベント情報</param>
    private static void OnScaleYChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var editor = (PlotEditor)d;
        editor.OnLayoutChanged();
    }

    /// <summary>
    /// トラック情報を読み込む
    /// </summary>
    /// <param name="track">トラック情報</param>
    private void LoadTrack(NeutrinoV1Track track)
    {
        var features = track.GetFeatures();

        // 楽譜情報解析
        this._score = MusicXmlUtil.Parse(track.MusicXml);
        this._pitches = Parse(features.F0!, 0.0f);
        this._dynamics = Parse(GetDynamicsFromMgc(features.Mgc!, features.F0!), -30d);
        this._framesCount = features.F0!.Length;
        this._isLoaded = true;

        this.Redraw();
    }

    /// <summary>スクロールバーの状態を更新する</summary>
    private void UpdateScrollLayout()
    {
        int dataLength = this.Track!.GetFeatures().F0!.Length;

        // ########## 縦スクロールの設定
        var vScrollBar = this.vScrollBar1;
        vScrollBar.Minimum = 0;
        vScrollBar.Maximum = MaxVScrollHeight;
        vScrollBar.LargeChange = 1;
        vScrollBar.ViewportSize = MaxVScrollHeight / 10;

        // ########## 横スクロールの設定
        long duration = (dataLength + 1) * FrameUnit;
        this.MaxHScrollHeight = duration;
        var hScrollBar = this.hScrollBar1;
        hScrollBar.Minimum = 0;
        hScrollBar.Maximum = this.MaxHScrollHeight;
        hScrollBar.ViewportSize = this.MaxHScrollHeight / 10;
    }

    /// <summary>
    /// 画面レイアウトの変更時
    /// </summary>
    public void OnLayoutChanged()
    {
        int renderWidth = (int)this.SKElement.CanvasSize.Width;

        this._renderInfo = new RenderInfo(this._scaling, renderWidth, this.ScaleX, 1.0f);
        this.Redraw();
        this.RelocateSeekBar();
    }

    /// <summary>
    /// シークバーの位置を補正する
    /// </summary>
    /// <param name="isRecursive"></param>
    private void RelocateSeekBar(bool isRecursive = false) => this.RelocateSeekBar(this.SelectionTime, isRecursive: isRecursive);

    /// <summary>
    /// シークバーの位置を補正する
    /// </summary>
    /// <param name="time">現在時刻</param>
    /// <param name="prevTime">前の時刻</param>
    /// <param name="isRecursive">再帰呼び出しフラグ</param>
    private void RelocateSeekBar(TimeSpan time, TimeSpan? prevTime = null, bool isRecursive = false)
    {
        long totalFrameCount = this._framesCount;

        var scaling = this._scaling;

        var renderInfo = this._renderInfo;

        // 描画領域
        int renderWidth = renderInfo.RenderWidth;

        // 開始フレーム位置
        int beginTime = this.GetRenderBeginFrameIdx() * RenderConfig.FramePeriod;
        int endTime = beginTime + (int)(scaling.ToRenderImageScaling(renderWidth) / this.ScaleX);
        int currentTime = (int)time.TotalMilliseconds;

        var lineElement = this.PART_SelectionTime;
        var renderElement = this.SKElement;
        if (beginTime <= currentTime && currentTime < endTime)
        {
            double x = scaling.ToDisplayScaling((currentTime - beginTime) * this.ScaleX);

            lineElement.X1 = x;
            lineElement.X2 = x;
            lineElement.Y1 = 0;
            lineElement.Y2 = renderElement.ActualHeight;

            lineElement.Visibility = Visibility.Visible;
        }
        else
        {
            bool isAutoScroll = this.IsAutoScroll;
            if (isAutoScroll)
            {
                double value;
                if (prevTime.HasValue && prevTime < time)
                {
                    // 前方向への移動
                    value = Math.Ceiling((time.TotalMilliseconds / (totalFrameCount * 5)) * MaxHScrollHeight);
                }
                else
                {
                    // 後方向への移動
                    value = Math.Ceiling(((time.TotalMilliseconds - (endTime - beginTime)) / (totalFrameCount * 5)) * MaxHScrollHeight);
                }

                if (!isRecursive)
                {
                    this.SetRenderBeginMs((int)value);
                    this.RelocateSeekBar(TimeSpan.FromMilliseconds(value), time, true);
                    this.Redraw();
                }

                return;
            }
            else
            {
                lineElement.Visibility = Visibility.Collapsed;
            }
        }
    }

    /// <summary>
    /// 12音階の画像を生成する
    /// </summary>
    /// <param name="width">画像幅</param>
    /// <param name="keyHeight">1音あたりの高さ</param>
    /// <param name="scaling">スケーリング情報</param>
    /// <returns></returns>
    private (SKBitmap bmp, int width, int height) CreatePianoOctaveBmp(int width, int keyHeight, RenderScaleInfo scaling)
    {
        const int keys = 12;

        int height = keyHeight * keys;
        int renderHeight = scaling.ToDisplayScaling(height);
        int renderWidth = scaling.ToDisplayScaling(width);
        int renderKeyHeight = scaling.ToDisplayScaling(keyHeight);

        var whiteKeyBrush = this._whiteKeyPaint;
        var whiteGridPen = this._whiteKeyGridPaint;
        var blackKeyBrush = this._blackKeyPaint;

        var image = new SKBitmap(renderWidth, renderHeight);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int GetYPos(int key) => renderHeight - ((key + 1) * renderKeyHeight);

        using (var g = new SKCanvas(image))
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void DrawRect(int key, SKPaint brush)
            {
                int y = GetYPos(key);
                g.DrawRect(new(0, y, renderWidth, renderKeyHeight + y), brush);
            }

            // ストライプの描画
            DrawRect(0, whiteKeyBrush); // C
            DrawRect(1, blackKeyBrush); // C#
            DrawRect(2, whiteKeyBrush); // D
            DrawRect(3, blackKeyBrush); // D#
            DrawRect(4, whiteKeyBrush); // E
            DrawRect(5, whiteKeyBrush); // F
            DrawRect(6, blackKeyBrush); // F#
            DrawRect(7, whiteKeyBrush); // G
            DrawRect(8, blackKeyBrush); // G#
            DrawRect(9, whiteKeyBrush); // A
            DrawRect(10, blackKeyBrush); // A#
            DrawRect(11, whiteKeyBrush); // B

            // 白鍵の境界を描画
            g.DrawLine(0, GetYPos(4), renderWidth, GetYPos(4), whiteGridPen);
            g.DrawLine(0, GetYPos(11), renderWidth, GetYPos(11), whiteGridPen);
        }

        return (image, width, height);
    }

    /// <summary>
    /// 描画領域のサイズを取得する
    /// </summary>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private (int width, int height) GetCanvasSize()
    {
        var size = this.SKElement.CanvasSize;

        return ((int)size.Width, (int)size.Height);
    }

    /// <summary>
    /// 描画する内容を再生成する
    /// </summary>
    private void UpdateRenderImage()
    {
        (int width, int height) = this.GetCanvasSize();
        if (width > 0 && height > 0)
        {
            using (this._renderImage)
            {
                var renderInfo = this._renderInfo = new RenderInfo(this._scaling, width, this.ScaleX, 1.0f);
                this._renderImage = this.CreateRenderImage(renderInfo);
                this._rulerImage = this.CreateRulerImage(renderInfo);
            }
        }
    }

    /// <summary>
    /// 画面を再描画する
    /// </summary>
    private void Redraw()
    {
        this.UpdateRenderImage();
        this.SKElement.InvalidateVisual();
    }

    /// <summary>
    /// 描画領域のサイズ変更時
    /// </summary>
    /// <param name="sender">イベント発火元</param>
    /// <param name="e">イベント情報</param>
    private void OnRenderSizeChanged(object sender, SizeChangedEventArgs e)
    {
        this.Redraw();
        this.RelocateSeekBar();
    }

    /// <summary>縦スクロール位置(0-100%)を取得する</summary>
    private double GetVerticalScrollCoe()
        => (double)this.vScrollBar1.Value / MaxVScrollHeight;

    /// <summary>描画開始時間</summary>
    private int _renderBeginTime = 0;

    /// <summary>描画開始時間を取得する</summary>
    /// <returns></returns>
    private int GetRenderBeginTimeMs()
        => this._renderBeginTime;

    /// <summary>描画開始フレーム番号を取得する</summary>
    /// <returns></returns>
    private int GetRenderBeginFrameIdx()
        => (int)Math.Ceiling((double)this.GetRenderBeginTimeMs() / RenderConfig.FramePeriod);

    /// <summary>描画開始位置を変更する</summary>
    /// <param name="time"></param>
    private void SetRenderBeginMs(int time)
    {
        time = Math.Max(0, time);

        this._renderBeginTime = time;
        if ((int)this.hScrollBar1.Value != time)
        {
            this.hScrollBar1.Value = time;
        }
    }

    /// <summary>描画開始位置を変更する</summary>
    /// <param name="time"></param>
    private void OnRenderBeginMsChanged(int time)
        => this._renderBeginTime = time;

    /// <summary>描画開始位置を変更する</summary>
    /// <param name="time"></param>
    private void SetRenderBeginMsOffset(int time)
        => this.SetRenderBeginMs(this.GetRenderBeginTimeMs() + time);

    /// <summary>描画開始位置を変更する</summary>
    /// <param name="time"></param>
    private void SetVerticalPosition(double time)
    {
        if ((int)this.vScrollBar1.Value != (int)time)
        {
            this.vScrollBar1.Value = time;
        }
    }

    /// <summary>描画開始位置を変更する</summary>
    /// <param name="time"></param>
    private void SetVerticalPositionOffset(double time)
        => this.SetVerticalPosition((int)(this.vScrollBar1.Value + time));

    /// <summary>
    /// 画面内容を描画する
    /// </summary>
    /// <param name="renderInfo">描画情報</param>
    /// <returns>描画内容</returns>
    private SKBitmap CreateRenderImage(RenderInfo renderInfo)
    {
        (int rulerHeight, var scaling) = (this._rulerHeight, this._scaling);

        int scoreHeight = renderInfo.ScoreHeight;

        // 描画領域
        int renderWidth = renderInfo.RenderWidth;
        int width = renderInfo.ImageWidth;
        int height = renderInfo.ImageHeight;
        int renderHeight = renderInfo.RenderHeight;

        // 予備フレーム数
        // 折れ線の前後が途切れないように前後1データ多めに描画しておく
        const int marginFrames = 1;

        int scoreYOffset = renderInfo.RenderRulerHeight;

        int scoreRenderWidth = renderWidth;
        int scoreWidth = width;
        int scoreRenderHeight = renderInfo.ScoreRenderWidth;

        var image = new SKBitmap(renderInfo.RenderWidth, renderInfo.RenderHeight);

        (var partImage, int octWidth, int octHeight) = CreatePianoOctaveBmp(100, KeyHeight, scaling);

        using (var g = new SKCanvas(image))
        {
            int imageWidth = octWidth;
            int imageHeight = octHeight;
            int offset = imageHeight - (height % imageHeight);

            int vCount = (int)Math.Ceiling((double)scoreHeight / imageHeight);
            int hCount = (int)Math.Ceiling((double)scoreWidth / imageWidth);

            int[] xList = Enumerable.Range(0, hCount)
                .Select(x => x * imageWidth)
                .ToArray();

            for (int yCount = 0; yCount < vCount; ++yCount)
            {
                int y = (yCount * imageHeight) - offset;

                foreach (int x in xList)
                {
                    g.DrawBitmap(partImage, scaling.ToDisplayScaling(x), scoreYOffset + scaling.ToDisplayScaling(y));
                }
            }

            if (this._isLoaded)
            {
                long totalFrameCount = this._framesCount;

                int offsetFrames = 1;

                // 描画するフレーム数
                int viewFrames = renderInfo.GetRenderFrames();
                int framesCount = viewFrames + (offsetFrames * 2);
                // float renderOffset =  viewFrames * this._frameWidth;

                // 開始フレーム位置
                int beginFrameIdx = this.GetRenderBeginFrameIdx();
                int endFrameIdx = beginFrameIdx + framesCount;

                // 描画開始位置のオフセットを計算
                int frameBasedTime = beginFrameIdx * RenderConfig.FramePeriod;
                int beginTime = (this.GetRenderBeginFrameIdx() * RenderConfig.FramePeriod) + RenderConfig.FramePeriod;
                int offsetX = frameBasedTime - beginTime;

                // スコアの描画
                var result = this._currentViewScore = this._score.GetRangeInfo(beginFrameIdx, endFrameIdx);
                var scores = result.Phrases.ToArray();
                {
                    for (int i = 0; i < scores.Length; ++i)
                    {
                        var score = scores[i];

                        int beginIndex = score.BeginFrame - beginFrameIdx;

                        float y = height - (float)(score.Pitch * KeyHeight);
                        var rect = SKRect.Create(
                            scaling.ToDisplayScaling((offsetX + beginIndex * RenderConfig.FramePeriod) * renderInfo.WidthStretch),
                            scoreYOffset + scaling.ToDisplayScaling(height - (score.Pitch * KeyHeight)),
                            scaling.ToDisplayScaling((score.EndFrame - score.BeginFrame) * renderInfo.WidthStretch * RenderConfig.FramePeriod),
                            scaling.ToDisplayScaling(KeyHeight));

                        g.DrawRect(rect, new SKPaint
                        {
                            Color = SKColors.LightSkyBlue,
                            Style = SKPaintStyle.Fill,
                        });
                        g.DrawRect(rect, new SKPaint
                        {
                            Color = SKColors.DarkBlue,
                            Style = SKPaintStyle.Stroke,
                            StrokeWidth = 1.0f,
                            IsStroke = true,
                        });

                        // 歌詞
                        g.DrawText(score.Lyrics, new SKPoint(rect.Left, rect.Top), lyricsTypography);
                    }
                }

                // 声量の描画
                {
                    float lower = (float)FrequencyToScale(this._pitches.Min(i => i.Values.Min())) * KeyHeight;
                    float highter = (float)FrequencyToScale(this._pitches.Max(i => i.Values.Max())) * KeyHeight;
                    float diff = highter - lower;

                    var dynaicsValues = this._dynamics.Where(i => i.Index <= endFrameIdx && (i.Index + i.Values.Length) >= beginFrameIdx);

                    float dynamicsOffset = (float)KeyHeight / 2;

                    foreach (var dynamics in dynaicsValues)
                    {
                        // 描画開始／終了インデックス
                        (int beginIdx, int endIdx) = GetDrawRange(dynamics.Index, dynamics.Values.Length, beginFrameIdx, endFrameIdx, marginFrames);

                        var points = new SKPoint[endIdx - beginIdx];

                        for (int idx = 0; idx < points.Length; ++idx)
                        {
                            int frameIdx = beginIdx + idx;

                            points[idx] = new SKPoint(
                                scaling.ToDisplayScaling((offsetX + ((frameIdx - beginFrameIdx) * RenderConfig.FramePeriod)) * renderInfo.WidthStretch),
                                scoreYOffset + scaling.ToDisplayScaling(height - dynamicsOffset - (lower + diff * ((dynamics.Values[frameIdx - dynamics.Index] + 30f) / 30f))));
                        }

                        g.DrawPoints(SKPointMode.Polygon, points, new SKPaint { Color = SKColors.Blue, StrokeWidth = 1.5f, IsAntialias = true });

                    }
                }

                // ピッチの描画
                {
                    var pitches = this._pitches.Where(i => i.Index <= endFrameIdx && (i.Index + i.Values.Length) >= beginFrameIdx);

                    float pitchOffset = (float)KeyHeight / 2;

                    foreach (var pitch in pitches)
                    {
                        // 描画開始／終了インデックス
                        (int beginIdx, int endIdx) = GetDrawRange(pitch.Index, pitch.Values.Length, beginFrameIdx, endFrameIdx, marginFrames);

                        var points = new SKPoint[endIdx - beginIdx];

                        for (int idx = 0; idx < points.Length; ++idx)
                        {
                            int frameIdx = beginIdx + idx;

                            points[idx] = new SKPoint(
                                scaling.ToDisplayScaling((offsetX + ((frameIdx - beginFrameIdx) * RenderConfig.FramePeriod)) * renderInfo.WidthStretch),
                                scoreYOffset + scaling.ToDisplayScaling(height - pitchOffset - ((float)FrequencyToScale(pitch.Values[frameIdx - pitch.Index]) * KeyHeight)));
                        }

                        g.DrawPoints(SKPointMode.Polygon, points, new SKPaint { Color = SKColors.Red, StrokeWidth = 1.5f, IsAntialias = true });

                    }
                }
            }
        }

        return image;
    }

    /// <summary>
    /// 描画範囲を取得する
    /// </summary>
    /// <param name="dataBeginIdx">データの開智位置</param>
    /// <param name="dataCount">データ数</param>
    /// <param name="rangeBeginIdx">範囲開始位置</param>
    /// <param name="rangeEndIdx">範囲終了位置</param>
    /// <param name="margin">前後のマージン</param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (int beginIdx, int endIdx) GetDrawRange(int dataBeginIdx, int dataCount, int rangeBeginIdx, int rangeEndIdx, int margin)
        => (Math.Max(dataBeginIdx, rangeBeginIdx - margin),
            Math.Min(dataBeginIdx + dataCount, rangeEndIdx + margin));

    /// <summary>
    /// ルーラを描画する
    /// </summary>
    /// <param name="renderInfo">描画情報</param>
    /// <returns>描画内容</returns>
    private SKBitmap CreateRulerImage(RenderInfo renderInfo)
    {
        var scaling = renderInfo.Scaling;

        long totalFrameCount = this._framesCount;

        int renderWidth = renderInfo.RenderWidth;
        int rulerHeight = this._rulerHeight;
        int renderHeight = renderInfo.RenderRulerHeight;
        var result = this._currentViewScore;

        var image = new SKBitmap(renderWidth, renderHeight);

        using (var g = new SKCanvas(image))
        {
            // 描画するフレーム数
            int viewFrames = renderInfo.GetRenderFrames();
            int framesCount = viewFrames/* + (offsetFrames * 2)*/;

            // 開始フレーム位置
            int beginFrameIdx = this.GetRenderBeginFrameIdx();
            int endFrameIdx = beginFrameIdx + framesCount;

            // 描画開始位置のオフセットを計算
            int frameBasedTime = beginFrameIdx * RenderConfig.FramePeriod;
            int _beginTime = (this.GetRenderBeginFrameIdx() * RenderConfig.FramePeriod) + RenderConfig.FramePeriod;
            int offsetX = frameBasedTime - _beginTime;

            g.DrawRect(0, 0, renderWidth, renderHeight, new SKPaint() { Color = SKColors.Black });

            if (this._isLoaded && result is not null)
            {
                var tempoDic = result.Tempos.ToDictionary(i => (int)i.Frame);
                var tsDic = result.TimeSignatures.ToDictionary(i => (int)i.Frame);

                int beginTime = beginFrameIdx * FrameUnit;
                int endTime = endFrameIdx * FrameUnit;

                var tempo = result.Tempos.First();
                var timeSignature = result.TimeSignatures.First();

                int count = 0;

                bool changed = true;
                decimal unit = 1;
                decimal c = 1;
                for (decimal time = result.BeginMeasureTime; time <= endTime;)
                {
                    if (tempoDic.TryGetValue((int)time, out var t))
                    {
                        tempo = t;
                        changed = true;
                    }

                    if (tsDic.TryGetValue((int)time, out var t2))
                    {
                        timeSignature = t2;
                        count = 0;
                        changed = true;
                    }

                    if (changed)
                    {
                        decimal quantize = 8m;
                        c = timeSignature.BeatType / 4m / (quantize / 4m);
                        unit = 60 / (decimal)tempo.Tempo * 1000 / (quantize / 4m);
                        changed = false;
                    }

                    // 描画範囲内
                    if (beginTime <= time || time <= endTime)
                    {
                        int x = scaling.ToDisplayScaling((offsetX + MsToFrameIndex(time - beginTime)) * RenderConfig.FramePeriod * ScaleX);

                        g.DrawLine(x, (count != 0 ? (renderHeight / 2) : 0), x, renderHeight, new SKPaint { StrokeWidth = 1, Color = SKColors.White });
                    }

                    ++count;

                    if (count == (timeSignature.Beats / c))
                    {
                        count = 0;
                    }

                    time += unit;
                }
            }
        }

        return image;
    }

    /// <summary>
    /// フレーム位置を時間(ミリ秒)に変換する
    /// </summary>
    /// <param name="frameIdx">フレームのインデックス</param>
    /// <returns>時間(ミリ秒)</returns>
    private static int FrameIndexToMs(decimal frameIdx) => (int)(frameIdx * FrameUnit);

    /// <summary>
    /// 時間(ミリ秒)をフレーム数に変換する
    /// </summary>
    /// <param name="time">時間(ミリ秒)</param>
    /// <returns>フレーム位置</returns>
    private static int MsToFrameIndex(decimal time) => (int)(time / FrameUnit);

    /// <summary>
    /// 再描画要求時
    /// </summary>
    /// <param name="sender">イベント発火時</param>
    /// <param name="e">イベント情報</param>
    private void OnPaintSurface(object sender, SkiaSharp.Views.Desktop.SKPaintSurfaceEventArgs e)
    {
        // 描画先
        var g = e.Surface.Canvas;

        if (DesignerProperties.GetIsInDesignMode(this))
        {
            // デザインモード時は描画処理を行わない
            return;
        }

        var renderInfo = this._renderInfo;
        if (renderInfo is null)
        {
            return;
        }

        (int width, int height) = (renderInfo.RenderWidth, renderInfo.RenderHeight);

        // 描画領域の更新
        int renderHeight = KeyHeight * KeyCount;

        int rulerHeight = renderInfo.RenderRulerHeight;

        // スクロール位置から描画位置(y)を計算
        int renderY = (int)Math.Floor(this.GetVerticalScrollCoe() * (renderHeight - height - rulerHeight));
        g.DrawBitmap(this._renderImage, SKRect.Create(0, rulerHeight + renderY, width, height - rulerHeight), SKRect.Create(0, rulerHeight, width, height - rulerHeight));
        g.DrawBitmap(this._rulerImage, SKRect.Create(0, 0, width, rulerHeight), SKRect.Create(0, 0, width, rulerHeight));
    }

    /// <summary>
    /// 周波数値を12音階律のスケールに変換する
    /// </summary>
    /// <param name="frequency">周波数</param>
    /// <returns>12音階率</returns>
    private static double FrequencyToScale(double frequency)
    {
        // http://signalprocess.binarized.work/2019/03/26/convert_frequency_to_cent/
        return 12 * Math.Log2(frequency / 440) + 69;
    }


    public static List<Class1> Parse(IReadOnlyCollection<double> values, double min)
    {
        var items = new List<Class1>(values.Count());

        int idx = 0;

        int tempIdx = 0;
        List<double>? tempItems = null;

        foreach (var value in values)
        {
            try
            {
                if (value <= min)
                {
                    if (tempItems is not null)
                    {
                        if (tempItems.Count > 1)
                        {
                            items.Add(new Class1(tempIdx, tempItems.ToArray()));
                        }
                        tempItems = null;
                    }
                    continue;
                }

                if (tempItems == null)
                {
                    tempItems = new List<double>();
                    tempIdx = idx;
                }
                tempItems.Add(value);
            }
            finally
            {
                ++idx;
            }
        }

        if (tempItems != null)
        {
            items.Add(new Class1(tempIdx, tempItems.ToArray()));
        }

        return items;
    }

    /// <summary>
    /// 縦スクロール時
    /// </summary>
    /// <param name="sender">イベント発火元</param>
    /// <param name="e">イベント情報</param>
    private void OnVScroll(object sender, ScrollEventArgs e)
    {
        if (e.ScrollEventType != ScrollEventType.First)
        {
            this.SKElement.InvalidateVisual();
        }
    }

    /// <summary>
    /// 横スクロール時
    /// </summary>
    /// <param name="sender">イベント発火元</param>
    /// <param name="e">イベント情報</param>
    private void OnHScroll(object sender, ScrollEventArgs e)
    {
        int time = (int)e.NewValue;
        if (this.GetRenderBeginTimeMs() == time)
        {
            return;
        }

        if (e.ScrollEventType != ScrollEventType.First)
        {
            this.OnRenderBeginMsChanged(time);
            this.Redraw();
            this.RelocateSeekBar();
        }
    }

    /// <summary>
    /// 暫定で平均値をしておく
    /// </summary>
    /// <param name="mgc"></param>
    /// <param name="f0"></param>
    /// <returns></returns>
    private IReadOnlyCollection<double> GetDynamicsFromMgc(double[] mgc, double[] f0)
    {
        int dimentions = mgc.Length / f0.Length;

        int length = mgc.Length / dimentions;
        var values = new double[length];

        for (int idx = 0; idx < length; ++idx)
        {
            values[idx] = mgc[idx * dimentions];
        }

        return values;
    }

    /// <summary>
    /// マウスホイールイベント発火時
    /// </summary>
    /// <param name="sender">イベント発火元</param>
    /// <param name="e">イベント情報</param>
    private void OnScoreMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var modifiers = Keyboard.Modifiers;

        if (modifiers == ModifierKeys.Control)
        {
            // Ctrl+スクロール時
            // 横倍率を変更

            double zoomLevel = this.ScaleX;

            if (e.Delta > 0)
            {
                // 横伸長率リストから次に大きい拡大率(拡大方向)を取得して適用する
                this.ChangeHorizontalScale(
                    HorizontalZoomLevels.GetNextUpper(zoomLevel));
            }
            else if (e.Delta < 0)
            {
                // 横伸長率リストから次に小さい拡大率(縮小方向)を取得して適用する
                this.ChangeHorizontalScale(
                    HorizontalZoomLevels.GetNextLower(zoomLevel));
            }
        }
        else if (modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            // Ctrl+Shift+スクロール時
            // 縦倍率を変更
        }
        else
        {
            // キー操作がない場合
            bool isHorizontal = modifiers == ModifierKeys.Shift;
            var targetScrollBar = isHorizontal
                ? this.hScrollBar1
                : this.vScrollBar1;

            int change = (int)(targetScrollBar.LargeChange / this.ScaleX) * 20;
            if (e.Delta > 0)
            {
                if (isHorizontal)
                {
                    this.SetRenderBeginMsOffset(-change);
                }
                else
                {
                    this.SetVerticalPositionOffset(-change);
                }
                this.Redraw();
            }
            else if (e.Delta < 0)
            {
                if (isHorizontal)
                {
                    this.SetRenderBeginMsOffset(change);
                }
                else
                {
                    this.SetVerticalPositionOffset(change);
                }
                this.Redraw();
            }
        }
    }

    /// <summary>
    /// 横の拡大率を変更する
    /// </summary>
    /// <param name="newZoomLevel"></param>
    private void ChangeHorizontalScale(double newZoomLevel)
    {
        var scaling = this._scaling;

        double oldZoomLevel = this.ScaleX;
        if (oldZoomLevel == newZoomLevel)
        {
            // 変更がない場合は処理をスキップする
            return;
        }

        var element = this.SKElement;

        var mousePosition = Mouse.GetPosition(element);

        (double width, _) = this.GetCanvasSize();
        width = scaling.ToRenderImageScaling(width);

        // マウス位置(%)
        double percentage = mousePosition.X / element.ActualWidth;

        // 現在の伸長率の尺を取得する
        double oldDuration = width * percentage / oldZoomLevel;
        // 変更後の伸長率の尺を計算する
        double newDuration = width * percentage / newZoomLevel;

        int diff = (int)Math.Round(oldDuration - newDuration);

        this.ScaleX = newZoomLevel;
        this.SetRenderBeginMsOffset(diff);
        this.OnLayoutChanged();
    }
}
