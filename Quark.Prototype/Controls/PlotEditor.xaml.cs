﻿using System;
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
using Quark.Controls.Editing;
using Quark.Converters;
using Quark.Drawing;
using Quark.Extensions;
using Quark.Helpers;
using Quark.ImageRender;
using Quark.Models.Scores;
using Quark.Projects.Tracks;
using Quark.Utils;
using static Quark.Controls.EditorRenderLayout;

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
    private double MaxHScrollHeight = 1000;

    private DispatcherTimer _mouseTimer;

    private ColorInfo ColorInfo { get; } = new ColorInfo();

    private bool _isLoaded = false;
    private long _framesCount = 0;
    private IList<TimingHandle> _timings = Array.Empty<TimingHandle>();
    private TrackScoreInfo _trackScoreInfo;
    private EditorRenderLayout _renderLayout;
    private RenderInfoCommon _renderCommon;
    private EditorPartsLayoutResolver _partsLayout = null!;
    private EditorRendererBase _editorRenderer = null!;

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
        5, 7, 12, 15, 19, 25, 31, 37,
    };

    /// <summary>自動スクロール用の内側領域の幅</summary>
    private const double AutoScrollInnerWidth = 100;

    /// <summary>自動スクロール用の外側領域の幅</summary>
    private const double AutoScrollOuterWidth = 400;

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

        this.InitializeRenderer(null);
    }

    /// <summary>
    /// レンダラを初期化する
    /// </summary>
    /// <param name="track"></param>
    private void InitializeRenderer(INeutrinoTrack? track)
    {
        this._partsLayout = new();
        this._editorRenderer = EditorRendererHelper.GetRenderer(track, this._partsLayout);
    }

    static PlotEditor()
    {
        FocusableProperty.OverrideMetadata(typeof(PlotEditor), new FrameworkPropertyMetadata(true));
        FocusManager.IsFocusScopeProperty.OverrideMetadata(typeof(PlotEditor), new FrameworkPropertyMetadata(true));
        IsTabStopProperty.OverrideMetadata(typeof(PlotEditor), new FrameworkPropertyMetadata(true));
        KeyboardNavigation.TabNavigationProperty.OverrideMetadata(typeof(PlotEditor), new FrameworkPropertyMetadata(KeyboardNavigationMode.Once));
        KeyboardNavigation.ControlTabNavigationProperty.OverrideMetadata(typeof(PlotEditor), new FrameworkPropertyMetadata(KeyboardNavigationMode.Once));
        KeyboardNavigation.DirectionalNavigationProperty.OverrideMetadata(typeof(PlotEditor), new FrameworkPropertyMetadata(KeyboardNavigationMode.None));
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
        window.LocationChanged += this.OnParentWindowLocationChanged;
    }

    /// <summary>
    /// コントロール読み込み完了時
    /// </summary>
    /// <param name="sender">イベント発火元</param>
    /// <param name="e">イベント情報</param>
    private void OnUnload(object sender, RoutedEventArgs e)
    {
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

    /// <summary>
    /// コントロールを内包しているウィンドウの移動
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnParentWindowLocationChanged(object? sender, EventArgs e)
    {
        this.RelocateSeekBars();
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

        var newTrack = (INeutrinoTrack?)e.NewValue;
        @this.InitializeRenderer(newTrack);
        if (newTrack != null)
        {
            newTrack.FeatureChanged += @this.OnTrackFeatureChanged;
            newTrack.TimingEstimated += @this.OnTrackTimingEstimated;
            @this.LoadTrack(newTrack);
        }

        @this.UpdateScrollLayout();
        @this.RelocateSeekBars();
    }

    /// <summary>内部の編集シークバーの選択位置</summary>
    public TimeSpan _tempSelectionTime;

    private void UpdateTempSelectionTime(TimeSpan selectionTime)
    {
        this._tempSelectionTime = selectionTime;
        this.RelocateSelectionSeekBar(selectionTime);
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
        => ((PlotEditor)d).UpdateTempSelectionTime((TimeSpan)e.NewValue);

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
        this.CancelEditInternal();

        this._isTimingEditable = this._isScoreEditable = editMode == EditMode.ScoreAndTiming;
        this._isFeatureEditable = this._isF0Editable = editMode == EditMode.AudioFeatures;

        this.UpdateRenderContent();
        this.UpdateScrollLayout();
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
            new PropertyMetadata(LineType.Note16th, OnQuantizeChanged));

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

    /// <summary>再生中モードの有効／無効</summary>
    public bool IsPlayMode
    {
        get => (bool)this.GetValue(IsPlayModeProperty);
        set => this.SetValue(IsPlayModeProperty, value);
    }

    /// <summary><see cref="IsPlayMode"/>の依存関係プロパティ</summary>
    public static readonly DependencyProperty IsPlayModeProperty =
        DependencyProperty.Register(nameof(IsPlayMode), typeof(bool), typeof(PlotEditor), new PropertyMetadata(false, OnIsPlayModePropertyChanged));

    /// <summary>
    /// <see cref="IsPlayMode"/>プロパティ変更時
    /// </summary>
    /// <param name="d">対象要素</param>
    /// <param name="e">イベント情報</param>
    private static void OnIsPlayModePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((PlotEditor)d).RelocatePlayingSeekBar();

    /// <summary>現在再生時点の時刻</summary>
    public TimeSpan PlayingTime
    {
        get => (TimeSpan)this.GetValue(PlayingTimeProperty);
        set => this.SetValue(PlayingTimeProperty, value);
    }

    private SeekBarMode GetSeekBarMode()
    {
        if (!this.IsPlayMode || this._mouseMode == MouseControlMode.Seek)
            return SeekBarMode.Edit;
        else
            return SeekBarMode.Play;
    }

    /// <summary><see cref="PlayingTime"/>の依存関係プロパティ</summary>
    public static readonly DependencyProperty PlayingTimeProperty =
        DependencyProperty.Register(nameof(PlayingTime), typeof(TimeSpan), typeof(PlotEditor), new PropertyMetadata(TimeSpan.Zero, OnPlayingTimePropertyChanged));

    /// <summary>
    /// <see cref="PlayingTime"/>プロパティ変更時
    /// </summary>
    /// <param name="d">対象要素</param>
    /// <param name="e">イベント情報</param>
    private static void OnPlayingTimePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((PlotEditor)d).RelocatePlayingSeekBar((TimeSpan)e.NewValue, (TimeSpan)e.OldValue);

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
            Score = track.Score,
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
        this.RelocateSeekBars();
    }

    /// <summary>
    /// スクロールバーの状態を更新する
    /// </summary>
    private void UpdateScrollLayout()
    {
        if (!this._isLoaded)
            return;

        long dataLength = this._framesCount;
        var renderLayout = this._renderLayout;

        // ########## 縦スクロールの設定
        int viewHeight = renderLayout.ScoreArea.Height;
        double keyHeight = renderLayout.PhysicalKeyHeight;
        SetupScrollBar(this.vScrollBar1, 0, renderLayout.ScoreImage.Height - viewHeight, keyHeight, keyHeight, viewHeight);

        // ########## 横スクロールの設定
        double vChange = 5 / this.ScaleX * 20;
        this.MaxHScrollHeight = NeutrinoUtil.FrameIndexToMs((int)dataLength + 1);
        SetupScrollBar(this.hScrollBar1, 0, this.MaxHScrollHeight, vChange, vChange, this.MaxHScrollHeight / 10);
    }

    /// <summary>
    /// スクロールバーのパラメータを設定する
    /// </summary>
    /// <param name="element">対象</param>
    /// <param name="min">最小値</param>
    /// <param name="max">最大値</param>
    /// <param name="smallChange">最小変更幅</param>
    /// <param name="largeChange">最大変更幅</param>
    /// <param name="viewportSize">ビューポートのサイズ(Thumbのサイズ計算に使用)</param>
    private static void SetupScrollBar(ScrollBar element, double min, double max, double smallChange, double largeChange, double viewportSize)
    {
        element.Minimum = min;
        element.Maximum = max;
        element.SmallChange = smallChange;
        element.LargeChange = largeChange;
        element.ViewportSize = viewportSize;
    }

    /// <summary>
    /// 画面レイアウトの変更時
    /// </summary>
    public void OnLayoutChanged()
    {
        var renderLayout = this._renderLayout = this.CreateRenderLayout();
        this.UpdateRenderInfo(this._renderCommon.RenderRange, renderLayout);
        this.UpdateRenderContent();
        this.UpdateScrollLayout();
        this.RelocateSeekBars();
    }

    /// <summary>
    /// シークバーを移動して表示させる。
    /// </summary>
    /// <param name="seekBar">シークバー要素</param>
    /// <param name="physicalX">X座標(物理ピクセル)</param>
    /// <param name="layout">レイアウト</param>
    /// <param name="toDisplay">表示させるかどうかのフラグ</param>
    private void MoveSeekBar(IsolatedSeekBar seekBar, double physicalX, EditorRenderLayout layout, bool toDisplay)
    {
        if (!toDisplay)
        {
            seekBar.Hide();
            return;
        }

        var scaling = this._renderLayout.Scaling;

        var pyxPoint = this.SKElement.PointToScreen(new(scaling.ToRenderImageScaling(physicalX), 0));
        (double lgcPointX, double lgcPointY) = scaling.ToRenderImageScaling(pyxPoint.X, pyxPoint.Y);

        double halfWidth = seekBar.Width * .5d;

        double x = double.IsNaN(halfWidth) ? lgcPointX : (lgcPointX - halfWidth);

        seekBar.Show(x, lgcPointY, layout.ScreenHeight);
    }

    /// <summary>
    /// シークバーを隠す
    /// </summary>
    /// <param name="seekBar">シークバー要素</param>
    static void HideSeekBar(IsolatedSeekBar seekBar)
        => seekBar.Hide();

    /// <summary>編集位置を示すシークバーの画面要素を取得する</summary>
    private IsolatedSeekBar GetSelectionSeekBar()
        => this.PART_SelectionTime;

    /// <summary>編集用シークバーを移動する。</summary>
    private void DisplaySeekBar(double x, EditorRenderLayout layout)
        => this.MoveSeekBar(this.GetSelectionSeekBar(), x, layout, toDisplay: true);

    /// <summary>編集用のシークバーを隠す</summary>
    private void HideSeekBar()
        => HideSeekBar(this.GetSelectionSeekBar());


    /// <summary>
    /// 再生用シークバーの配置を修正する。
    /// </summary>
    /// <param name="isRecursive"></param>
    private void RelocatePlayingSeekBar(bool isRecursive = false)
        => this.RelocatePlayingSeekBar(this.PlayingTime, isRecursive: isRecursive);

    /// <summary>
    /// 再生位置シークバーの配置を修正する。
    /// </summary>
    /// <param name="time">現在時刻</param>
    /// <param name="prevTime">前の時刻</param>
    /// <param name="isRecursive">再帰呼び出しフラグ</param>
    private void RelocatePlayingSeekBar(TimeSpan time, TimeSpan? prevTime = null, bool isRecursive = false)
    {
        if (this.GetSeekBarMode() != SeekBarMode.Play)
            return;

        long totalFrameCount = this._framesCount;

        var renderLayout = this._renderLayout;

        // 開始フレーム位置
        int beginTime = this.GetRenderBeginTimeMs();
        int endTime = beginTime + renderLayout.GetRenderTimes();
        int currentTime = (int)time.TotalMilliseconds;

        if (beginTime <= currentTime && currentTime < endTime)
        {
            double x = renderLayout.GetRenderPosXFromTime(currentTime - beginTime) + renderLayout.ScoreArea.X;

            this.DisplaySeekBar(x, renderLayout);
            this.RelocateSelectionSeekBar();
        }
        else
        {
            if (this.IsAutoScroll && this._mouseMode == MouseControlMode.None)
            {
                double value;
                if (totalFrameCount <= 0)
                {
                    value = 0;
                }
                else if (prevTime.HasValue && prevTime < time)
                {
                    // 前方向への移動
                    value = Math.Ceiling(time.TotalMilliseconds / (totalFrameCount * 5) * MaxHScrollHeight);
                }
                else
                {
                    // 後方向への移動
                    value = Math.Ceiling((time.TotalMilliseconds - (endTime - beginTime)) / (totalFrameCount * 5) * MaxHScrollHeight);
                }

                if (!isRecursive)
                {
                    this.SetRenderBeginMs((int)value);
                    this.RelocatePlayingSeekBar(TimeSpan.FromMilliseconds(value), time, true);
                    this.UpdateRenderContent();
                }

                //this.SetRenderBeginMs((int)value);
                //this.UpdateRenderContent();

                return;
            }
            else
            {
                this.HideSeekBar();
            }
        }
    }

    private void RelocateSeekBars()
    {
        switch (this.GetSeekBarMode())
        {
            case SeekBarMode.Play:
                this.RelocatePlayingSeekBar();
                break;

            case SeekBarMode.Edit:
                this.RelocateSelectionSeekBar();
                break;
        };
    }

    /// <summary>
    /// 編集用シークバーの位置を修正する。
    /// </summary>
    /// <param name="isRecursive"></param>
    private void RelocateSelectionSeekBar(bool isRecursive = false)
        => this.RelocateSelectionSeekBar(this._tempSelectionTime, isRecursive: isRecursive);

    /// <summary>
    /// 編集用シークバーの位置を修正する。
    /// </summary>
    /// <param name="time">現在時刻</param>
    /// <param name="prevTime">前の時刻</param>
    /// <param name="isRecursive">再帰呼び出しフラグ</param>
    private void RelocateSelectionSeekBar(TimeSpan time, TimeSpan? prevTime = null, bool isRecursive = false)
    {
        if (this.GetSeekBarMode() != SeekBarMode.Edit)
            return;

        var renderLayout = this._renderLayout;

        // 開始フレーム位置
        int beginTime = this.GetRenderBeginTimeMs();
        int endTime = beginTime + renderLayout.GetRenderTimes();
        int currentTime = (int)time.TotalMilliseconds;

        if (beginTime <= currentTime && currentTime < endTime)
        {
            double x = renderLayout.GetRenderPosXFromTime(currentTime - beginTime) + renderLayout.ScoreArea.X;

            this.DisplaySeekBar(x, renderLayout);
        }
        else
        {
            this.HideSeekBar();
        }
    }

    /// <summary>
    /// ピアノロール上の描画位置(X)を取得する
    /// </summary>
    /// <param name="timeMs">時間(ミリ秒)</param>
    /// <returns></returns>
    private int GetScoreLocationX(EditorRenderLayout renderLayout, int timeMs)
        => renderLayout.GetRenderPosXFromTime(timeMs - this.GetRenderBeginTimeMs()) + renderLayout.ScoreArea.X;

    /// <summary>
    /// ピアノロール上の描画位置(X)を取得する
    /// </summary>
    /// <param name="timeMs">時間(ミリ秒)</param>
    /// <returns></returns>
    private int GetScoreLocationX(int timeMs)
        => this.GetScoreLocationX(this._renderLayout, timeMs);

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

    private EditorRenderLayout CreateRenderLayout()
    {
        var scaling = new RenderScaleInfo(this._displayDpi);
        (int width, int height) = this.GetCanvasSize();

        return new(scaling, width, height, this.KeyHeight, width, this.EditMode, this.ScaleX, 1.0f);
    }

    /// <summary>
    /// 描画する内容を再生成する
    /// </summary>
    private void UpdateRenderContent(bool redraw = true)
    {
        if (this.ActualWidth <= 0 || this.ActualHeight <= 0)
            return;

        var renderLayout = this._renderLayout = this.CreateRenderLayout();

        // 描画するフレーム数
        int offsetFrames = 1;
        int viewFrames = renderLayout.GetRenderFrames();
        int framesCount = viewFrames; // + (offsetFrames * 2);

        // 描画開始・終了位置
        int beginTime = this.GetRenderBeginTimeMs();
        int endTime = beginTime + (framesCount * RenderConfig.FramePeriod);

        var rangeInfo = new RenderRangeInfo(beginTime, endTime, offsetFrames, framesCount);
        this.UpdateRenderInfo(rangeInfo, renderLayout);

        var trackScore = this._trackScoreInfo;
        if (trackScore != null)
        {
            var timings = this._timings;
            var timingEditingTarget = (this._editingInfo as TimingEditingInfo)?.Target;

            this._renderCommon.RangeScoreRenderInfo = new RangeScoreRenderInfo
            {
                Score = trackScore.Score.GetRangeInfo(beginTime, endTime),
                Timings = ScoreLayoutHelper.GetRenderTargets(this._renderCommon, this._partsLayout, timings, timingEditingTarget),
                NoteLines = ScoreDrawingUtil.GetVerticalLines(trackScore.Score, beginTime, endTime, this.Quantize),
                RulerLines = ScoreDrawingUtil.GetVerticalLines(trackScore.Score, beginTime, endTime, LineType.Note8th),
            };
        }

        if (redraw)
            this.Redraw();
    }

    private void UpdateRenderInfo(RenderRangeInfo render, EditorRenderLayout renerLayout)
    {
        this._renderCommon = new RenderInfoCommon
        {
            Track = this.Track,
            RenderRange = render,
            ColorInfo = this.ColorInfo,
            ScreenLayout = renerLayout,
            SelectionRange = this._rangeSelection,
            VScrollPosition = this.GetVScrollPosition(),
        };
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
        this.RelocateSeekBars();
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

        if (this._editorRenderer is { } renderer && this._renderCommon is { } renderInfo)
            renderer.Render(g, renderInfo);
    }

    /// <summary>
    /// 縦スクロール時
    /// </summary>
    /// <param name="sender">イベント発火元</param>
    /// <param name="e">イベント情報</param>
    private void OnVScroll(object sender, ScrollEventArgs e)
    {
        this._renderCommon?.OnVerticalScrollChanged(this.GetVScrollPosition());
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

        // 自動スクロールを解除
        this.CancelAudoScroll();

        this.OnRenderBeginMsChanged(time);
        this.UpdateRenderContent();
        this.RelocateSeekBars();
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
                    HorizontalZoomLevels.GetNextUpper(zoomLevel), e);
            }
            else if (e.Delta < 0)
            {
                // 横伸長率リストから次に小さい拡大率(縮小方向)を取得して適用する
                this.ChangeHorizontalScale(
                    HorizontalZoomLevels.GetNextLower(zoomLevel), e);
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
                    KeySizes.GetNextUpper(currentHeight), e);
            }
            else if (e.Delta < 0)
            {
                // 横伸長率リストから次に小さい拡大率(縮小方向)を取得して適用する
                this.ChangeKeyHeightSize(
                    KeySizes.GetNextLower(currentHeight), e);
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

            if (isHorizontal)
                // 横スクロールの場合は自動スクロールを無効化
                this.CancelAudoScroll();

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
                this.RelocateSeekBars();
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
                this.RelocateSeekBars();
            }
        }
    }

    /// <summary> 
    /// 横の拡大率を変更する
    /// </summary>
    /// <param name="newZoomLevel"></param>
    private void ChangeHorizontalScale(double newZoomLevel, MouseEventArgs e)
    {
        double oldZoomLevel = this.ScaleX;
        if (oldZoomLevel == newZoomLevel)
        {
            // 変更がない場合は処理をスキップする
            return;
        }

        var renderLayout = this._renderLayout;
        var scoreArea = renderLayout.ScoreArea;

        var mousePosition = this.GetPhysicalMousePosition(renderLayout, e);

        double width = scoreArea.Width;

        // マウス位置(%)
        double percentage = scoreArea.RelativeX(mousePosition.X) / width;

        // 現在の伸長率の尺を取得する
        double oldDuration = width * percentage / oldZoomLevel;
        // 変更後の伸長率の尺を計算する
        double newDuration = width * percentage / newZoomLevel;

        int delta = (int)Math.Round(oldDuration - newDuration);

        this.ScaleX = newZoomLevel;
        this.SetRenderBeginMsOffset(delta);
        this.OnLayoutChanged();
    }

    /// <summary>
    /// 横の拡大率を変更する
    /// </summary>
    /// <param name="newHeight"></param>
    private void ChangeKeyHeightSize(int newHeight, MouseEventArgs e)
    {
        var renderLayout = this._renderLayout;

        int oldHeight = this.KeyHeight;
        if (oldHeight == newHeight)
        {
            // 変更がない場合は処理をスキップする
            return;
        }

        var mousePosition = this.GetPhysicalMousePosition(renderLayout, e);

        double height = renderLayout.ScoreArea.Height;

        // マウス位置(%)
        double posY = mousePosition.Y;
        int areaPosY = renderLayout.ScoreArea.Y;
        double percentage = CalcRatioWithLowerOffset(posY, areaPosY, height);

        double zoom = (double)newHeight / oldHeight;

        // 現在の伸長率の尺を取得する
        double oldDuration = height * percentage;
        // 変更後の伸長率の尺を計算する
        double newDuration = oldDuration * zoom;

        int delta = (int)Math.Round(newDuration - oldDuration);

        this.KeyHeight = newHeight;
        this.SetVScrollPosition((int)((this.GetVScrollPosition() * zoom) + delta));
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
    /// <param name="mode"></param>
    private void ChangeMouseMode(MouseControlMode mode)
    {
        this.CancelEditInternal(nextMode: mode);
        (this._mouseMode, this._editingInfo) = (mode, null);
    }

    /// <summary>
    /// 編集モードを変更する
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="mode"></param>
    /// <param name="receiver"></param>
    /// <param name="value"></param>
    private void ChangeMouseMode<T>(MouseControlMode mode, T value) where T : IEditInfo
    {
        this.CancelEditInternal(nextMode: mode);
        (this._mouseMode, this._editingInfo) = (mode, value);
    }

    /// <summary>
    /// 編集モードをキャンセルする
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="receiver"></param>
    private void ClearMouseMode()
        => (this._mouseMode, this._editingInfo) = (MouseControlMode.None, default);

    /// <summary>
    /// タイミング編集情報
    /// タイミング編集時以外はnullを設定しておく
    /// </summary>
    private IEditInfo? _editingInfo;

    private int _putNoteBeginTime = 0;
    private int _putNoteKeyIndex = 0;

    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseDown(e);

        if (!this.IsFocused)
        {
            this.Focus();
            Keyboard.Focus(this);
        }
    }

    /// <summary>
    /// マウス押下時の処理
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        Debug.WriteLine("Mouse down.");

        var renderLayout = this._renderLayout;
        var scaling = renderLayout.Scaling;
        var element = (UIElement)sender;

        var cursor = this._cursor;

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            Debug.WriteLine("Mouse captured.");
            Mouse.Capture(this.SKElement);

            var mousePos = this.GetPhysicalMousePosition(renderLayout, e);

            if (IsWithinRuler(renderLayout, mousePos))
            {
                if (this._mouseMode == MouseControlMode.None)
                {
                    this.ChangeMouseMode(MouseControlMode.Seek);
                    this.RelocateSeekBarForSeeking(e);
                    e.Handled = true;
                }
            }
            else if (IsWithinEditArea(renderLayout, mousePos))
            {
                if (this.EditMode == EditMode.ScoreAndTiming)
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

                                this.ChangeMouseMode(MouseControlMode.EditTiming, new TimingEditingInfo(handle, handle.TimingInfo.EditedBeginTime100Ns)
                                {
                                    LowerTime100Ns = idx <= 1 ? 0L : this._timings[idx - 1].TimingInfo.EditedBeginTime100Ns,
                                    UpperTime100Ns = (idx == -1 || (this._timings.Count <= (idx + 1)))
                                                                    ? null : this._timings[idx + 1].TimingInfo.EditedBeginTime100Ns,
                                    OffsetX = x - handle.X,
                                });

                                e.Handled = true;
                                return;
                            }
                        }
                    }

                    this._mouseMode = MouseControlMode.PutNote;
                    var mousePosition = this.GetPhysicalMousePosition(renderLayout, e);
                    this._putNoteBeginTime = this.FindJustBeforeQuantizeSnapping(renderLayout, this.GetRenderBeginTimeMs(), mousePosition);

                    // キーのインデックスを計算する
                    // (譜面の高さ + (スクロール位置 + スクロール位置 + マウス位置 - ルーラ高)) ÷ キー高
                    this._putNoteKeyIndex = this.GetKeyIndex(renderLayout, mousePosition);

                    this.RelocateNoteRectangle(e);
                    e.Handled = true;
                }
                else if (this._mouseMode == MouseControlMode.None)
                {
                    if (this.EditMode == EditMode.AudioFeatures)
                    {
                        // TODO: 変更情報をマウス座標ではなく編集データの値で持つようにする
                        var mousePosition = this.GetPhysicalMousePosition(renderLayout, e);

                        var scoreArea = renderLayout.ScoreArea;
                        var dynamicsArea = renderLayout.DynamicsArea;

                        int beginTime = this.GetRenderBeginTimeMs();
                        int adjustedTime = GetConditionTimeRoundFrame(renderLayout, beginTime, mousePosition);
                        this.RelocateSelectionSeekBar(TimeSpan.FromMilliseconds(adjustedTime));

                        var keyboard = Keyboard.PrimaryDevice;
                        if (keyboard.Modifiers == ModifierKeys.Shift)
                        {
                            // Shiftキーが押されている場合は範囲選択モードにする
                            if (renderLayout.EditArea.IsContains(mousePosition))
                            {
                                cursor = Cursors.SizeWE;
                                var editingInfo = new RangeSelectingInfo(
                                    renderLayout.ScoreArea.IsContainsY(mousePos.Y),
                                    adjustedTime, adjustedTime);
                                this.ChangeMouseMode(MouseControlMode.RangeSelect, editingInfo);
                                this._rangeSelection = editingInfo;
                            }

                            this.UpdateRenderContent();
                            e.Handled = true;
                        }
                        else if (keyboard.Modifiers == ModifierKeys.Control)
                        {
                            // Ctrlキーが押されている場合は一括編集モードにする
                            if (this._rangeSelection is { } select)
                            {
                                if (select.IsScoreArea)
                                {
                                    if (renderLayout.ScoreArea.IsContainsY(mousePos.Y))
                                    {
                                        double pitch = this.GetPitch12FromMousePosition(renderLayout, mousePosition);

                                        (int a1, int a2) = select.GetOrdererRange();
                                        this.ChangeMouseMode(MouseControlMode.EditPitchBulkSeek, new PitchSeekingInfo(a1, a2, pitch));

                                        this.UpdateRenderContent();
                                        e.Handled = true;
                                    }
                                }
                                else
                                {
                                    if (renderLayout.HasDynamicsArea && renderLayout.DynamicsArea.IsContainsY(mousePos.Y))
                                    {
                                        (int a1, int a2) = select.GetOrdererRange();
                                        double coe = this.GetDynamicsCoeFromMousePosition(renderLayout, mousePosition);
                                        this.ChangeMouseMode(MouseControlMode.EditDynamicsBulkSeek, new DynamicsSeekingInfo(a1, a2, coe));

                                        this.UpdateRenderContent();
                                        e.Handled = true;
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (scoreArea.IsContains(mousePosition))
                            {
                                double pitch = this.GetPitchFromMousePosition(renderLayout, mousePosition);

                                this.ChangeMouseMode(MouseControlMode.EditPitch, new PitchEditingInfo(adjustedTime, pitch));

                                this.EditF0(adjustedTime, EnumerableUtil.ToEnumerable(pitch));
                            }
                            else if (dynamicsArea != null && dynamicsArea.IsContains(mousePosition))
                            {
                                double coe = this.GetDynamicsCoeFromMousePosition(renderLayout, mousePosition);
                                double frequency = DynamicsCoeToFrequency(this.Track!, coe);

                                this.ChangeMouseMode(MouseControlMode.EditDynamics, new DynamicsEditingInfo(adjustedTime, frequency));

                                this.EditDynamics(adjustedTime, EnumerableUtil.ToEnumerable(frequency));
                            }

                            this.UpdateRenderContent();
                            e.Handled = true;
                        }
                    }
                }
            }
        }

        this.SetCursor(cursor);
    }

    /// <summary>
    /// マウス移動時の処理
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
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
        {
            this.RelocateSeekBarForSeeking(e);
            e.Handled = true;
        }
        else if (this._mouseMode == MouseControlMode.PutNote)
        {
            this.RelocateNoteRectangle(e);
            e.Handled = true;
        }
        else if (this._mouseMode == MouseControlMode.EditTiming)
        {
            var renderLayout = this._renderLayout;
            var scaling = renderLayout.Scaling;

            int beginTime = this.GetRenderBeginTimeMs();

            var trackScoreInfo = this._trackScoreInfo;
            if (trackScoreInfo != null)
            {
                var editingInfo = this._editingInfo;

                if (editingInfo is TimingEditingInfo timingEditing)
                {
                    // マウスの位置を取得、オフセット分補正する
                    var mousePosition = this.GetPhysicalMousePosition(renderLayout, e).GetOffset((int)scaling.ToScaled(-timingEditing.OffsetX), 0);

                    // マウスで選択中の時間を計算し、範囲内に収める
                    int adjustedTime = GetConditionTimeRoundFrame(renderLayout, beginTime, mousePosition);
                    adjustedTime = Math.Max(adjustedTime, NeutrinoUtil.TimingTimeToMs(timingEditing.LowerTime100Ns));
                    if (timingEditing.UpperTime100Ns != null)
                        adjustedTime = Math.Min(adjustedTime, NeutrinoUtil.TimingTimeToMs(timingEditing.UpperTime100Ns.Value));

                    // レイアウト、編集中のタイミング情報を更新
                    int adjustedX = this.GetScoreLocationX(renderLayout, adjustedTime);
                    timingEditing.Target.MoveX(adjustedX);
                    timingEditing.CurrentTimeMs = adjustedTime;

                    // 再描画
                    this.Redraw();
                    e.Handled = true;
                }
            }
        }
        else if (this.EditMode == EditMode.AudioFeatures)
        {
            var renderLayout = this._renderLayout;
            var mousePosition = this.GetPhysicalMousePosition(renderLayout, e);

            if (renderLayout.EditArea.IsContains(mousePosition))
                cursor = Cursors.Pen;

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                int beginTime = this.GetRenderBeginTimeMs();
                int frameAdjustedTime = GetConditionTimeRoundFrame(renderLayout, beginTime, mousePosition);

                // this.RelocateSelectionSeekBar(TimeSpan.FromMilliseconds(frameAdjustedTime));


                var editingInfo = this._editingInfo;
                if (editingInfo is PitchEditingInfo pitchEditing)
                {
                    // ピッチ編集中
                    double currentPitch = this.GetPitchFromMousePosition(renderLayout, mousePosition);

                    int currentTime = frameAdjustedTime;
                    int previousTime = pitchEditing.PreviousTime;
                    double previousPitch = pitchEditing.PreviousPitch;

                    int beginFrameTime;
                    IEnumerable<double> frequencies;

                    int length = Math.Abs(NeutrinoUtil.MsToFrameIndex(currentTime - previousTime)) + 1;
                    if (length <= 1)
                    {
                        (beginFrameTime, frequencies) = (currentTime, EnumerableUtil.ToEnumerable(currentPitch));
                    }
                    else
                    {
                        (beginFrameTime, frequencies) = previousTime < currentTime
                            // 右方向の変更
                            ? (previousTime, EnumearteArithmeticSequence(previousPitch, currentPitch, length))
                            // 左方向への変更
                            : (currentTime, EnumearteArithmeticSequence(currentPitch, previousPitch, length));
                    }

                    this.EditF0(beginFrameTime, frequencies);

                    // 直前の編集情報を更新
                    pitchEditing.SetPrevious(currentTime, currentPitch);
                }
                else if (editingInfo is DynamicsEditingInfo dynamicsEditingInfo)
                {
                    // ピッチ編集中
                    double coe = this.GetDynamicsCoeFromMousePosition(renderLayout, mousePosition);
                    double currentFrequency = DynamicsCoeToFrequency(this.Track!, coe);

                    int currentTime = frameAdjustedTime;
                    int previousTime = dynamicsEditingInfo.PreviousTime;
                    double previousDynamics = dynamicsEditingInfo.PreviousDynamics;

                    int beginFrameTime;
                    IEnumerable<double> frequencies;

                    int length = Math.Abs(NeutrinoUtil.MsToFrameIndex(currentTime - previousTime)) + 1;
                    if (length <= 1)
                    {
                        (beginFrameTime, frequencies) = (currentTime, EnumerableUtil.ToEnumerable(currentFrequency));
                    }
                    else
                    {
                        (beginFrameTime, frequencies) = previousTime < currentTime
                            // 右方向の変更
                            ? (previousTime, EnumearteArithmeticSequence(previousDynamics, currentFrequency, length))
                            // 左方向への変更
                            : (currentTime, EnumearteArithmeticSequence(currentFrequency, previousDynamics, length));
                    }

                    this.EditDynamics(beginFrameTime, frequencies);

                    // 直前の編集情報を更新
                    dynamicsEditingInfo.SetPrevious(currentTime, currentFrequency);
                }
                else if (editingInfo is RangeSelectingInfo rangeSelection)
                {
                    // 範囲選択モード
                    cursor = Cursors.SizeWE;
                    rangeSelection.UpdateEndTime(frameAdjustedTime);
                }
                else if (editingInfo is PitchSeekingInfo pitchSeeking)
                {
                    // ピッチ編集中
                    double currentPitch = this.GetPitch12FromMousePosition(renderLayout, mousePosition);

                    int beginTime2 = pitchSeeking.EndTime < pitchSeeking.EndTime ? pitchSeeking.EndTime : pitchSeeking.BeginTime;
                    int length = NeutrinoUtil.MsToFrameIndex(Math.Abs(pitchSeeking.EndTime - pitchSeeking.BeginTime) + 1);

                    this.AddPitch12(beginTime2, Enumerable.Repeat(currentPitch - pitchSeeking.Pitch, length));

                    pitchSeeking.SetPitch(currentPitch);
                }
                else if (editingInfo is DynamicsSeekingInfo dynamicsSeeking)
                {
                    // ダイナミクス一括編集中
                    double currentCoe = this.GetDynamicsCoeFromMousePosition(renderLayout, mousePosition);

                    int beginTime2 = dynamicsSeeking.EndTime < dynamicsSeeking.EndTime ? dynamicsSeeking.EndTime : dynamicsSeeking.BeginTime;
                    int length = NeutrinoUtil.MsToFrameIndex(Math.Abs(dynamicsSeeking.EndTime - dynamicsSeeking.BeginTime) + 1);

                    this.AddDynamicsCoe(beginTime2, Enumerable.Repeat(currentCoe - dynamicsSeeking.Coe, length));

                    dynamicsSeeking.SetCoe(currentCoe);
                }

                this.UpdateRenderContent();
                e.Handled = true;
            }
            else if (e.LeftButton == MouseButtonState.Released)
            {
                if (this._mouseMode == MouseControlMode.None)
                {
                    var rangeSelection = this._rangeSelection;
                    if (rangeSelection != null)
                    {
                        var keyboard = Keyboard.PrimaryDevice;
                        if (keyboard.Modifiers == ModifierKeys.Control)
                        {
                            // Ctrlキーが押されている場合は一括編集モードにする
                            if (renderLayout.ScoreArea.IsContainsY(mousePosition.Y))
                            {
                                cursor = Cursors.SizeNS;
                            }
                            else if (renderLayout.HasDynamicsArea && renderLayout.DynamicsArea.IsContainsY(mousePosition.Y))
                            {
                                cursor = Cursors.SizeNS;
                            }
                        }
                    }
                }
            }
        }
        else
        {
            // this.RelocatePuttableNoteRectangle(e);

            var scaling = this._renderCommon.ScreenLayout.Scaling;
            var renderLayout = this._renderLayout;
            var mousePosition = this.GetPhysicalMousePosition(renderLayout, e);

            if (IsWithinEditArea(this._renderLayout, mousePosition))
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
                            bool isSelected = noSelected && target.IsCollisionDetection(mousePosition.X, mousePosition.Y);
                            if (isSelected)
                                noSelected = false;

                            target.IsSelected = isSelected;
                        }

                        // 選択中のタイミングがある場合はカーソルを変更する
                        if (!noSelected)
                            cursor = Cursors.SizeWE;

                        // 再描画
                        this.Redraw();
                        e.Handled = true;
                    }
                }
            }
        }

        this.SetCursor(cursor);

        e.Handled = true;
    }

    /// <summary>
    /// マウスを離した時の処理
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        //Debug.WriteLine("Mouse up.");

        if (e.LeftButton == MouseButtonState.Released)
        {
            this.SKElement.ReleaseMouseCapture();
            Debug.WriteLine("Mouse released.");

            switch (this._mouseMode)
            {
                case MouseControlMode.Seek:
                    // その他
                    this._mouseTimer.Stop();
                    this.SelectionTime = this._tempSelectionTime;
                    this.ClearMouseMode();
                    e.Handled = true;
                    return;

                case MouseControlMode.PutNote:
                    // ノート配置中
                    this.ClearMouseMode();
                    e.Handled = true;
                    return;

                case MouseControlMode.EditTiming:
                    // タイミング編集中
                    this.DetermineTimingEdit();
                    e.Handled = true;
                    return;

                case MouseControlMode.EditPitch:
                    // ピッチ編集中
                    this.DeterminePitchEdit();
                    e.Handled = true;
                    return;

                case MouseControlMode.EditDynamics:
                    // ダイナミクス編集中
                    this.DetermineDynamicsEdit();
                    e.Handled = true;
                    return;

                case MouseControlMode.RangeSelect:
                    // 範囲選択完了
                    this.DetermineRangeSelect();
                    e.Handled = true;
                    return;

                case MouseControlMode.EditPitchBulkSeek:
                    // ピッチの一括シーク終了
                    this.DeterminePitchSeeking();
                    e.Handled = true;
                    return;

                case MouseControlMode.EditDynamicsBulkSeek:
                    // ダイナミクスの一括シーク終了
                    this.DetermineDynamicsSeeking();
                    e.Handled = true;
                    return;
            }
        }
    }

    /// <summary>
    /// キー押下時の処理
    /// </summary>
    /// <param name="e"></param>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        Debug.WriteLine(e);
    }

    /// <summary>
    /// キーを離した時の処理
    /// </summary>
    /// <param name="e"></param>
    protected override void OnPreviewKeyUp(KeyEventArgs e)
    {
        var keyboard = e.KeyboardDevice;

        if (this.EditMode == EditMode.AudioFeatures)
        {
            if (this._rangeSelection is { } select)
            {
                if (select.IsScoreArea)
                {
                    // ピッチ編集エリアを選択中

                    if (keyboard.Modifiers == ModifierKeys.None)
                    {
                        (int beginTime, int endTime) = select.GetOrdererRange();
                        int lenggth = NeutrinoUtil.MsToFrameIndex(endTime - beginTime + 1);

                        if (e.Key is (Key.Up or Key.Down))
                        {
                            // 上下キーのみの場合は1音分上下させる
                            this.AddPitch12(beginTime, Enumerable.Repeat(e.Key == Key.Up ? 1.0d : -1.0d, lenggth));

                            this.Resync();
                            this.UpdateRenderContent();
                            e.Handled = true;
                        }
                    }
                    else if (keyboard.Modifiers == ModifierKeys.Shift)
                    {
                        (int beginTime, int endTime) = select.GetOrdererRange();
                        int lenggth = NeutrinoUtil.MsToFrameIndex(endTime - beginTime + 1);

                        if (e.Key is (Key.Up or Key.Down))
                        {
                            // Shift+上下キーの場合は1オクターブ分上下させる

                            this.AddPitch12(beginTime, Enumerable.Repeat(e.Key == Key.Up ? 12.0d : -12.0d, lenggth));

                            this.Resync();
                            this.UpdateRenderContent();
                            e.Handled = true;
                        }
                    }
                }
            }
        }
    }

    private Cursor _cursor = Cursors.Arrow;
    private RangeSelectingInfo? _rangeSelection;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetCursor(Cursor cursor)
    {
        if (this._cursor != cursor)
            this.Cursor = this._cursor = cursor;
    }

    private void RelocateSeekBarForSeeking(MouseEventArgs e)
    {
        var renderLayout = this._renderLayout;
        int beginTime = this.GetRenderBeginTimeMs();

        var scaling = renderLayout.Scaling;
        double width = renderLayout.ScoreArea.Width;

        var mousePosition = this.GetPhysicalMousePosition(renderLayout, e);
        double posX = renderLayout.ScoreArea.RelativeX(mousePosition.X);

        int marginWidth = (int)scaling.ToUnscaled(AutoScrollInnerWidth);
        if (posX < marginWidth)
        {
            this._mouseTimer.Start();
        }
        else if ((width - marginWidth) < posX)
        {
            this._mouseTimer.Start();
        }

        int conditionTime = GetConditionTime(renderLayout, beginTime, mousePosition);

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

        this.UpdateTempSelectionTime(TimeSpan.FromMilliseconds(conditionTime));
    }

    private void RelocateNoteRectangle(MouseEventArgs e)
    {
        var renderLayout = this._renderLayout;
        int beginTime = this.GetRenderBeginTimeMs();

        var trackScoreInfo = this._trackScoreInfo;
        if (trackScoreInfo != null)
        {
            var mousePosition = this.GetPhysicalMousePosition(renderLayout, e);
            int conditionTime = this._putNoteBeginTime;
            int keyIndex = this._putNoteKeyIndex;

            int currentCursorPosition = GetConditionTime(renderLayout, beginTime, mousePosition);

            decimal duration = ScoreDrawingUtil.GetNoteDuration(trackScoreInfo.Score, conditionTime, this.Quantize, currentCursorPosition);

            // 図形の位置を変更
            this.MoveBorder(renderLayout, keyIndex, conditionTime - beginTime, (int)duration + 1);
        }
    }

    private int GetKeyIndex(EditorRenderLayout renderLayout, LayoutPoint mousePosition)
    {
        int scrollPosition = this.GetVScrollPosition();

        return (int)(renderLayout.ScoreArea.Height - ((scrollPosition + renderLayout.ScoreArea.RelativeY(mousePosition.Y)) / renderLayout.PhysicalKeyHeight));
    }

    private void MoveBorder(EditorRenderLayout renderLayout, int keyIndex, int time, int duration)
    {
        var scaling = renderLayout.Scaling;

        int keyIndexForTop = RenderConfig.KeyCount - keyIndex - 1;
        double posY = scaling.ToScaled((keyIndexForTop * renderLayout.PhysicalKeyHeight) - this.GetVScrollPosition() + renderLayout.ScoreArea.Y);

        double posX = renderLayout.GetRenderPosXFromTime(time);
        double width = renderLayout.GetRenderPosXFromTime(duration);

        var element = this.PART_Rectangle;
        var beforeMargin = element.Margin;
        element.Margin = new(posX, posY, beforeMargin.Right, beforeMargin.Bottom);
        element.Width = width + 1;
        element.Height = (int)scaling.ToScaled(renderLayout.PhysicalKeyHeight) + 1;

        element.Visibility = Visibility.Visible;
    }

    private int FindJustBeforeQuantizeSnapping(EditorRenderLayout renderLayout, int beginTime, LayoutPoint mousePosition)
    {
        int conditionTime = GetConditionTime(renderLayout, beginTime, mousePosition);

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

    private static int GetConditionTime(EditorRenderLayout renderLayout, int timeMs, LayoutPoint mousePosition)
    {
        var scoreArea = renderLayout.ScoreArea;

        double width = scoreArea.Width;
        double percentageX = scoreArea.RelativeX(mousePosition.X) / width;

        return Math.Max(0, timeMs + renderLayout.Scaling.ToRenderImageScaling(width * percentageX / renderLayout.WidthStretch));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetConditionTimeRoundFrame(EditorRenderLayout renderLayout, int timeMs, LayoutPoint mousePosition)
    {
        // 最小単位
        const int unit = NeutrinoConfig.FramePeriod;

        return (int)Math.Round(GetConditionTime(renderLayout, timeMs, mousePosition) / (double)unit) * unit;
    }

    /// <summary>
    /// マウス位置からピッチを取得する。
    /// </summary>
    /// <param name="renderLayout">レイアウト情報</param>
    /// <param name="mousePosition">マウス位置</param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetPitchFromMousePosition(EditorRenderLayout renderLayout, LayoutPoint mousePosition)
    {
        double pitch = this.GetPitch12FromMousePosition(renderLayout, mousePosition);
        return AudioDataConverter.Pitch12ToFrequency(pitch);
    }

    /// <summary>
    /// マウス位置からピッチを取得する。
    /// </summary>
    /// <param name="renderLayout">レイアウト情報</param>
    /// <param name="mousePosition">マウス位置</param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetPitch12FromMousePosition(EditorRenderLayout renderLayout, LayoutPoint mousePosition)
    {
        int scrollPosition = this.GetVScrollPosition();
        var scoreArea = renderLayout.ScoreArea;
        int keyHeight = renderLayout.PhysicalKeyHeight;

        return (double)(renderLayout.ScoreImage.Height - (scrollPosition + scoreArea.RelativeY(mousePosition.Y)) - (keyHeight / 2)) / keyHeight;
    }

    /// <summary>
    /// ダイナミクス値を取得する。
    /// </summary>
    /// <param name="renderLayout">レイアウト</param>
    /// <param name="mousePosition">マウス位置</param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetDynamicsCoeFromMousePosition(EditorRenderLayout renderLayout, LayoutPoint mousePosition)
    {
        double areaY = renderLayout.DynamicsArea!.RelativeY(mousePosition.Y);
        return 1d - (((double)areaY) / renderLayout.DynamicsArea.Height);
    }

    /// <summary>
    /// タイミング編集を確定する
    /// </summary>
    public void DetermineTimingEdit()
    {
        if (this._editingInfo is TimingEditingInfo editing)
        {
            var track = this.Track;
            // HACK: 処理見直し
            track?.ChangeTiming(Array.IndexOf(track.Timings, editing.Target.TimingInfo), editing.CurrentTimeMs);
        }

        this.ClearMouseMode();
    }

    /// <summary>
    /// タイミング編集をキャンセルする
    /// </summary>
    public void CancelTimingEdit()
    {
        var editing = this._editingInfo as TimingEditingInfo;
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

        this.ClearMouseMode();
    }

    /// <summary>
    /// F0値を編集する
    /// </summary>
    /// <param name="time">開始時間</param>
    /// <param name="pitches">F0値</param>
    private void EditF0(int time, IEnumerable<double> pitches)
    {
        var track = this.Track;

        if (track is NeutrinoV1Track v1Track)
            v1Track.EditF0(time, pitches.ToArray());
        else if (track is NeutrinoV2Track v2Track)
            v2Track.EditF0(time, pitches.Select(i => (float)i).ToArray());
    }

    /// <summary>
    /// F0値を編集する。
    /// </summary>
    /// <param name="time">開始時間</param>
    /// <param name="pitches">F0値</param>
    private void AddPitch12(int time, IEnumerable<double> pitches)
    {
        var track = this.Track;

        if (track is NeutrinoV1Track v1Track)
            v1Track.AddPitch12(time, pitches.ToArray());
        else if (track is NeutrinoV2Track v2Track)
            v2Track.AddPitch12(time, pitches.Select(i => (float)i).ToArray());
    }

    /// <summary>
    /// ピッチ編集を確定する
    /// </summary>
    public void DeterminePitchEdit()
    {
        this.Resync();

        this.ClearMouseMode();
    }

    private void Resync()
    {
        if (this.Track is { } track)
        {
            track.ReSynseEditing();
        }
    }

    /// <summary>
    /// ピッチ編集をキャンセルする
    /// </summary>
    public void CancelPitchEdit()
    {
        if (this.Track is { } track && this._editingInfo is PitchEditingInfo editing)
        {
            foreach (var phrase in track.Phrases.Where(p => p.IsF0Editing()))
            {
                phrase.CancelEditingF0();
            }

            // 再描画
            this.UpdateRenderContent();
        }

        this.ClearMouseMode();
    }

    /// <summary>
    /// ダイナミクス値を編集する
    /// </summary>
    /// <param name="time">開始時間</param>
    /// <param name="dynamics">ダイナミクス値</param>
    private void EditDynamics(int time, IEnumerable<double> dynamics)
    {
        var track = this.Track;

        if (track is NeutrinoV1Track v1Track)
            v1Track.EditDynamics(time, dynamics.ToArray());
        else if (track is NeutrinoV2Track v2Track)
            v2Track.EditDynamics(time, dynamics.Select(i => (float)i).ToArray());

        this.UpdateRenderContent();
    }

    /// <summary>
    /// ダイナミクス値に加算する
    /// </summary>
    /// <param name="time">開始時間</param>
    /// <param name="coeDelta">F0値</param>
    private void AddDynamicsCoe(int time, IEnumerable<double> coeDelta)
    {
        var track = this.Track;

        if (track is NeutrinoV1Track v1Track)
            v1Track.AddDynamicsCoe(time, coeDelta.ToArray());
        else if (track is NeutrinoV2Track v2Track)
            v2Track.AddDynamicsCoe(time, coeDelta.Select(i => (float)i).ToArray());
    }

    /// <summary>
    /// ダイナミクス編集を確定する
    /// </summary>
    public void DetermineDynamicsEdit()
    {
        if (this.Track is { } track)
        {
            track.ReSynseEditing();
        }

        this.ClearMouseMode();
    }

    /// <summary>
    /// ダイナミクス編集をキャンセルする
    /// </summary>
    public void CancelDynamicsEdit()
    {
        if (this.Track is { } track && this._editingInfo is DynamicsEditingInfo editing)
        {
            foreach (var phrase in track.Phrases.Where(p => p.IsDynamicsEditing()))
            {
                phrase.CancelEditingDynamics();
            }

            // 再描画
            this.UpdateRenderContent();
        }

        this.ClearMouseMode();
    }

    /// <summary>
    /// ピッチ編集をキャンセルする
    /// </summary>
    public void CancelSeekingPitch()
    {
        if (this.Track is { } track && this._editingInfo is PitchSeekingInfo editing)
        {
            foreach (var phrase in track.Phrases.Where(p => p.IsF0Editing()))
            {
                phrase.CancelEditingF0();
            }

            // 再描画
            this.UpdateRenderContent();
        }

        this.ClearMouseMode();
    }

    /// <summary>
    /// ピッチ編集をキャンセルする
    /// </summary>
    public void CancelSeekingDynamics()
    {
        if (this.Track is { } track && this._editingInfo is DynamicsSeekingInfo editing)
        {
            foreach (var phrase in track.Phrases.Where(p => p.IsDynamicsEditing()))
            {
                phrase.CancelEditingDynamics();
            }

            // 再描画
            this.UpdateRenderContent();
        }

        this.ClearMouseMode();
    }

    /// <summary>
    /// 範囲選択を完了する。
    /// </summary>
    private void DetermineRangeSelect()
    {
        this.ClearMouseMode();
    }

    private void DeterminePitchSeeking()
    {
        if (this.Track is { } track)
        {
            track.ReSynseEditing();
        }

        this.ClearMouseMode();
    }

    private void DetermineDynamicsSeeking()
    {
        if (this.Track is { } track)
        {
            track.ReSynseEditing();
        }

        this.ClearMouseMode();
    }

    /// <summary>
    /// 範囲選択をキャンセルする。
    /// </summary>
    public void CancelRangeSelect()
    {
        this.ClearMouseMode();
    }

    /// <summary>
    /// 編集操作をキャンセルする(Esc押下時)
    /// </summary>
    private void CancelEditInternal(MouseControlMode nextMode = MouseControlMode.None)
    {
        // エスケープキーが押下された場合
        // マウス操作の作業をキャンセルする
        switch (this._mouseMode)
        {
            case MouseControlMode.None:
                if (nextMode == MouseControlMode.None)
                    break; // 次のモードがない場合は後続処理に進む
                return; // 次のモードが指定されている場合は何もしない

            case MouseControlMode.EditTiming:
                this.CancelTimingEdit();
                return;

            case MouseControlMode.EditPitch:
                this.CancelPitchEdit();
                return;

            case MouseControlMode.EditDynamics:
                this.CancelDynamicsEdit();
                return;

            case MouseControlMode.RangeSelect:
                this.CancelRangeSelect();
                return;

            case MouseControlMode.EditPitchBulkSeek:
                this.CancelSeekingPitch();
                return;

            case MouseControlMode.EditDynamicsBulkSeek:
                this.CancelSeekingDynamics();
                return;
        }

        if (this._rangeSelection != null)
            this._rangeSelection = null;
    }

    public void CancelEdit()
    {
        this.CancelEditInternal();
        this.UpdateRenderContent(redraw: true);
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

        var renderLayout = this._renderLayout;

        var scoreArea = renderLayout.ScoreArea;

        double width = scoreArea.Width;
        double posX = scoreArea.RelativeX(this.GetPhysicalMousePosition(renderLayout).X);

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

    /// <summary>
    /// マウス座標がルーラ内にあるかどうかを判定する。
    /// </summary>
    /// <param name="renderLayout">レイアウト情報</param>
    /// <param name="mousePosition">マウス位置</param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsWithinRuler(EditorRenderLayout renderLayout, LayoutPoint mousePosition)
        => renderLayout.RulerArea.IsContainsY(mousePosition.Y);

    /// <summary>
    /// マウス位置が編集エリア内にあるかどうかを判定する。
    /// </summary>
    /// <param name="renderLayout">レイアウト情報</param>
    /// <param name="mousePosition">マウス位置</param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsWithinEditArea(EditorRenderLayout renderLayout, LayoutPoint mousePosition)
        => renderLayout.EditArea.IsContainsY(mousePosition.Y);

    private static string GetLyrics(ScoreInfo score)
        => string.Concat(score.Notes.Select(i => i.Lyrics.Length > 1 ? $"({i.Lyrics})" : i.Lyrics));

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

        var node = trackScoreInfo.Score.Notes.First!;

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
        this.UpdateTempSelectionTime(TimeSpan.FromMilliseconds(note.BeginTime));
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
    private static IEnumerable<T> EnumearteArithmeticSequence<T>(T begin, T end, int length)
        where T : INumber<T>
    {
        T delta = end - begin;
        T coe = T.CreateChecked(length - 1);

        for (int idx = 0; idx < length; ++idx)
            yield return (delta * T.CreateChecked(idx) / coe) + begin;
    }

    private static LayoutPoint ToUnscaled(RenderScaleInfo scaling, Point point)
        => new((int)scaling.ToUnscaled(point.X), (int)scaling.ToUnscaled(point.Y));

    private static LayoutPoint ToUnscaled(RenderScaleInfo scaling, ref Point point)
        => new((int)scaling.ToUnscaled(point.X), (int)scaling.ToUnscaled(point.Y));

    private LayoutPoint GetPhysicalMousePosition(EditorRenderLayout renderLayout)
    {
        var position = Mouse.GetPosition(this.SKElement);
        return ToUnscaled(renderLayout.Scaling, ref position);
    }

    private LayoutPoint GetPhysicalMousePosition(EditorRenderLayout renderLayout, MouseEventArgs e)
    {
        var position = e.GetPosition(this.SKElement);
        return ToUnscaled(renderLayout.Scaling, ref position);
    }

    public static double DynamicsCoeToFrequency(INeutrinoTrack track, double coe)
        => track switch
        {
            NeutrinoV1Track => NeutrinoUtil.LinearMgcCoeToLogValue(coe),
            NeutrinoV2Track => NeutrinoUtil.LinearMspecCoeToLogValue((float)coe),
            _ => throw new NotSupportedException(),
        };

    /// <summary>
    /// 自動スクロールを無効にする。
    /// </summary>
    public void CancelAudoScroll()
    {
        if (this.IsAutoScroll)
        {
            this.IsAutoScroll = false;
        }
    }
}
