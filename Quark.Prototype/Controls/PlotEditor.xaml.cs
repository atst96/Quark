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
using System.Windows.Threading;
using Quark.Drawing;
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

    private double MaxHScrollHeight = 1000;

    private DispatcherTimer _mouseTimer;

    private SKPaint _whiteKeyPaint = new SKPaint { Color = new SKColor(255, 255, 255) };
    private SKPaint _blackKeyPaint = new SKPaint { Color = new SKColor(230, 230, 230) };
    private SKPaint _whiteKeyGridPaint = new SKPaint { Color = new SKColor(230, 230, 230), StrokeWidth = 1 };

    private SKBitmap _renderImage;
    private SKBitmap _rulerImage;

    private bool _isLoaded = false;
    private long _framesCount = -1;
    private List<Class1> _pitches;
    private List<Class1> _dynamics;
    private PartScore _score;
    private PartScore _currentViewScore;
    private VerticalLineInfo[]? _noteLines;
    private VerticalLineInfo[]? _rulerLines;
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

    /// <summary>キー描画高の初期値</summary>
    private const int DefaultKeyHeight = 12;

    /// <summary>キー高のリスト</summary>
    public static readonly int[] KeySizes =
    {
        5, 7, 12, 15, 19, 25
    };

    /// <summary>自動スクロール用の内側領域の幅</summary>
    private const double AutoScrollInnerWidth = 100;

    /// <summary>自動スクロール用の外側領域の幅</summary>
    private const double AutoScrollOuterWidth = 400;

    private int _rulerHeight = 24;

    private SKPaint lyricsTypography = new(new SKFont(SKTypeface.FromFamilyName("MS UI Gothic"), 12));

    public PlotEditor()
    {
        this.InitializeComponent();
        this._scaling = new RenderScaleInfo(VisualTreeHelper.GetDpi(this).DpiScaleX);

        // マウス操作時のタイマー
        this._mouseTimer = new(
            TimeSpan.FromMilliseconds(20d), DispatcherPriority.Render, this.OnMouseTimerTicked, this.Dispatcher)
        {
            IsEnabled = false,
        };
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

    /// <summary>キー描画高の一時変数</summary>
    private int _tempKeyHeight = DefaultKeyHeight;

    /// <summary>1キーあたりの高さ(px)</summary>
    public int KeyHeight
    {
        get => (int)this.GetValue(KeyHeightProperty);
        set => this.SetValue(KeyHeightProperty, this._tempKeyHeight = value);
    }

    /// <summary><seealso cref="KeyHeight"/>の依存関係プロパティ</summary>
    public static readonly DependencyProperty KeyHeightProperty =
        DependencyProperty.Register(nameof(KeyHeight), typeof(int), typeof(PlotEditor), new PropertyMetadata(DefaultKeyHeight, OnKeyHeightChanged));

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

    /// <summary>クオンタイズ値</summary>
    public LineType Quantize
    {
        get => (LineType)this.GetValue(QuantizeProperty);
        set => this.SetValue(QuantizeProperty, value);
    }

    /// <summary><seealso cref="Quantize"/>の依存関係プロパティ</summary>
    public static readonly DependencyProperty QuantizeProperty =
        DependencyProperty.Register(nameof(Quantize), typeof(LineType), typeof(PlotEditor),
            new PropertyMetadata(LineType.Note4th, OnQuantizeChanged));

    /// <summary>スナッピングの切り替え</summary>
    public bool IsQuantizeSnapping
    {
        get => (bool)this.GetValue(IsQuantizeSnappingProperty);
        set => this.SetValue(IsQuantizeSnappingProperty, value);
    }

    /// <summary><seealso cref="IsQuantizeSnapping"/>の依存関係プロパティ</summary>
    public static readonly DependencyProperty IsQuantizeSnappingProperty =
        DependencyProperty.Register(nameof(IsQuantizeSnapping), typeof(bool), typeof(PlotEditor), new PropertyMetadata(true));

    /// <summary>
    /// <seealso cref="Quantize"/>変更時
    /// </summary>
    /// <param name="d"></param>
    /// <param name="e"></param>
    private static void OnQuantizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // 再描画
        ((PlotEditor)d).Redraw();
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
    /// <seealso cref="KeyHeight"/>プロパティ変更時
    /// </summary>
    /// <param name="d">プロパティ変更要素</param>
    /// <param name="e">イベント情報</param>
    private static void OnKeyHeightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var editor = (PlotEditor)d;

        if (editor.UpdateInternalKeyHeight((int)e.NewValue))
        {
            editor.SetVScrollPosition((int)(editor.GetVScrollPosition() * ((double)(int)e.NewValue / (int)e.OldValue)));

            // 内部で変更済み出ない場合
            editor.OnLayoutChanged();
        }
    }

    /// <summary>
    /// 内部的に保持している横伸長率を更新する
    /// </summary>
    /// <param name="newValue"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool UpdateInternalKeyHeight(int newValue)
    {
        if (this._tempKeyHeight == newValue)
        {
            return false;
        }

        this._tempKeyHeight = newValue;
        return true;
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
        if (!this._isLoaded)
        {
            return;
        }

        int dataLength = this.Track!.GetFeatures().F0!.Length;

        var renderInfo = this._renderInfo;

        (_, double height) = this.GetCanvasSize();

        // ########## 縦スクロールの設定
        var vScrollBar = this.vScrollBar1;
        int renderHeight = renderInfo.GetDrawScoreHeight(height);
        double scaledKeyHeight = this._scaling.ToDisplayScaling(this.KeyHeight);
        vScrollBar.Minimum = 0;
        vScrollBar.Maximum = renderInfo.ScoreHeight - renderHeight;
        vScrollBar.SmallChange = scaledKeyHeight;
        vScrollBar.LargeChange = scaledKeyHeight;
        vScrollBar.ViewportSize = renderHeight;

        // ########## 横スクロールの設定
        long duration = (dataLength + 1) * FrameUnit;
        this.MaxHScrollHeight = duration;
        var hScrollBar = this.hScrollBar1;
        hScrollBar.Minimum = 0;
        hScrollBar.Maximum = this.MaxHScrollHeight;
        hScrollBar.LargeChange = 5 / this.ScaleX * 20;
        hScrollBar.ViewportSize = this.MaxHScrollHeight / 10;
    }

    /// <summary>
    /// 画面レイアウトの変更時
    /// </summary>
    public void OnLayoutChanged()
    {
        (_, double renderWidth) = this.GetCanvasSize();

        this._renderInfo = new RenderInfo(this._scaling, this.KeyHeight, (int)renderWidth, this.ScaleX, 1.0f);
        this.Redraw();
        this.UpdateScrollLayout();
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
        int beginTime = this.GetRenderBeginTimeMs();
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
                var renderInfo = this._renderInfo = new RenderInfo(this._scaling, this.KeyHeight, width, this.ScaleX, 1.0f);

                // 描画するフレーム数
                int offsetFrames = 1;
                int viewFrames = renderInfo.GetRenderFrames();
                int framesCount = viewFrames + (offsetFrames * 2);

                // 描画開始・終了位置
                int beginTime = this.GetRenderBeginTimeMs();
                int endTime = beginTime + (framesCount * RenderConfig.FramePeriod);

                var rangeInfo = new RenderRangeInfo(beginTime, endTime, offsetFrames, framesCount);

                var score = this._score;

                if (this._isLoaded && score is not null)
                {
                    this._currentViewScore = score.GetRangeInfo(beginTime, endTime);
                    this._noteLines = ScoreDrawingUtil.GetVerticalLines(score, beginTime, endTime, this.Quantize);
                    this._rulerLines = ScoreDrawingUtil.GetVerticalLines(score, beginTime, endTime, LineType.Note8th);
                }

                this._renderImage = this.CreateRenderImage(renderInfo, rangeInfo);
                this._rulerImage = this.CreateRulerImage(renderInfo, rangeInfo);
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
        this.UpdateScrollLayout();
        this.RelocateSeekBar();
    }

    /// <summary>縦スクロール位置を取得する</summary>
    private int GetVScrollPosition()
        => (int)this.vScrollBar1.Value;

    /// <summary>縦スクロール位置を設定する</summary>
    /// <param name="position">スクロール位置(px)</param>
    private void SetVScrollPosition(int position)
        => this.vScrollBar1.Value = position;

    /// <summary>縦スクロール位置を設定する</summary>
    /// <param name="offset">スクロール位置(px)</param>
    private void SetVScrollPositionOffset(int offset)
        => this.SetVScrollPosition(this.GetVScrollPosition() + offset);

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
    /// <param name="rangeInfo">描画範囲情報</param>
    /// <returns>描画内容</returns>
    private SKBitmap CreateRenderImage(RenderInfo renderInfo, RenderRangeInfo rangeInfo)
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
                    g.DrawBitmap(partImage, scaling.ToDisplayScaling(x), scaling.ToDisplayScaling(y));
                }
            }

            if (this._isLoaded)
            {
                long totalFrameCount = this._framesCount;

                // 描画開始・終了位置
                int beginTime = rangeInfo.BeginTime;
                int endTime = rangeInfo.EndTime;

                // フレームの描画範囲
                int beginFrameIdx = beginTime / RenderConfig.FramePeriod;
                int endFrameIdx = beginFrameIdx + rangeInfo.FramesCount;

                // 描画開始位置のオフセットを計算
                int offsetX = (beginFrameIdx * RenderConfig.FramePeriod) - beginTime;

                // 描画範囲の情報を取得
                var result = this._currentViewScore;

                // 罫線の描画
                var noteLines = this._noteLines!;
                foreach (var noteLine in noteLines)
                {
                    float scaledX = scaling.ToDisplayScaling(((int)noteLine.Time - beginTime) * renderInfo.WidthStretch);

                    var lineColor = noteLine.LineType switch
                    {
                        LineType.Measure => SKColors.Black,
                        LineType.Whole => SKColors.DarkGray,
                        LineType.Note2th => SKColors.DarkGray,
                        LineType.Note4th => SKColors.DarkGray,
                        _ => SKColors.LightGray,
                    };

                    g.DrawLine(
                        scaledX, 0,
                        scaledX, scaling.ToDisplayScaling(renderInfo.ImageHeight),
                        new SKPaint { Color = lineColor, StrokeWidth = 1 });
                }

                // スコアの描画
                foreach (var score in result.Phrases)
                {
                    float y = height - (float)(score.Pitch * KeyHeight);

                    var rect = SKRect.Create(
                        scaling.ToDisplayScaling((score.BeginTime - beginTime) * renderInfo.WidthStretch),
                        scaling.ToDisplayScaling(height - (score.Pitch * KeyHeight)),
                        scaling.ToDisplayScaling((score.EndTime - score.BeginTime) * renderInfo.WidthStretch),
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
                                scaling.ToDisplayScaling(height - dynamicsOffset - (lower + diff * ((dynamics.Values[frameIdx - dynamics.Index] + 30f) / 30f))));
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
                                scaling.ToDisplayScaling(height - pitchOffset - ((float)FrequencyToScale(pitch.Values[frameIdx - pitch.Index]) * KeyHeight)));
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
    private SKBitmap CreateRulerImage(RenderInfo renderInfo, RenderRangeInfo renderRange)
    {
        var scaling = renderInfo.Scaling;

        int renderWidth = renderInfo.RenderWidth;
        int renderHeight = renderInfo.RenderRulerHeight;
        var rulerLines = this._rulerLines;

        var image = new SKBitmap(renderWidth, renderHeight);

        using (var g = new SKCanvas(image))
        {
            g.DrawRect(0, 0, renderWidth, renderHeight, new SKPaint() { Color = SKColors.Black });

            if (rulerLines is not null)
            {

                // 描画開始・終了位置
                int beginTime = renderRange.BeginTime;

                // 小節、4分音符、8分音符時の描画位置
                float measureLineY = 0.0f;
                float beat4thLineY = renderHeight * 0.4f;
                float beat8thLineY = renderHeight * 0.7f;

                foreach (var rulerLine in rulerLines)
                {
                    float scaledX = scaling.ToDisplayScaling(((int)rulerLine.Time - beginTime) * renderInfo.WidthStretch);

                    var linePosY = rulerLine.LineType switch
                    {
                        LineType.Measure => measureLineY,
                        LineType.Whole or LineType.Note2th or LineType.Note4th => beat4thLineY,
                        _ => beat8thLineY,
                    };

                    g.DrawLine(
                        scaledX, linePosY,
                        scaledX, renderHeight,
                        new SKPaint { StrokeWidth = 1, Color = SKColors.White });
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

        (int scaledWidth, _) = (renderInfo.RenderWidth, renderInfo.RenderHeight);

        (_, int h) = this.GetCanvasSize();

        // 描画領域の更新
        int scaledRulerHeight = renderInfo.RenderRulerHeight;
        int scaledScoreHeight = Math.Min(renderInfo.RenderHeight, h - scaledRulerHeight);

        // スクロール位置から描画位置(y)を計算
        int scaledScoreY = renderInfo.Scaling.ToDisplayScaling(this.GetVScrollPosition());
        g.DrawBitmap(this._rulerImage, SKRect.Create(0, 0, scaledWidth, scaledRulerHeight), SKRect.Create(0, 0, scaledWidth, scaledRulerHeight));
        g.DrawBitmap(this._renderImage,
            SKRect.Create(0, scaledScoreY, scaledWidth, scaledScoreHeight),
            SKRect.Create(0, scaledRulerHeight, scaledWidth, scaledScoreHeight));

        double renderedY = scaledRulerHeight + scaledScoreHeight;
        if (renderedY < h)
        {
            g.DrawRect(SKRect.Create(0, (float)renderedY, scaledWidth, (float)(h - renderedY)), this._whiteKeyPaint);
        }
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
            int currentHeight = this.KeyHeight;
            if (e.Delta > 0)
            {
                // 横伸長率リストから次に大きい拡大率(拡大方向)を取得して適用する
                this.ChangeKeyHeightSize(
                    KeySizes.GetNextUpper(currentHeight));
            }
            else if (e.Delta < 0)
            {
                // 横伸長率リストから次に小さい拡大率(縮小方向)を取得して適用する
                this.ChangeKeyHeightSize(
                    KeySizes.GetNextLower(currentHeight));
            }
        }
        else
        {
            // キー操作がない場合
            bool isHorizontal = modifiers == ModifierKeys.Shift;
            var targetScrollBar = isHorizontal
                ? this.hScrollBar1
                : this.vScrollBar1;

            int change = (int)targetScrollBar.LargeChange;
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

    /// <summary>
    /// 横の拡大率を変更する
    /// </summary>
    /// <param name="newHeight"></param>
    private void ChangeKeyHeightSize(int newHeight)
    {
        var renderInfo = this._renderInfo;
        var scaling = renderInfo.Scaling;

        int oldHeight = this.KeyHeight;
        if (oldHeight == newHeight)
        {
            // 変更がない場合は処理をスキップする
            return;
        }

        var element = this.SKElement;
        var mousePosition = Mouse.GetPosition(element);

        (_, double height) = this.GetCanvasSize();
        height = scaling.ToRenderImageScaling(height);

        // マウス位置(%)
        double posY = mousePosition.Y;
        int rulerHeight = this._renderInfo.RulerHeight;
        double percentage = CalcRatioWithLowerOffset(posY, rulerHeight, height);

        double zoom = (double)newHeight / oldHeight;

        // 現在の伸長率の尺を取得する
        double oldDuration = height * percentage;
        // 変更後の伸長率の尺を計算する
        double newDuration = oldDuration * zoom;

        int diff = (int)Math.Round(newDuration - oldDuration);

        this.KeyHeight = newHeight;
        this.SetVScrollPosition((int)((this.GetVScrollPosition() * zoom) + diff));
        this.OnLayoutChanged();
    }

    /// <summary>範囲内の値の位置(%)を計算する(オフセット対応)</summary>
    /// <param name="value">現在の値</param>
    /// <param name="lowerOffset">下限オフセット</param>
    /// <param name="upperLimit">上限</param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double CalcRatioWithLowerOffset(double value, int lowerOffset, double upperLimit)
    {
        if (value <= lowerOffset)
        {
            return 0.0d;
        }
        else if (value > upperLimit)
        {
            return 1.0d;
        }
        else
        {
            return (value - lowerOffset) / (upperLimit - lowerOffset);
        }
    }

    private bool _mouseSeek = false;

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        Debug.WriteLine("Mouse down.");

        var renderInfo = this._renderInfo;
        var scaling = renderInfo.Scaling;
        var element = (UIElement)sender;

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            Debug.WriteLine("Mouse captured.");
            Mouse.Capture(this.SKElement);

            var mousePos = e.GetPosition(element);
            if (mousePos.Y <= renderInfo.RenderRulerHeight)
            {
                this._mouseSeek = true;
            }
        }
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        //if (e.LeftButton == MouseButtonState.Pressed
        //    || e.RightButton == MouseButtonState.Pressed
        //    || e.MiddleButton == MouseButtonState.Pressed
        //    || e.XButton1 == MouseButtonState.Pressed
        //    || e.XButton2 == MouseButtonState.Pressed)
        //{
        //    Debug.WriteLine($"Mouse move. Left: {e.LeftButton}");
        //}

        var renderInfo = this._renderInfo;
        var scaling = renderInfo.Scaling;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int GetConditionTime(RenderInfo renderInfo, ref Point mousePosition)
        {
            double width = renderInfo.ImageWidth;
            double percentageX = mousePosition.X / width;

            return Math.Max(0, this.GetRenderBeginTimeMs() + (int)(width * percentageX / renderInfo.WidthStretch)); ;
        }

        if (this._mouseSeek)
        {
            (double width, _) = this.GetCanvasSize();
            width = scaling.ToRenderImageScaling(width);

            var mousePosition = e.GetPosition(this);
            double posX = mousePosition.X;

            if (posX < AutoScrollInnerWidth)
            {
                this._mouseTimer.Start();
            }
            else if ((width - AutoScrollInnerWidth) < posX)
            {
                this._mouseTimer.Start();
            }

            int conditionTime = GetConditionTime(renderInfo, ref mousePosition);

            var noteLines = this._noteLines;
            if (this.IsQuantizeSnapping && noteLines is not null)
            {
                // スナッピング有効時にシークバーを罫線に沿うようにする
                int rangeBegin = (int)noteLines.First().Time;
                int rangeEnd = (int)noteLines.Last().Time;

                if (conditionTime < rangeBegin)
                {
                    conditionTime = rangeBegin;
                }
                else if (conditionTime > rangeEnd)
                {
                    conditionTime = rangeEnd;
                }
                else
                {
                    for (int idx = 1; idx < noteLines.Length; ++idx)
                    {
                        int begin = (int)noteLines[idx - 1].Time;
                        int end = (int)noteLines[idx - 0].Time - 1;

                        if (begin <= conditionTime && conditionTime <= end)
                        {
                            // 直前・直後どちらかの罫線に近い方に合わせる
                            if (conditionTime < (begin + ((end - begin) / 2)))
                            {
                                conditionTime = begin;
                            }
                            else
                            {
                                conditionTime = end + 1;
                            }
                            break;
                        }
                    }
                }
            }

            this.SelectionTime = TimeSpan.FromMilliseconds(conditionTime);
        }
        else
        {
            var score = this._score;

            if (score is not null)
            {
                var mousePosition = e.GetPosition(this);

                // TEST
                int conditionTime = GetConditionTime(renderInfo, ref mousePosition);

                int beginTime = this.GetRenderBeginTimeMs();

                var noteLines = this._noteLines;
                if (noteLines is not null)
                {
                    // スナッピング有効時にシークバーを罫線に沿うようにする
                    int rangeBegin = (int)noteLines.First().Time;
                    int rangeEnd = (int)noteLines.Last().Time;

                    if (conditionTime < rangeBegin)
                    {
                        conditionTime = rangeBegin;
                    }
                    else if (conditionTime > rangeEnd)
                    {
                        conditionTime = rangeEnd;
                    }
                    else
                    {
                        for (int idx = 1; idx < noteLines.Length; ++idx)
                        {
                            int begin = (int)noteLines[idx - 1].Time;
                            int end = (int)noteLines[idx - 0].Time - 1;

                            if (begin <= conditionTime && conditionTime <= end)
                            {
                                // 直前の罫線似合わせる
                                conditionTime = begin;
                                break;
                            }
                        }
                    }
                }

                decimal duration = ScoreDrawingUtil.GetNoteDuration(score, conditionTime, this.Quantize);
                double tempoPosX = scaling.ToDisplayScaling((conditionTime - beginTime) * renderInfo.WidthStretch);

                int noteWidth = scaling.ToDisplayScaling(((int)duration + 1) * renderInfo.WidthStretch);

                // 図形の位置を変更
                var el = this.PART_Rectangle;
                var elm = el.Margin;
                el.Margin = new(tempoPosX, elm.Top, elm.Right, elm.Bottom);
                el.Width = noteWidth;
            }
        }
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        //Debug.WriteLine("Mouse up.");

        if (e.LeftButton == MouseButtonState.Released)
        {
            this.SKElement.ReleaseMouseCapture();
            Debug.WriteLine("Mouse released.");

            if (this._mouseSeek)
            {
                this._mouseSeek = false;
                this._mouseTimer.Stop();
            }
        }
    }

    /// <summary>
    /// マウス操作用タイマーTick時
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnMouseTimerTicked(object? sender, EventArgs e)
    {
        if (!this._mouseSeek)
        {
            var timer = (DispatcherTimer)sender!;
            if (timer.IsEnabled)
                timer.Stop();

            return;
        }

        // TODO: スナッピング有効時にシークバー位置がずれるので何とかする
        // → シークバーの位置を再計算すれば何とかなりそう

        var renderInfo = this._renderInfo;
        var scaling = renderInfo.Scaling;

        (double width, _) = this.GetCanvasSize();
        width = scaling.ToRenderImageScaling(width);

        double posX = Mouse.GetPosition(this).X;

        double scaleX = this.ScaleX;
        if (posX < AutoScrollInnerWidth)
        {
            int degree = (int)((posX >= -AutoScrollOuterWidth) ? (posX - AutoScrollInnerWidth) : -(AutoScrollInnerWidth + AutoScrollOuterWidth));
            this.SetRenderBeginMs(Math.Max(0, this.GetRenderBeginTimeMs() + (int)(degree / scaleX)));
            // TODO: 再レンダリングの処理を見直す
            this.Redraw();
        }
        else if ((width - AutoScrollInnerWidth) < posX)
        {
            int degree = (int)((posX <= (width + AutoScrollOuterWidth)) ? (posX - width + AutoScrollInnerWidth) : (AutoScrollInnerWidth + AutoScrollOuterWidth));
            this.SetRenderBeginMsOffset((int)(degree / scaleX));
            // TODO: 再レンダリングの処理を見直す
            this.Redraw();
        }
    }
}
