using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Quark.Constants;
using Quark.Converters;
using Quark.Drawing;
using Quark.Extensions;
using Quark.Helpers;
using Quark.ImageRender;
using Quark.ImageRender.Parts;
using Quark.ImageRender.PianoRoll;
using Quark.ImageRender.Score;
using Quark.Models.Neutrino;
using Quark.Models.Scores;
using Quark.Projects.Tracks;
using Quark.Utils;
using SkiaSharp;
using static Quark.Controls.ViewDrawingBoxInfo;

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

    private ColorInfo ColorInfo { get; } = new ColorInfo();

    private SKBitmap? _renderImage;
    private SKBitmap? _rulerImage;
    private SKBitmap? _dynamicImage;

    private bool _isLoaded = false;
    private long _framesCount = 0;
    private IList<TimingHandle> _timings = Array.Empty<TimingHandle>();
    private TrackScoreInfo _trackScoreInfo;
    private ViewDrawingBoxInfo _viewBoxInfo;
    private RenderInfoCommon _renderCommon;
    private PianoRollRenderer _pianoRollRenderer;
    private RulerRenderer _rulerRenderer;
    private DynamicsRendererV2 _dynamicsRenderer;
    private TimingRenderer _timingRenderer;

    /// <summary>スコア編集可否フラグ</summary>
    private bool _isScoreEditable = true;

    /// <summary>タイミング編集可否フラグ</summary>
    private bool _isTimingEditable = true;

    /// <summary>F0編集可否フラグ</summary>
    private bool _isF0Editable = false;

    /// <summary>音響パラメータ編集可否フラグ</summary>
    private bool _isFeatureEditable = false;

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

    private SKPaint lyricsTypography = new(new SKFont(SKTypeface.FromFamilyName("MS UI Gothic"), 12));

    private (int x, int y)? _tempMousePos = null;

    private double _displayDpi;

    public PlotEditor()
    {
        this.InitializeComponent();
        this._displayDpi = VisualTreeHelper.GetDpi(this).DpiScaleX;

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
        this._displayDpi = e.NewDpi.DpiScaleX;
        this.OnLayoutChanged();
    }

    /// <summary>トラック情報</summary>
    internal INeutrinoTrack? Track
    {
        get => (INeutrinoTrack)this.GetValue(TrackProperty);
        set => this.SetValue(TrackProperty, value);
    }

    /// <summary><see cref="Track"/>プロパティ</summary>
    public static readonly DependencyProperty TrackProperty = DependencyProperty.Register(
        nameof(Track), typeof(INeutrinoTrack), typeof(PlotEditor), new PropertyMetadata(null, OnTrackPropertyChanged));

    /// <summary>
    /// <seealso cref="Track"/>プロパティ変更時
    /// </summary>
    /// <param name="d">プロパティ変更時</param>
    /// <param name="e">イベント情報</param>
    private static void OnTrackPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PlotEditor @this)
            return;

        if (e.OldValue is INeutrinoTrack oldTrack)
        {
            oldTrack.FeatureChanged -= @this.OnTrackFeatureChanged;
            oldTrack.TimingEstimated -= @this.OnTrackTimingEstimated;
        }

        if (e.NewValue is INeutrinoTrack newTrack)
        {
            newTrack.FeatureChanged += @this.OnTrackFeatureChanged;
            newTrack.TimingEstimated += @this.OnTrackTimingEstimated;
            @this.LoadTrack(newTrack);
        }

        @this.UpdateScrollLayout();
        @this.RelocateSeekBar();
    }

    /// <summary>シークバーの選択位置</summary>
    public TimeSpan SelectionTime
    {
        get => (TimeSpan)this.GetValue(SelectionTimeProperty);
        set => this.SetValue(SelectionTimeProperty, value);
    }

    /// <summary><see cref="SelectionTime"/>の依存関係プロパティ</summary>
    public static readonly DependencyProperty SelectionTimeProperty = DependencyProperty.Register(
        nameof(SelectionTime), typeof(TimeSpan), typeof(PlotEditor), new PropertyMetadata(TimeSpan.Zero, OnSelectionTimePropertyChanged));

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
    public static readonly DependencyProperty IsAutoScrollProperty = DependencyProperty.Register(
        nameof(IsAutoScroll), typeof(bool), typeof(PlotEditor), new PropertyMetadata(false));

    /// <summary>横伸長率の一時変数</summary>
    private double _tempScaleX = DefaultScaleX;

    /// <summary>横方向のズームレベル</summary>
    public double ScaleX
    {
        get => (double)this.GetValue(ScaleXProperty);
        set => this.SetValue(ScaleXProperty, this._tempScaleX = value);
    }

    /// <summary><seealso cref="ScaleX"/>の依存関係プロパティ</summary>
    public static readonly DependencyProperty ScaleXProperty = DependencyProperty.Register(
        nameof(ScaleX), typeof(double), typeof(PlotEditor), new PropertyMetadata(DefaultScaleX, OnScaleXChanged));

    /// <summary>キー描画高の一時変数</summary>
    private int _tempKeyHeight = DefaultKeyHeight;

    /// <summary>1キーあたりの高さ(px)</summary>
    public int KeyHeight
    {
        get => (int)this.GetValue(KeyHeightProperty);
        set => this.SetValue(KeyHeightProperty, this._tempKeyHeight = value);
    }

    /// <summary><seealso cref="KeyHeight"/>の依存関係プロパティ</summary>
    public static readonly DependencyProperty KeyHeightProperty = DependencyProperty.Register(
        nameof(KeyHeight), typeof(int), typeof(PlotEditor), new PropertyMetadata(DefaultKeyHeight, OnKeyHeightChanged));

    /// <summary>編集モード</summary>
    public EditMode EditMode
    {
        get => (EditMode)this.GetValue(EditModeProperty);
        set => this.SetValue(EditModeProperty, value);
    }

    /// <summary><see cref="EditMode"/>の依存関係プロパティ</summary>
    public static readonly DependencyProperty EditModeProperty = DependencyProperty.Register(
        nameof(EditMode), typeof(EditMode), typeof(PlotEditor),
        new PropertyMetadata(EditMode.ScoreAndTiming, static (d, _) => (d as PlotEditor)?.OnEditModeChanged()));

    /// <summary>
    /// 編集モード変更時
    /// </summary>
    private void OnEditModeChanged()
    {
        var editMode = this.EditMode;

        // 編集操作をキャンセルする
        this.CancelEdit();

        this._isTimingEditable = this._isScoreEditable = editMode == EditMode.ScoreAndTiming;
        this._isFeatureEditable = this._isF0Editable = editMode == EditMode.AudioFeatures;

        this.UpdateRenderContent();
    }

    private void OnTrackFeatureChanged(object? sender, EventArgs e) => this.Dispatcher.InvokeAsync(() =>
    {
        // 再描画
        this.UpdateRenderContent();
    }
    , DispatcherPriority.Render);

    private void OnTrackTimingEstimated(object? sender, EventArgs e) => this.Dispatcher.InvokeAsync(() =>
    {
        // 再描画
        this.LoadTiming();
    }
    , DispatcherPriority.Normal);

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
    public static readonly DependencyProperty IsQuantizeSnappingProperty = DependencyProperty.Register(
        nameof(IsQuantizeSnapping), typeof(bool), typeof(PlotEditor), new PropertyMetadata(true));

    /// <summary>
    /// <seealso cref="Quantize"/>変更時
    /// </summary>
    /// <param name="d"></param>
    /// <param name="e"></param>
    private static void OnQuantizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // 再描画
        ((PlotEditor)d).UpdateRenderContent();
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
    private void LoadTrack(INeutrinoTrack track)
    {
        // 楽譜情報解析

        this._isLoaded = true;
        var trackInfo = this._trackScoreInfo = new TrackScoreInfo
        {
            Score = MusicXmlUtil.Parse(track.MusicXml),
        };

        this._timings = ScoreLayoutHelper.CreateTimingHandles(track);

        this._framesCount = track.GetTotalFramesCount();

        this.PART_LyricsTextBox.Text = GetLyrics(trackInfo.Score);

        this.UpdateRenderContent();
    }

    private void LoadTiming()
    {
        var track = this.Track;
        if (track == null)
            return;

        this._timings = ScoreLayoutHelper.CreateTimingHandles(track);

        var trackInfo = this._trackScoreInfo;

        this._framesCount = track.GetTotalFramesCount();

        this.PART_LyricsTextBox.Text = GetLyrics(trackInfo.Score);

        this.UpdateRenderContent();
        this.UpdateScrollLayout();
        this.RelocateSeekBar();
    }

    /// <summary>スクロールバーの状態を更新する</summary>
    private void UpdateScrollLayout()
    {
        if (!this._isLoaded)
        {
            return;
        }

        long dataLength = this._framesCount;

        var renderInfo = this._viewBoxInfo;

        double height = renderInfo.RenderDisplayHeight;

        // ########## 縦スクロールの設定
        var vScrollBar = this.vScrollBar1;
        int renderHeight = renderInfo.GetDrawScoreHeight(height);
        double scaledKeyHeight = renderInfo.Scaling.ToDisplayScaling(renderInfo.KeyHeight);
        vScrollBar.Minimum = 0;
        vScrollBar.Maximum = renderInfo.UnscaledScoreHeight - renderHeight;
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
        this._viewBoxInfo = this.CreateRenderInfo();
        this.UpdateRenderInfo(new RenderInfoCommon
        {
            Track = this.Track,
            RenderRange = this._renderCommon.RenderRange,
            ColorInfo = this.ColorInfo,
            PartRenderInfo = this._viewBoxInfo,
        });
        this.UpdateRenderContent();
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

        var viewBox = this._viewBoxInfo;
        var scaling = viewBox.Scaling;

        // 描画領域
        int renderWidth = viewBox.RenderWidth;

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
                if (totalFrameCount <= 0)
                {
                    value = 0;
                }
                else if (prevTime.HasValue && prevTime < time)
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
                    this.UpdateRenderContent();
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
    /// ピアノロール上の描画位置(X)を取得する
    /// </summary>
    /// <param name="timeMs">時間(ミリ秒)</param>
    /// <returns></returns>
    private int GetScoreLocationX(int timeMs)
    {
        var renderInfo = this._viewBoxInfo;
        var scaling = renderInfo.Scaling;

        return scaling.ToDisplayScaling((timeMs - this.GetRenderBeginTimeMs()) * renderInfo.WidthStretch);
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

    private ViewDrawingBoxInfo CreateRenderInfo()
    {
        (int width, int height) = this.GetCanvasSize();

        return new(new RenderScaleInfo(this._displayDpi), width, height, this.KeyHeight, width, this.ScaleX, 1.0f);
    }

    /// <summary>
    /// 描画する内容を再生成する
    /// </summary>
    private void UpdateRenderContent(bool redraw = true)
    {
        if (this.ActualWidth <= 0 || this.ActualHeight <= 0)
            return;

        var renderInfo = this._viewBoxInfo = this.CreateRenderInfo();

        // 描画するフレーム数
        int offsetFrames = 1;
        int viewFrames = renderInfo.GetRenderFrames();
        int framesCount = viewFrames; // + (offsetFrames * 2);

        // 描画開始・終了位置
        int beginTime = this.GetRenderBeginTimeMs();
        int endTime = beginTime + (framesCount * RenderConfig.FramePeriod);

        var rangeInfo = new RenderRangeInfo(beginTime, endTime, offsetFrames, framesCount);

        this.UpdateRenderInfo(new RenderInfoCommon
        {
            Track = this.Track,
            RenderRange = rangeInfo,
            ColorInfo = this.ColorInfo,
            PartRenderInfo = this._viewBoxInfo,
        });

        var trackScore = this._trackScoreInfo;
        if (trackScore != null)
        {
            var timings = this._timings;

            this._renderCommon.RangeScoreRenderInfo = new RangeScoreRenderInfo
            {
                Score = trackScore.Score.GetRangeInfo(beginTime, endTime),
                Timings = ScoreLayoutHelper.GetRenderTargets(this._timingRenderer, this._renderCommon!, timings, this._editingTimingInfo?.Target),
                NoteLines = ScoreDrawingUtil.GetVerticalLines(trackScore.Score, beginTime, endTime, this.Quantize),
                RulerLines = ScoreDrawingUtil.GetVerticalLines(trackScore.Score, beginTime, endTime, LineType.Note8th),
            };
        }

        DisposableUtil.ExchangeDisposable(ref this._renderImage, this._pianoRollRenderer.CreateImage());
        DisposableUtil.ExchangeDisposable(ref this._rulerImage, this._rulerRenderer.CreateImage());
        DisposableUtil.ExchangeDisposable(ref this._dynamicImage, this._dynamicsRenderer.CreateImage());

        if (redraw)
            this.Redraw();
    }

    private void UpdateRenderInfo(RenderInfoCommon renderInfoCommon)
    {
        this._renderCommon = renderInfoCommon;
        this._pianoRollRenderer = new PianoRollRenderer(renderInfoCommon);
        this._rulerRenderer = new RulerRenderer(renderInfoCommon);
        this._dynamicsRenderer = new DynamicsRendererV2(renderInfoCommon);
        this._timingRenderer = new TimingRenderer(renderInfoCommon);
    }

    /// <summary>
    /// 画面を再描画する
    /// </summary>
    private void Redraw()
        => this.SKElement.InvalidateVisual();

    /// <summary>
    /// 描画領域のサイズ変更時
    /// </summary>
    /// <param name="sender">イベント発火元</param>
    /// <param name="e">イベント情報</param>
    private void OnRenderSizeChanged(object sender, SizeChangedEventArgs e)
    {
        this.UpdateRenderContent();
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

        var renderInfo = this._viewBoxInfo;
        if (renderInfo is null)
        {
            return;
        }

        (int scaledWidth, int scaledHeight) = (renderInfo.RenderWidth, renderInfo.RenderScoreHeight);

        int h = renderInfo.RenderDisplayHeight;

        // 描画領域の更新
        int scaledRulerHeight = renderInfo.RenderRulerHeight;
        int scaledScoreHeight = renderInfo.RenderDisplayScoreHeight;

        // スクロール位置から描画位置(y)を計算
        int scaledScoreY = renderInfo.Scaling.ToDisplayScaling(this.GetVScrollPosition());
        g.DrawBitmap(this._rulerImage,
            SKRect.Create(0, 0, scaledWidth, scaledRulerHeight),
            SKRect.Create(0, 0, scaledWidth, scaledRulerHeight));
        g.DrawBitmap(this._renderImage,
            SKRect.Create(0, scaledScoreY, scaledWidth, scaledScoreHeight),
            SKRect.Create(0, renderInfo.DisplayScorePosY, scaledWidth, scaledScoreHeight));

        this._timingRenderer.Render(g);

        if (renderInfo.DynamicRenderHeight > 0)
        {
            g.DrawBitmap(this._dynamicImage, 0, renderInfo.DynamicsDisplayPosY);
        }

        //double renderedY = scaledRulerHeight + scaledScoreHeight;
        //if (renderedY < h)
        //{
        //    g.DrawRect(SKRect.Create(0, (float)renderedY, scaledWidth, (float)(h - renderedY)), this._whiteKeyPaint);
        //}
    }

    /// <summary>
    /// 縦スクロール時
    /// </summary>
    /// <param name="sender">イベント発火元</param>
    /// <param name="e">イベント情報</param>
    private void OnVScroll(object sender, ScrollEventArgs e)
    {
        this.Redraw();
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

        this.OnRenderBeginMsChanged(time);
        this.UpdateRenderContent();
        this.RelocateSeekBar();
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
        var renderInfo = this._viewBoxInfo;
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
            int currentHeight = renderInfo.KeyHeight;
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
            // その他のキー操作

            // Shiftキーが押下中なら横スクロール、それ以外なら縦スクロール
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
                this.UpdateRenderContent();
                this.RelocateSeekBar();
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
                this.UpdateRenderContent();
                this.RelocateSeekBar();
            }
        }
    }

    /// <summary> 
    /// 横の拡大率を変更する
    /// </summary>
    /// <param name="newZoomLevel"></param>
    private void ChangeHorizontalScale(double newZoomLevel)
    {
        double oldZoomLevel = this.ScaleX;
        if (oldZoomLevel == newZoomLevel)
        {
            // 変更がない場合は処理をスキップする
            return;
        }

        var element = this.SKElement;

        var mousePosition = Mouse.GetPosition(element);

        var renderInfo = this._viewBoxInfo;

        double width = renderInfo.RenderWidth;

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
        var renderInfo = this._viewBoxInfo;
        var scaling = renderInfo.Scaling;

        int oldHeight = this.KeyHeight;
        if (oldHeight == newHeight)
        {
            // 変更がない場合は処理をスキップする
            return;
        }

        var element = this.SKElement;
        var mousePosition = Mouse.GetPosition(element);

        double height = renderInfo.RenderDisplayHeight;

        // マウス位置(%)
        double posY = mousePosition.Y;
        int rulerHeight = this._viewBoxInfo.UnscaledRulerHeight;
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

    /// <summary>編集モード</summary>
    private MouseControlMode _mouseMode = MouseControlMode.None;

    /// <summary>
    /// 編集モードを変更する
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="mode"></param>
    /// <param name="receiver"></param>
    /// <param name="value"></param>
    private void ChangeMouseMode<T>(MouseControlMode mode, out T? receiver, T value) where T : class
    {
        this.CancelEdit();
        (this._mouseMode, receiver) = (mode, value);
    }

    /// <summary>
    /// 編集モードをキャンセルする
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="receiver"></param>
    private void ClearMouseMode<T>(out T? receiver) where T : class
        => (this._mouseMode, receiver) = (MouseControlMode.None, default);

    /// <summary>
    /// タイミング編集情報
    /// タイミング編集時以外はnullを設定しておく
    /// </summary>
    private TimingEditingInfo? _editingTimingInfo;

    private int _putNoteBeginTime = 0;
    private int _putNoteKeyIndex = 0;

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        Debug.WriteLine("Mouse down.");

        var renderInfo = this._viewBoxInfo;
        var scaling = renderInfo.Scaling;
        var element = (UIElement)sender;

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            Debug.WriteLine("Mouse captured.");
            Mouse.Capture(this.SKElement);

            var mousePos = e.GetPosition(element);
            if (this.EditMode == EditMode.ScoreAndTiming)
            {
                if (IsWithinRuler(renderInfo, ref mousePos))
                {
                    this._mouseMode = MouseControlMode.Seek;
                    this.RelocateSeekBarForSeeking(e);
                }
                else if (IsWithinPianoRoll(renderInfo, ref mousePos))
                {
                    if (this._isTimingEditable)
                    {
                        var rangeTimings = this._renderCommon.RangeScoreRenderInfo?.Timings;
                        if (rangeTimings != null)
                        {
                            (int x, int y) = scaling.ToRenderImageScaling(mousePos.X, mousePos.Y);

                            var handle = rangeTimings.FirstOrDefault(h => h.IsSelected && h.IsCollisionDetection(x, y));
                            if (handle != null)
                            {
                                int idx = this._timings!.IndexOf(handle);

                                this.ChangeMouseMode(MouseControlMode.EditTiming, out this._editingTimingInfo, new(handle, handle.TimingInfo.EditedBeginTime100Ns)
                                {
                                    LowerTime100Ns = idx <= 1 ? 0L : this._timings[idx - 1].TimingInfo.EditedBeginTime100Ns,
                                    UpperTime100Ns = (idx == -1 || (this._timings.Count <= (idx + 1)))
                                                                    ? null : this._timings[idx + 1].TimingInfo.EditedBeginTime100Ns,
                                    OffsetX = x - handle.X,
                                });

                                return;
                            }
                        }
                    }

                    this._mouseMode = MouseControlMode.PutNote;
                    var mousePosition = e.GetPosition(this);
                    this._putNoteBeginTime = this.FindJustBeforeQuantizeSnapping(renderInfo, this.GetRenderBeginTimeMs(), ref mousePosition);

                    // キーのインデックスを計算する
                    // (譜面の高さ + (スクロール位置 + スクロール位置 + マウス位置 - ルーラ高)) ÷ キー高
                    this._putNoteKeyIndex = this.GetKeyIndex(renderInfo, ref mousePosition);

                    this.RelocateNoteRectangle(e);
                }
            }
            else if (this.EditMode == EditMode.AudioFeatures)
            {
                var mousePosition = e.GetPosition(this);

                int beginTime = this.GetRenderBeginTimeMs();
                (int x, int y) = scaling.ToRenderImageScaling(mousePosition.X, mousePosition.Y);
                this._tempMousePos = (x, y);

                int adjustedTime = GetConditionTimeRoundFrame(renderInfo, beginTime, ref mousePosition);

                this.RelocateSeekBar(TimeSpan.FromMilliseconds(adjustedTime));

                this._changeDynamicsDateTime = DateTime.Now;
            }
        }
    }

    private const int DirectionLeft = -1;
    private const int DirectionRight = 1;

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        // カーソル
        var cursor = Cursors.Arrow;

        //if (e.LeftButton == MouseButtonState.Pressed
        //    || e.RightButton == MouseButtonState.Pressed
        //    || e.MiddleButton == MouseButtonState.Pressed
        //    || e.XButton1 == MouseButtonState.Pressed
        //    || e.XButton2 == MouseButtonState.Pressed)
        //{
        //    Debug.WriteLine($"Mouse move. Left: {e.LeftButton}");
        //}

        if (this._mouseMode == MouseControlMode.Seek)
            this.RelocateSeekBarForSeeking(e);
        else if (this._mouseMode == MouseControlMode.PutNote)
            this.RelocateNoteRectangle(e);
        else if (this._mouseMode == MouseControlMode.EditTiming)
        {
            var renderInfo = this._viewBoxInfo;
            var scaling = renderInfo.Scaling;

            int beginTime = this.GetRenderBeginTimeMs();

            var trackScoreInfo = this._trackScoreInfo;
            if (trackScoreInfo != null)
            {
                var editing = this._editingTimingInfo!;

                // マウスの位置を取得、オフセット分補正する
                var mousePosition = e.GetPosition(this);
                mousePosition.Offset(scaling.ToRenderImageScaling(-editing.OffsetX), 0);

                // マウスで選択中の時間を計算し、範囲内に収める
                int adjustedTime = GetConditionTimeRoundFrame(renderInfo, beginTime, ref mousePosition);
                adjustedTime = Math.Max(adjustedTime, NeutrinoUtil.TimingTimeToMs(editing.LowerTime100Ns));
                if (editing.UpperTime100Ns != null)
                    adjustedTime = Math.Min(adjustedTime, NeutrinoUtil.TimingTimeToMs(editing.UpperTime100Ns.Value));

                // レイアウト、編集中のタイミング情報を更新
                int adjustedX = this.GetScoreLocationX(adjustedTime);
                editing.Target.MoveX(adjustedX);
                editing.CurrentTimeMs = adjustedTime;

                // 再描画
                this.Redraw();
            }
        }
        else if (this.EditMode == EditMode.AudioFeatures)
        {
            var renderInfo = this._viewBoxInfo;
            var mousePosition = e.GetPosition(this);

            int beginTime = this.GetRenderBeginTimeMs();
            var scaling = this._renderCommon.PartRenderInfo.Scaling;
            (int x, int y) = scaling.ToRenderImageScaling(mousePosition.X, mousePosition.Y);
            var prevPos = this._tempMousePos;

            int adjustedTime = GetConditionTimeRoundFrame(renderInfo, beginTime, ref mousePosition);

            bool isPianoRoll = !(renderInfo.DynamicsPosY <= mousePosition.Y);
            // this.DebugText(isDynamics.ToString());

            this.RelocateSeekBar(TimeSpan.FromMilliseconds(adjustedTime));

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                int prevAdjustedTime = -1;

                if (prevPos != null)
                {
                    var mousePosition2 = new Point(prevPos.Value.x, prevPos.Value.y);
                    prevAdjustedTime = GetConditionTimeRoundFrame(renderInfo, beginTime, ref mousePosition2);
                }

                if (isPianoRoll)
                {
                    int scrollPosition = this.GetVScrollPosition();

                    double currentPitch = (double)(renderInfo.UnscaledScoreHeight - (scrollPosition + (y - renderInfo.UnscaledRulerHeight)) - (renderInfo.KeyHeight / 2)) / renderInfo.KeyHeight;
                    double currentFrequency = AudioDataConverter.ScaleToFrequency(currentPitch);

                    int currentTime = adjustedTime;
                    int previousTime = prevPos == null ? currentTime : prevAdjustedTime;

                    double[] frequencies;
                    int beginFrameTime;

                    int length = Math.Abs(NeutrinoUtil.MsToFrameIndex(currentTime - previousTime));
                    if (length <= 1)
                    {
                        (beginFrameTime, frequencies) = (currentTime, new double[] { currentFrequency });
                    }
                    else
                    {
                        double previousPitch = (double)(renderInfo.UnscaledScoreHeight - (scrollPosition + (prevPos.Value.y - renderInfo.UnscaledRulerHeight)) - (renderInfo.KeyHeight / 2)) / renderInfo.KeyHeight;
                        double previousFrequency = AudioDataConverter.ScaleToFrequency(previousPitch);

                        (beginFrameTime, frequencies) = previousTime < currentTime
                            // 右方向の変更
                            ? (previousTime, CreateArithmeticSequenceArray(previousFrequency, currentFrequency, length))
                            // 左方向への変更
                            : (currentTime, CreateArithmeticSequenceArray(currentFrequency, previousFrequency, length));
                    }

                    var track = this.Track!;

                    if (track is NeutrinoV1Track v1Track)
                        v1Track.EditF0(beginFrameTime, frequencies);
                    else if (track is NeutrinoV2Track v2Track)
                        v2Track.EditF0(beginFrameTime, DataConvertUtil.ConvertArray<double, float>(frequencies));
                }
                else
                {
                    int y2 = y - renderInfo.DynamicsPosY;
                    double coe = 1d - ((double)y2) / renderInfo.DynamicRenderHeight;

                    var track = this.Track!;
                    if (track == null)
                        return;

                    var foundPhrase = track.Phrases.FirstOrDefault(i => i.BeginTime <= adjustedTime && adjustedTime <= i.EndTime);
                    if (foundPhrase == null)
                        return;

                    if (track is NeutrinoV1Track v1Track)
                    {
                        var phrase = (NeutrinoV1Phrase)foundPhrase;

                        const double min = -30d;
                        const double max = 0d;
                        const double period = max - min;

                        double value = (coe * period) + min;

                        phrase.EditDynamics(adjustedTime, value);
                    }
                    else if (track is NeutrinoV2Track v2Track)
                    {
                        var phrase = (NeutrinoV2Phrase)foundPhrase;

                        const float min = -6.0f;
                        const float max = 1.0f;
                        const float period = max - min;

                        float value = ((float)coe * period) + min;

                        phrase.EditDynamics(adjustedTime, value);
                    }
                }

                this._tempMousePos = (x, y);
                this.UpdateRenderContent();
            }
        }
        else
        {
            // this.RelocatePuttableNoteRectangle(e);

            var scaling = this._renderCommon.PartRenderInfo.Scaling;
            var point = e.GetPosition(this);

            (int x, int y) = scaling.ToRenderImageScaling(point.X, point.Y);

            if (IsWithinPianoRoll(this._viewBoxInfo, ref point))
            {
                if (this._isTimingEditable)
                {
                    var timings = this._renderCommon.RangeScoreRenderInfo?.Timings;
                    if (timings != null)
                    {
                        bool noSelected = true;

                        // 末尾から検索する
                        foreach (var target in timings.Reverse())
                        {
                            // 選択中(マウスオーバー)判定
                            // 選択中判定となるのは最初に見つかった要素のみ。以降は未選択とする。
                            bool isSelected = noSelected && target.IsCollisionDetection(x, y);
                            if (isSelected)
                                noSelected = false;

                            target.IsSelected = isSelected;
                        }

                        // 選択中のタイミングがある場合はカーソルを変更する
                        if (!noSelected)
                            cursor = Cursors.SizeWE;

                        // 再描画
                        this.Redraw();
                    }
                }
            }
        }

        this.SetCursor(cursor);
    }

    private Cursor _cursor = Cursors.Arrow;
    private DateTime? _changeDynamicsDateTime = null;

    private void SetCursor(Cursor cursor)
    {
        if (this._cursor != null)
        {
            this.Cursor = this._cursor = cursor;
        }
    }

    private void RelocateSeekBarForSeeking(MouseEventArgs e)
    {
        var renderInfo = this._viewBoxInfo;
        int beginTime = this.GetRenderBeginTimeMs();

        double width = renderInfo.RenderWidth;

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

        int conditionTime = GetConditionTime(renderInfo, beginTime, ref mousePosition);

        var noteLines = this._renderCommon?.RangeScoreRenderInfo?.NoteLines;
        if (this.IsQuantizeSnapping && noteLines != null)
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
                for (int idx = 1; idx < noteLines.Count; ++idx)
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

    private void RelocateNoteRectangle(MouseEventArgs e)
    {
        var renderInfo = this._viewBoxInfo;
        int beginTime = this.GetRenderBeginTimeMs();

        var trackScoreInfo = this._trackScoreInfo;
        if (trackScoreInfo != null)
        {
            var mousePosition = e.GetPosition(this);
            int conditionTime = this._putNoteBeginTime;
            int keyIndex = this._putNoteKeyIndex;

            int currentCursorPosition = GetConditionTime(renderInfo, beginTime, ref mousePosition);

            decimal duration = ScoreDrawingUtil.GetNoteDuration(trackScoreInfo.Score, conditionTime, this.Quantize, currentCursorPosition);

            // 図形の位置を変更
            this.MoveBorder(renderInfo, keyIndex, conditionTime - beginTime, (int)duration + 1);
        }
    }

    private void RelocatePuttableNoteRectangle(MouseEventArgs e)
    {
        var renderInfo = this._viewBoxInfo;
        int beginTime = this.GetRenderBeginTimeMs();

        var trackScoreInfo = this._trackScoreInfo;
        var mousePosition = e.GetPosition(this);
        if (trackScoreInfo != null && IsWithinPianoRoll(renderInfo, ref mousePosition))
        {
            // キーのインデックスを計算する
            // (譜面の高さ + (スクロール位置 + スクロール位置 + マウス位置 - ルーラ高)) ÷ キー高
            int keyIndex = this.GetKeyIndex(renderInfo, ref mousePosition);

            int conditionTime = this.FindJustBeforeQuantizeSnapping(renderInfo, beginTime, ref mousePosition);

            decimal duration = ScoreDrawingUtil.GetNoteDuration(trackScoreInfo.Score, conditionTime, this.Quantize);

            this.MoveBorder(renderInfo, keyIndex, (conditionTime - beginTime), (int)duration + 1);
        }
    }

    private int GetKeyIndex(ViewDrawingBoxInfo renderInfo, ref Point mousePosition)
    {
        int scrollPosition = this.GetVScrollPosition();
        return (int)((renderInfo.UnscaledScoreHeight - (scrollPosition + (mousePosition.Y - renderInfo.UnscaledRulerHeight))) / renderInfo.KeyHeight);
    }

    private void MoveBorder(ViewDrawingBoxInfo renderInfo, int keyIndex, int time, int duration)
    {
        var scaling = renderInfo.Scaling;


        int keyIndexForTop = RenderConfig.KeyCount - keyIndex - 1;
        double posY = (keyIndexForTop * renderInfo.KeyHeight) - this.GetVScrollPosition() + renderInfo.UnscaledRulerHeight;

        double posX = scaling.ToDisplayScaling(time * renderInfo.WidthStretch);
        double width = scaling.ToDisplayScaling(duration * renderInfo.WidthStretch);

        var element = this.PART_Rectangle;
        var beforeMargin = element.Margin;
        element.Margin = new(posX, posY, beforeMargin.Right, beforeMargin.Bottom);
        element.Width = width + 1;
        element.Height = renderInfo.KeyHeight + 1;

        element.Visibility = Visibility.Visible;
    }

    private void HideBorder()
        => this.PART_Rectangle.Visibility = Visibility.Collapsed;

    private int FindJustBeforeQuantizeSnapping(ViewDrawingBoxInfo renderInfo, int beginTime, ref Point mousePosition)
    {
        int conditionTime = GetConditionTime(renderInfo, beginTime, ref mousePosition);

        var noteLines = this._renderCommon?.RangeScoreRenderInfo?.NoteLines;
        if (noteLines != null)
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
                for (int idx = 1; idx < noteLines.Count; ++idx)
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

        return conditionTime;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetConditionTime(ViewDrawingBoxInfo renderInfo, int timeMs, ref Point mousePosition)
    {
        double width = renderInfo.UnscaledWidth;
        double percentageX = mousePosition.X / width;

        return Math.Max(0, timeMs + (int)(width * percentageX / renderInfo.WidthStretch));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetConditionTimeRoundFrame(ViewDrawingBoxInfo renderInfo, int timeMs, ref Point mousePosition)
    {
        // 最小単位
        const int unit = NeutrinoConfig.FramePeriod;

        return (int)Math.Round(GetConditionTime(renderInfo, timeMs, ref mousePosition) / (double)unit) * unit;
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        //Debug.WriteLine("Mouse up.");

        if (e.LeftButton == MouseButtonState.Released)
        {
            this.SKElement.ReleaseMouseCapture();
            Debug.WriteLine("Mouse released.");

            if (this._mouseMode == MouseControlMode.Seek)
            {
                this._mouseMode = MouseControlMode.None;
                this._mouseTimer.Stop();
            }
            else if (this._mouseMode == MouseControlMode.PutNote)
            {
                this._mouseMode = MouseControlMode.None;
            }
            else if (this._mouseMode == MouseControlMode.EditTiming)
            {
                this.DetermineTimingEdit();
            }
            else if (this.EditMode == EditMode.AudioFeatures)
            {
                var mousePosition = e.GetPosition(this);
                var renderInfo = this._viewBoxInfo;
                var scaling = this._renderCommon.PartRenderInfo.Scaling;

                int beginTime = this.GetRenderBeginTimeMs();
                (int x, int y) = scaling.ToRenderImageScaling(mousePosition.X, mousePosition.Y);

                int adjustedTime = GetConditionTimeRoundFrame(renderInfo, beginTime, ref mousePosition);

                if (IsWithinPianoRoll(renderInfo, ref mousePosition))
                {
                    // this.RelocateSeekBar(TimeSpan.FromMilliseconds(adjustedTime));

                    var updatedDateTime = this._changeDynamicsDateTime;
                    if (updatedDateTime != null)
                    {
                        this.Track?.ReSynthesis(updatedDateTime.Value);

                        this._changeDynamicsDateTime = null;
                    }
                }

                this._tempMousePos = null;
            }
        }
    }

    /// <summary>
    /// タイミング編集を確定する
    /// </summary>
    public void CancelTimingEdit()
    {
        var editing = this._editingTimingInfo;
        if (editing != null)
        {
            int adjustedX = this.GetScoreLocationX(NeutrinoUtil.TimingTimeToMs(editing.InitialTime100Ns));
            editing.Target.MoveX(adjustedX);

            foreach (var timing in this._timings)
            {
                timing.IsSelected = false;
            }

            // 再描画
            this.Redraw();
        }

        this.ClearMouseMode(out this._editingTimingInfo);
    }

    /// <summary>
    /// タイミング編集を確定する
    /// </summary>
    public void DetermineTimingEdit()
    {
        var editing = this._editingTimingInfo;
        if (editing != null)
        {
            var track = this.Track;
            track?.ChangeTiming(Array.IndexOf(track.Timings, editing.Target.TimingInfo), editing.CurrentTimeMs);
        }

        this.ClearMouseMode(out this._editingTimingInfo);
    }

    /// <summary>
    /// 編集操作をキャンセルする(Esc押下時)
    /// </summary>
    public void CancelEdit()
    {
        // エスケープキーが押下された場合
        // マウス操作の作業をキャンセルする
        switch (this._mouseMode)
        {
            case MouseControlMode.EditTiming:
                this.CancelTimingEdit();
                return;
        }
    }

    /// <summary>
    /// マウス操作用タイマーTick時
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnMouseTimerTicked(object? sender, EventArgs e)
    {
        if (this._mouseMode != MouseControlMode.Seek)
        {
            var timer = (DispatcherTimer)sender!;
            if (timer.IsEnabled)
                timer.Stop();

            return;
        }

        // TODO: スナッピング有効時にシークバー位置がずれるので何とかする
        // → シークバーの位置を再計算すれば何とかなりそう

        var renderInfo = this._viewBoxInfo;

        double width = renderInfo.RenderWidth;
        double posX = Mouse.GetPosition(this).X;

        double scaleX = this.ScaleX;
        if (posX < AutoScrollInnerWidth)
        {
            int degree = (int)((posX >= -AutoScrollOuterWidth) ? (posX - AutoScrollInnerWidth) : -(AutoScrollInnerWidth + AutoScrollOuterWidth));
            this.SetRenderBeginMs(Math.Max(0, this.GetRenderBeginTimeMs() + (int)(degree / scaleX)));
            // TODO: 再レンダリングの処理を見直す
            this.UpdateRenderContent();
        }
        else if ((width - AutoScrollInnerWidth) < posX)
        {
            int degree = (int)((posX <= (width + AutoScrollOuterWidth)) ? (posX - width + AutoScrollInnerWidth) : (AutoScrollInnerWidth + AutoScrollOuterWidth));
            this.SetRenderBeginMsOffset((int)(degree / scaleX));
            // TODO: 再レンダリングの処理を見直す
            this.UpdateRenderContent();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsWithinRuler(ViewDrawingBoxInfo renderInfo, ref Point mousePosition)
        => mousePosition.Y <= renderInfo.RenderRulerHeight;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsWithinPianoRoll(ViewDrawingBoxInfo renderInfo, ref Point mousePosition)
        => mousePosition.Y > renderInfo.RenderRulerHeight;

    private static string GetLyrics(PartScore score)
        => string.Concat(score.Phrases.Select(i => i.Lyrics.Length > 1 ? $"({i.Lyrics})" : i.Lyrics));

    private void OnLyricsTextBoxChanged(object sender, TextChangedEventArgs e)
    {
    }

    private void OnLyricsTextBoxSelectionChanged(object sender, RoutedEventArgs e)
    {
        var trackScoreInfo = this._trackScoreInfo;
        if (trackScoreInfo is null)
            return;

        var textBox = (sender as TextBox)!;

        var text = textBox.Text ?? string.Empty;
        int selection = textBox.SelectionStart + textBox.SelectionLength;

        int bracketNestCount = 0;

        var node = trackScoreInfo.Score.Phrases.First!;

        // 選択中の文字位置からどの(何番目の)音符にあたるかを走査する
        for (int idx = 0; idx < text.Length && idx < selection && node.Next is not null; ++idx)
        {
            char @char = text[idx];
            if (IsIgnoreLyricsCharacter(ref @char))
                continue;

            if (IsBracketStart(ref @char))
            {
                ++bracketNestCount;
            }
            else if (IsBracketEnd(ref @char))
            {
                --bracketNestCount;

                if (bracketNestCount < 0)
                    break;
                else if (bracketNestCount == 0)
                    node = node.Next;
            }
            else if (bracketNestCount == 0)
                node = node.Next;
        }

        var note = node.Value;

        // TODO: 暫定実装
        // 音符の位置にシークバーを移動させる。
        bool tempAutoScroll = this.IsAutoScroll;
        this.IsAutoScroll = true;
        this.SelectionTime = TimeSpan.FromMilliseconds(note.BeginTime);
        if (!tempAutoScroll)
            this.IsAutoScroll = tempAutoScroll;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsIgnoreLyricsCharacter(ref char @char)
        => @char == ' ' || @char == '　';

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsBracketStart(ref char @char)
        => @char == '(' || @char == '（';

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsBracketEnd(ref char @char)
        => @char == ')' || @char == '）';

    /// <summary>
    /// 指定の要素数(<paramref name="length"/>)で<paramref name="begin"/>から<paramref name="end"/>までの等差数列を生成する。
    /// </summary>
    /// <typeparam name="T">数値型</typeparam>
    /// <param name="begin">開始値</param>
    /// <param name="end">終了値</param>
    /// <param name="length">要素数</param>
    /// <returns></returns>
    private static T[] CreateArithmeticSequenceArray<T>(T begin, T end, int length)
        where T : INumber<T>
    {
        T[] values = new T[length];
        T delta = end - begin;
        T coe = T.CreateChecked(length - 1);

        for (int idx = 0; idx < values.Length; ++idx)
            values[idx] = (delta * T.CreateChecked(idx) / coe) + begin;

        return values;
    }
}
