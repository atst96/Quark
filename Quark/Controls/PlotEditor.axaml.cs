using System.Collections.Generic;
using System;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Quark.Constants;
using Quark.ImageRender;
using Quark.Projects.Tracks;
using Avalonia;
using Quark.Drawing;
using Avalonia.Input;
using Quark.Controls.Editing;
using Quark.Utils;
using System.Diagnostics;
using System.Linq;
using Quark.Converters;
using static Quark.Controls.EditorRenderLayout;
using System.Runtime.CompilerServices;
using System.Numerics;
using Quark.Extensions;
using SkiaSharp;
using Avalonia.Media;

namespace Quark.Controls;
public partial class PlotEditor : UserControl
{
    private double MaxHScrollHeight = 1000;

    private DispatcherTimer _mouseTimer;

    private ColorInfo ColorInfo { get; } = new ColorInfo();

    private bool _isLoaded = false;
    private long _framesCount = 0;
    private TrackScoreInfo _trackScoreInfo;
    private RenderInfoCommon _renderCommon;
    private RangeScoreRenderInfo? _rangeInfo;
    private EditorPartsLayoutResolver _partsLayout = null!;
    private TimingEditNegotiator _timingEdit;

    /// <summary>
    /// エディタのレイアウト情報。親要素のサイズ変更時や編集モード変更時に更新される。
    /// </summary>
    private EditorRenderLayout? _renderLayout = null;

    /// <summary>
    /// トラックの描画情報。
    /// </summary>
    private TrackRenderInfo? _trackRenderInfo = null;

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

        this._timingEdit = new(this.PART_Notes);

        // HACK:
        // this._displayDpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
        this._displayDpi = 1.0;

        // マウス操作時のタイマー
        this._mouseTimer = new(TimeSpan.FromMilliseconds(20d), DispatcherPriority.Render, this.OnMouseTimerTicked)
        {
            IsEnabled = false,
        };
        this.SKElement.Rendering += this.SKElement_Rendering;

        this.InitializeRenderer(null);
    }

    private void SKElement_Rendering(object? sender, SKCanvas e)
    {
        if (Design.IsDesignMode)
            return;

        //if (this._editorRenderer is { } renderer && this._renderCommon is { } renderCommon)
        //{
        //    renderer.Render(e, renderCommon);
        //}
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        // HACK
        this._renderLayout = this.CreateRenderLayout();
    }

    #region Properties

    /// <summary>トラック情報</summary>
    public INeutrinoTrack? Track
    {
        get => this.GetValue(TrackProperty);
        set => this.SetValue(TrackProperty, value);
    }

    /// <summary><see cref="Track"/>のプロパティ情報</summary>
    public static readonly StyledProperty<INeutrinoTrack?> TrackProperty
        = AvaloniaProperty.Register<PlotEditor, INeutrinoTrack?>(nameof(INeutrinoTrack), defaultValue: null);

    /// <summary>
    /// <seealso cref="Track"/>プロパティ変更時
    /// </summary>
    /// <param name="d">プロパティ変更時</param>
    /// <param name="e">イベント情報</param>
    private void OnTrackPropertyChanged(INeutrinoTrack? prevTrack, INeutrinoTrack? newTrack)
    {
        var @this = this;

        if (prevTrack != null)
        {
            prevTrack.FeatureChanged -= this.OnTrackFeatureChanged;
            prevTrack.TimingEstimated -= this.OnTrackTimingEstimated;
        }

        this.InitializeRenderer(newTrack);
        if (newTrack != null)
        {
            newTrack.FeatureChanged += this.OnTrackFeatureChanged;
            newTrack.TimingEstimated += this.OnTrackTimingEstimated;
            this.LoadTrack(newTrack);
        }

        this.UpdateScrollLayout();
        this.RelocateSeekBars();
    }

    /// <summary>カラーテーマ</summary>
    public ColorTheme ColorTheme
    {
        get => this.GetValue<ColorTheme>(ColorThemeProperty);
        set => this.SetValue(ColorThemeProperty, value);
    }

    /// <summary><see cref="ColorTheme"/>のプロパティ</summary>
    public static readonly AvaloniaProperty<ColorTheme> ColorThemeProperty = AvaloniaProperty.Register<PlotEditor, ColorTheme>(nameof(ColorTheme), ColorTheme.Light);

    /// <summary>シークバーの選択位置</summary>
    public TimeSpan SelectionTime
    {
        get => this.GetValue(SelectionTimeProperty);
        set => this.SetValue(SelectionTimeProperty, value);
    }

    /// <summary><see cref="SelectionTime"/>のプロパティ</summary>
    public static readonly StyledProperty<TimeSpan> SelectionTimeProperty
        = AvaloniaProperty.Register<PlotEditor, TimeSpan>(nameof(SelectionTime), defaultValue: TimeSpan.Zero);

    /// <summary>
    /// <seealso cref="SelectionTime"/>プロパティ変更時
    /// </summary>
    private void OnSelectionTimePropertyChanged(TimeSpan newTime)
        => this.UpdateTempSelectionTime(newTime);

    /// <summary>
    /// スクロール追従の有効／無効
    /// </summary>
    public bool IsAutoScroll
    {
        get => this.GetValue(IsAutoScrollProperty);
        set => this.SetValue(IsAutoScrollProperty, value);
    }

    /// <summary>
    /// <seealso cref="IsAutoScroll"/>の依存関係プロパティ
    /// </summary>
    public static readonly StyledProperty<bool> IsAutoScrollProperty
        = AvaloniaProperty.Register<PlotEditor, bool>(nameof(IsAutoScroll), defaultValue: false);

    /// <summary>横伸長率の一時変数</summary>
    private double _tempScaleX = DefaultScaleX;

    /// <summary>横方向のズームレベル</summary>
    public double ScaleX
    {
        get => this.GetValue(ScaleXProperty);
        set => this.SetValue(ScaleXProperty, this._tempScaleX = value);
    }

    /// <summary><seealso cref="ScaleX"/>の依存関係プロパティ</summary>
    public static readonly StyledProperty<double> ScaleXProperty
        = AvaloniaProperty.Register<PlotEditor, double>(nameof(ScaleX), defaultValue: 1.0d, validate: e => e > 0d);

    /// <summary>
    /// <seealso cref="ScaleX"/>プロパティ変更時
    /// </summary>
    /// <param name="d">プロパティ変更要素</param>
    /// <param name="e">イベント情報</param>
    private void OnScaleXChanged(double newValue)
    {
        if (this.UpdateInternalScaleX(newValue))
        {
            this.OnLayoutChanged();
        }
    }
    /// <summary>キー描画高の一時変数</summary>
    private int _tempKeyHeight = DefaultKeyHeight;

    /// <summary>1キーあたりの高さ(px)</summary>
    public int KeyHeight
    {
        get => (int)this.GetValue(KeyHeightProperty);
        set => this.SetValue(KeyHeightProperty, this._tempKeyHeight = value);
    }

    /// <summary><seealso cref="KeyHeight"/>の依存関係プロパティ</summary>
    public static readonly StyledProperty<int> KeyHeightProperty
        = AvaloniaProperty.Register<PlotEditor, int>(nameof(KeyHeight), defaultValue: DefaultKeyHeight);

    /// <summary>
    /// <seealso cref="KeyHeight"/>プロパティ変更時
    /// </summary>
    /// <param name="d">プロパティ変更要素</param>
    /// <param name="e">イベント情報</param>
    private void OnKeyHeightChanged(int oldValue, int newValue)
    {
        if (this.UpdateInternalKeyHeight(newValue))
        {
            this.SetVerticalScrollPosition((int)(this.GetVerticalScrollPosition() * ((double)newValue / oldValue)));

            // 内部で変更済み出ない場合
            this.OnLayoutChanged();
        }
    }

    /// <summary>編集モード</summary>
    public EditMode EditMode
    {
        get => this.GetValue(EditModeProperty);
        set => this.SetValue(EditModeProperty, value);
    }

    /// <summary><see cref="EditMode"/>の依存関係プロパティ</summary>
    public static readonly StyledProperty<EditMode> EditModeProperty
        = AvaloniaProperty.Register<PlotEditor, EditMode>(nameof(EditMode), defaultValue: EditMode.ScoreAndTiming);

    private void EditModePropertyChanged()
    {
        this.OnEditModeChanged();
    }

    /// <summary>クオンタイズ値</summary>
    public LineType Quantize
    {
        get => this.GetValue(QuantizeProperty);
        set => this.SetValue(QuantizeProperty, value);
    }

    /// <summary><seealso cref="Quantize"/>のプロパティ</summary>
    public static readonly StyledProperty<LineType> QuantizeProperty =
        AvaloniaProperty.Register<PlotEditor, LineType>(nameof(Quantize), defaultValue: LineType.Note16th);

    /// <summary>
    /// <seealso cref="Quantize"/>変更時
    /// </summary>
    /// <param name="d"></param>
    /// <param name="e"></param>
    private void OnQuantizeChanged()
    {
        // 再描画
        this.UpdateRenderContent();
        this.RedrawAll();
    }

    /// <summary>スナッピングの切り替え</summary>
    public bool IsQuantizeSnapping
    {
        get => this.GetValue(IsQuantizeSnappingProperty);
        set => this.SetValue(IsQuantizeSnappingProperty, value);
    }

    /// <summary><seealso cref="IsQuantizeSnapping"/>のプロパティ</summary>
    public static readonly StyledProperty<bool> IsQuantizeSnappingProperty
        = AvaloniaProperty.Register<PlotEditor, bool>(nameof(IsQuantizeSnapping), defaultValue: false);

    /// <summary>再生中モードの有効／無効</summary>
    public bool IsPlayMode
    {
        get => this.GetValue(IsPlayModeProperty);
        set => this.SetValue(IsPlayModeProperty, value);
    }

    /// <summary><see cref="IsPlayMode"/>の依存関係プロパティ</summary>
    public static readonly StyledProperty<bool> IsPlayModeProperty
        = AvaloniaProperty.Register<PlotEditor, bool>(nameof(IsPlayMode));

    /// <summary>
    /// <see cref="IsPlayMode"/>プロパティ変更時
    /// </summary>
    private void OnIsPlayModePropertyChanged()
        => this.RelocatePlayingSeekBar();
    /// <summary>現在再生時点の時刻</summary>
    public TimeSpan PlayingTime
    {
        get => (TimeSpan)this.GetValue(PlayingTimeProperty);
        set => this.SetValue(PlayingTimeProperty, value);
    }

    /// <summary><see cref="PlayingTime"/>の依存関係プロパティ</summary>
    public static readonly StyledProperty<TimeSpan> PlayingTimeProperty =
        AvaloniaProperty.Register<PlotEditor, TimeSpan>(nameof(PlayingTime));

    /// <summary>
    /// <see cref="PlayingTime"/>プロパティ変更時
    /// </summary>
    private void OnPlayingTimePropertyChanged(TimeSpan prevValue, TimeSpan newValue)
        => this.RelocatePlayingSeekBar(newValue, prevValue);

    /// <summary>
    /// プロパティ変更時
    /// </summary>
    /// <param name="change"></param>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        var proeprty = change.Property;

        if (proeprty == TrackProperty)
        {
            // Trackが変更された
            this.OnTrackPropertyChanged(change.GetOldValue<INeutrinoTrack?>(), change.GetNewValue<INeutrinoTrack?>());
        }
        else if (proeprty == SelectionTimeProperty)
        {
            // 選択位置が変更された
            this.OnSelectionTimePropertyChanged(change.GetNewValue<TimeSpan>());
        }
        else if (proeprty == IsAutoScrollProperty)
        {
            // pass
        }
        else if (proeprty == ScaleXProperty)
        {
            this.OnScaleXChanged(change.GetNewValue<double>());
        }
        else if (proeprty == KeyHeightProperty)
        {
            this.OnKeyHeightChanged(change.GetOldValue<int>(), change.GetNewValue<int>());
        }
        else if (proeprty == EditModeProperty)
        {
            this.EditModePropertyChanged();
        }
        else if (proeprty == QuantizeProperty)
        {
            this.OnQuantizeChanged();
        }
        else if (proeprty == IsPlayModeProperty)
        {
            this.OnIsPlayModePropertyChanged();
        }
        else if (proeprty == PlayingTimeProperty)
        {
            this.OnPlayingTimePropertyChanged(change.GetOldValue<TimeSpan>(), change.GetNewValue<TimeSpan>());
        }

        base.OnPropertyChanged(change);
    }

    #endregion Properties

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

        this._timingEdit.UpdateTimings(track.Timings);

        this._framesCount = track.GetTotalFramesCount();

        // this.PART_LyricsTextBox.Text = GetLyrics(trackInfo.Score);

        this.UpdateRenderContent();
        this.RedrawAll();
    }

    private void LoadTiming()
    {
        var track = this.Track;
        if (track == null)
            return;

        var trackInfo = this._trackScoreInfo;

        this._framesCount = track.GetTotalFramesCount();

        this._timingEdit.UpdateTimings(track.Timings);

        // this.PART_LyricsTextBox.Text = GetLyrics(trackInfo.Score);

        this.UpdateRenderContent();
        this.UpdateScrollLayout();
        this.RelocateSeekBars();
        this.RedrawAll();
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
        if (renderLayout == null)
            return;

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
        this.RedrawAll();
    }

    /// <summary>
    /// シークバーを移動して表示させる。
    /// </summary>
    /// <param name="seekBar">シークバー要素</param>
    /// <param name="physicalX">X座標(物理ピクセル)</param>
    /// <param name="layout">レイアウト</param>
    /// <param name="toDisplay">表示させるかどうかのフラグ</param>
    // private void MoveSeekBar(IsolatedSeekBar seekBar, double physicalX, EditorRenderLayout layout, bool toDisplay)
    private void MoveSeekBar(Control seekBar, double physicalX, EditorRenderLayout layout, bool toDisplay)
    {
        if (!toDisplay)
        {
            seekBar.IsVisible = false;
            return;
        }

        var scaling = this._renderLayout.Scaling;

        // TODO: 別ウィンドウとして表示する場合
        //var pyxPoint = this.SKElement.PointToScreen(new(scaling.ToRenderImageScaling(physicalX), 0));
        //(double lgcPointX, double lgcPointY) = scaling.ToRenderImageScaling(pyxPoint.X, pyxPoint.Y);

        //double halfWidth = seekBar.Width * .5d;

        //double x = double.IsNaN(halfWidth) ? lgcPointX : (lgcPointX - halfWidth);

        // seekBar.Show((int)x, (int)lgcPointY, layout.ScreenHeight);

        // TODO: ウィンドウ内のコントロールの一部として表示する場合
        var scorePoint = scaling.ToRenderImageScaling(physicalX);

        double xOffset = Math.Round(seekBar.Width * .5d);
        Canvas.SetLeft(seekBar, scorePoint - xOffset);
        Canvas.SetTop(seekBar, 0);
        seekBar.Height = layout.ScreenHeight;
        seekBar.IsVisible = true;
    }

    /// <summary>
    /// シークバーを隠す
    /// </summary>
    /// <param name="seekBar">シークバー要素</param>
    // static void HideSeekBar(IsolatedSeekBar seekBar)
    static void HideSeekBar(Control seekBar)
        // => seekBar.Hide();
        => seekBar.IsVisible = false;

    /// <summary>編集位置を示すシークバーの画面要素を取得する</summary>
    //private IsolatedSeekBar GetSelectionSeekBar()
    //    => this.PART_SelectionTime;
    private Control GetSelectionSeekBar()
        => this.PART_SeekBar;

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
                    this.RedrawAll();
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
        var size = this.SKElement.Bounds;

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
        var size = this.SKElement;
        if (size.Width <= 0 || size.Height <= 0)
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
            var timingEditingTarget = (this._editingInfo as TimingEditingInfo)?.Target;

            var trackRenderInfo = new TrackRenderInfo()
            {
                Track = this.Track!,
                RenderRange = rangeInfo,
                ScreenLayout = renderLayout,
            };
            this._trackRenderInfo = trackRenderInfo;

            this._renderCommon.RangeScoreRenderInfo = this._rangeInfo = new RangeScoreRenderInfo
            {
                Score = trackScore.Score.GetRangeInfo(beginTime, endTime),
                Timings = [],
                NoteLines = ScoreDrawingUtil.GetVerticalLines(trackScore.Score, beginTime, endTime, this.Quantize),
                RulerLines = ScoreDrawingUtil.GetVerticalLines(trackScore.Score, beginTime, endTime, LineType.Note8th),
            };
        }

        //if (redraw)
        //    this.Redraw();
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
            VScrollPosition = this.GetVerticalScrollPosition(),
        };
    }

    /// <summary>
    /// 画面を再描画する
    /// </summary>
    [Obsolete]
    private void Redraw()
    {
        using (Dispatcher.UIThread.DisableProcessing())
        {
            this.SKElement.InvalidateVisual();
            this.UpdateScreenContent();
            this.KeysLayer.OnScroll(this.GetVerticalScrollPosition());

            this.RulerLayer.InvalidateVisual();
            this.BackgroundLayer.InvalidateVisual();
            this.WaveformLayer.InvalidateVisual();
        }
    }

    private void UpdateScreenContent()
    {
        var elParts = this.PART_Notes;
        var ri = this._renderCommon;
        if (ri == null)
            return;

        var rangeScoreInfo = ri.RangeScoreRenderInfo;
        if (rangeScoreInfo == null)
            return;

        var rangeInfo = ri.RenderRange;
        var renderLayout = ri.ScreenLayout;

        // 描画開始・終了位置
        int beginTime = rangeInfo.BeginTime;
        int endTime = rangeInfo.EndTime;

        // 描画領域
        int height = renderLayout.ScoreImage.Height;
        int keyHeight = renderLayout.PhysicalKeyHeight;

        // スコアの描画
        var notes = rangeScoreInfo.Score.Notes;

        var children = elParts.Children;

        var dict = children.OfType<EditorNote>().ToDictionary(e => e.Note);

        // 次の範囲に含まれないノートを画面上から削除
        children.RemoveAll(dict.Values.Where(e => !notes.Contains(e.Note)));

        int offsetY = -this.GetVerticalScrollPosition();

        int phraseZIndex = 1;
        int noteZIndex = 2;
        int timingZIndex = 3;

        foreach (var score in notes)
        {
            int x = renderLayout.GetRenderPosXFromTime(score.BeginTime - beginTime);
            int y = height - (score.Pitch * keyHeight);
            int w = renderLayout.GetRenderPosXFromTime(score.EndTime - score.BeginTime);
            int h = keyHeight;

            EditorNote note;
            bool isExists = dict.TryGetValue(score, out note!);
            if (!isExists)
                note = new EditorNote(score)
                {
                    ZIndex = noteZIndex,
                };

            Canvas.SetLeft(note, x);
            Canvas.SetTop(note, offsetY + y);
            note.Width = w + 1;
            note.Height = h;
            note.Lyrics = score.Lyrics;
            note.HasBreath = score.IsBreath;

            if (!isExists)
                children.Add(note);
        }

        // 次の範囲に含まれない範囲描画を画面上から削除

        var track = ri.Track;
        if (track != null)
        {
            var phrases = track.Phrases.WithinRange(beginTime, endTime).ToList();

            var rangeRects = children.OfType<PhraseRect>().ToDictionary(e => e.Phrase);
            children.RemoveAll(rangeRects.Values.Where(e => !phrases.Contains(e.Phrase)));

            foreach (var phrase in phrases)
            {
                int x = renderLayout.GetRenderPosXFromTime(phrase.BeginTime - beginTime);
                int y = 0;
                int w = renderLayout.GetRenderPosXFromTime(phrase.EndTime - phrase.BeginTime);
                int h = renderLayout.ScoreArea.Height;

                PhraseRect phraseRect;
                bool isExists = rangeRects.TryGetValue(phrase, out phraseRect!);
                if (!isExists)
                    phraseRect = new(phrase)
                    {
                        ZIndex = phraseZIndex,
                        IsHitTestVisible = false
                    };

                Canvas.SetLeft(phraseRect, x);
                Canvas.SetTop(phraseRect, y);
                phraseRect.Width = w;
                phraseRect.Height = h;

                if (!isExists)
                    children.Add(phraseRect);

                phraseRect.UpdateBackground();
            }
        }

        this._timingEdit.OnParentEditorLayoutUpdated(ri);
    }

    private const int KeyCount = 88;

    private static IBrush ToBrush(SKPaint paint)
    {
        var color = paint.Color;
        return new SolidColorBrush(Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue));
    }

    public void Ruler_Rendering(object? _, DrawingContext context)
    {
        var renderTarget = this.BackgroundLayer;
        var ri = this._renderCommon;
        if (ri == null)
            return;

        context.PushRenderOptions(new RenderOptions
        {
            EdgeMode = EdgeMode.Aliased,
            TextRenderingMode = TextRenderingMode.Alias,
        });

        var _renderInfo = ri.ScreenLayout;

        // 描画領域
        (int renderWidth, int renderHeight) = _renderInfo.RulerArea.Size;

        var rangeScoreInfo = ri.RangeScoreRenderInfo;
        var renderLayout = ri.ScreenLayout;
        var renderRange = ri.RenderRange;

        var pen = new Pen(Brushes.White, 1);
        context.FillRectangle(Brushes.Black, new Rect(0, 0, renderWidth, renderHeight));

        if (rangeScoreInfo != null)
        {
            // 描画開始・終了位置
            int beginTime = renderRange.BeginTime;

            // 小節、4分音符、8分音符時の描画位置
            float measureLineY = 0.0f;
            float beat4thLineY = renderHeight * 0.4f;
            float beat8thLineY = renderHeight * 0.7f;

            foreach (var rulerLine in rangeScoreInfo.RulerLines)
            {
                float scaledX = renderLayout.GetRenderPosXFromTime((int)rulerLine.Time - beginTime);

                var linePosY = rulerLine.LineType switch
                {
                    LineType.Measure => measureLineY,
                    LineType.Whole or LineType.Note2th or LineType.Note4th => beat4thLineY,
                    _ => beat8thLineY,
                };

                context.DrawLine(pen, new Point(scaledX, linePosY), new Point(scaledX, renderHeight));
            }
        }
    }

    public void Background_Rendering(object? _, DrawingContext context)
    {
        var renderTarget = this.BackgroundLayer;
        var ri = this._renderCommon;
        if (ri == null)
            return;

        var _renderInfo = ri.ScreenLayout;
        var rangeInfo = ri.RenderRange;
        var scaling = _renderInfo.Scaling;

        context.PushRenderOptions(new RenderOptions
        {
            EdgeMode = EdgeMode.Aliased,
            TextRenderingMode = TextRenderingMode.Alias,
        });

        // 描画領域
        (int renderWidth, int renderHeight) = _renderInfo.ScoreImage.Size;
        // (int width, int height) = _renderInfo.ScoreImage.Size;

        var renderLayout = ri.ScreenLayout;

        int renderKeyHeight = renderLayout.PhysicalKeyHeight;
        int width = renderLayout.ScoreArea.Width;
        int height = renderKeyHeight * KeyCount;

        var colorInfo = ri.ColorInfo;
        var whiteKeyBrush = ToBrush(colorInfo.ScoreWhiteKeyPaint);
        var whiteGridPen = ToBrush(colorInfo.ScoreWhiteKeyGridPaint);
        var blackKeyBrush = ToBrush(colorInfo.ScoreBlackKeyPaint);

        var whiteKeyPen = new Pen(whiteGridPen, 1);

        int yOffset = this.GetVerticalScrollPosition();

        int scoreRenderHeight = renderLayout.ScoreArea.Height;

        // 描画できるキー数
        int renderKeyCount = (int)Math.Ceiling((double)scoreRenderHeight / renderKeyHeight);

        int scoreHeight = renderKeyHeight * KeyCount;

        // 描画を始めるキーのインデックスを計算する
        // スコア領域が描画領域より小さければA0から、そうでなければ描画領域の下端から描画する
        int baseKeyIdx = 0;
        if (scoreRenderHeight < scoreHeight)
            baseKeyIdx = (scoreHeight - scoreRenderHeight - yOffset) / renderKeyHeight;

        int bottomMarign = scoreRenderHeight - scoreHeight;
        if (bottomMarign > 0)
            // 描画領域がスコア領域より大きい場合は、描画されない部分を塗りつぶす
            context.FillRectangle(Brushes.White, new(0, (scoreRenderHeight - bottomMarign), renderWidth, bottomMarign));

        // ストライプの描画
        for (int keyIdx = baseKeyIdx, maxKeyIdx = baseKeyIdx + renderKeyCount; keyIdx < maxKeyIdx; ++keyIdx)
        {
            int y = height - ((keyIdx + 1) * renderKeyHeight) - yOffset;
            var rect = new Rect(0, y, width, renderKeyHeight);

            int keyCode = keyIdx % 12;

            if (keyCode is 0 or 2 or 5 or 7 or 9)
            {
                // 白鍵のみ描画
                // C, D, F, G, A
                context.FillRectangle(whiteKeyBrush, rect);
            }
            else if (keyCode is 4 or 11)
            {
                // 白鍵と隣接する白鍵の境界
                // E, A
                context.FillRectangle(whiteKeyBrush, rect);
                context.DrawLine(whiteKeyPen, new Point(0, y), new Point(width, y));
            }
            else
            {
                // 黒鍵のみ描画
                context.FillRectangle(blackKeyBrush, rect);
            }
        }

        // 小節／拍の罫線を描画
        var rangeScoreInfo = ri.RangeScoreRenderInfo;
        if (rangeScoreInfo != null)
        {
            int beginTime = ri.RenderRange.BeginTime;

            int penSize = 1;
            var notePen = new Pen(Brushes.DarkGray, penSize);
            var measurePen = new Pen(Brushes.Black, penSize);
            var otherNotePen = new Pen(Brushes.LightGray, penSize);

            // 罫線の描画
            foreach (var noteLine in rangeScoreInfo.NoteLines)
            {
                float scaledX = ri.ScreenLayout.GetRenderPosXFromTime((int)noteLine.Time - beginTime);

                var pen = noteLine.LineType switch
                {
                    LineType.Measure => measurePen,
                    LineType.Whole => notePen,
                    LineType.Note2th => notePen,
                    LineType.Note4th => notePen,
                    _ => otherNotePen,
                };

                context.DrawLine(pen, new Point(scaledX, 0), new Point(scaledX, scoreRenderHeight));
            }
        }
    }

    public void Waveform_Rendering(object? _, DrawingContext context)
    {
        var ri = this._renderCommon;
        if (ri == null)
            return;

        var rangeScoreInfo = ri.RangeScoreRenderInfo;
        if (rangeScoreInfo == null)
            return;

        var rangeInfo = ri.RenderRange;
        var renderLayout = ri.ScreenLayout;

        if (ri.Track is not NeutrinoV2Track track)
            return;

        // 描画領域
        (int width, int height) = renderLayout.ScoreImage.Size;
        int keyHeight = renderLayout.PhysicalKeyHeight;

        // 描画開始・終了位置
        int beginTime = rangeInfo.BeginTime;
        int endTime = rangeInfo.EndTime;

        // フレームの描画範囲
        int beginFrameIdx = NeutrinoUtil.MsToFrameIndex(beginTime);
        int endFrameIdx = beginFrameIdx + rangeInfo.FramesCount;

        // 描画対象のフレーズ情報
        var targetPhrases = track.Phrases
            .Where(p => beginTime <= p.EndTime && p.BeginTime <= endTime);

        var pitches = targetPhrases
            .Where(p => p.F0 is not null)
            .SelectMany(p => PhraseUtils.EnumerateGreaterThanForLowerRanges(p, p.F0!, 0, 1, NeutrinoUtil.MsToFrameIndex(p.BeginTime)))
            .OrderBy(i => i.PhraseBeginFrameIdx + i.BeginIndex)
            .GroupingAdjacentRange(i => i.TotalBeginIndex, i => i.TotalEndIndex);

        float pitchOffset = (float)keyHeight / 2;

        int offsetMs = NeutrinoUtil.FrameIndexToMs(beginFrameIdx) - beginTime;

        foreach (var pitchGroup in pitches)
        {
            int count = pitchGroup.Last().TotalEndIndex - pitchGroup.First().TotalBeginIndex + 1;
            var origPoints = new Point[count];
            var editedPoints = new Point[count];
            int pointsIdx = 0;

            foreach (var pitch in pitchGroup)
            {
                var phrase = pitch.Phrase;
                float[] f0 = phrase.F0!;
                float[] editedF0 = phrase.GetEditingF0()!;

                // 描画開始／終了インデックス
                (int beginIdx, int endIdx) = DrawUtil.GetDrawRange(
                    pitch.PhraseBeginFrameIdx + pitch.BeginIndex, pitch.EndIndex - pitch.BeginIndex + 1,
                    beginFrameIdx, endFrameIdx, 0);

                if (beginIdx >= endIdx)
                {
                    Debug.WriteLine($"Beg: {beginIdx}, End: {endIdx}");
                    continue;
                }

                int f = beginIdx - pitch.PhraseBeginFrameIdx;

                for (int idx = 0, length = endIdx - beginIdx; idx < length; ++idx)
                {
                    int frameIdx = idx + f;
                    int x = renderLayout.GetRenderPosXFromTime(offsetMs + NeutrinoUtil.FrameIndexToMs(frameIdx + pitch.PhraseBeginFrameIdx) - beginTime);

                    origPoints[pointsIdx] = new Point(x,
                        height - pitchOffset - ((float)AudioDataConverter.FrequencyToPitch12(f0[frameIdx]) * keyHeight));

                    editedPoints[pointsIdx] = new Point(x,
                        height - pitchOffset - ((float)AudioDataConverter.FrequencyToPitch12(editedF0[frameIdx]) * keyHeight));

                    ++pointsIdx;
                }
            }

            if (pointsIdx > 0)
            {
                var range = 0..pointsIdx;
                // context.DrawGlyphRun()

                var waveformPen = new Pen(Brushes.Red, 1.5);

                var path = new PathGeometry()
                {
                    Figures = [
                        new PathFigure() {
                            IsClosed = false,
                            StartPoint = editedPoints[0],
                            Segments = [ new PolyLineSegment() { Points = new ArraySegment<Point>(editedPoints, 1, pointsIdx - 1) } ]
                        }
                    ]
                };

                context.DrawGeometry(null, waveformPen, path);


                //g.DrawPoints(SKPointMode.Polygon, origPoints[range], new SKPaint { Color = SKColors.OrangeRed.WithAlpha(150), StrokeWidth = 1.2f, IsAntialias = true });
                //g.DrawPoints(SKPointMode.Polygon, editedPoints[range], new SKPaint { Color = SKColors.Red, StrokeWidth = 1.2f, IsAntialias = true });
            }
        }
    }

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
        this.RedrawAll();
    }

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

    /// <summary>縦スクロール位置を取得する</summary>
    private int GetVerticalScrollPosition()
        => (int)this.vScrollBar1.Value;

    /// <summary>描画開始位置を変更する</summary>
    /// <param name="position">縦位置</param>
    private void SetVerticalScrollPosition(int position)
    {
        if ((int)this.vScrollBar1.Value != position)
        {
            this.vScrollBar1.Value = position;
            this.OnVScrolled(position);
        }
    }

    private void OnVScrolled(int position)
    {
        this.KeysLayer.OnScroll(position);
    }

    /// <summary>描画開始位置を変更する</summary>
    /// <param name="offset">縦位置オフセット値</param>
    private void SetVerticalPositionOffset(double offset)
        => this.SetVerticalScrollPosition((int)(this.GetVerticalScrollPosition() + offset));

    /// <summary>
    /// 縦スクロール時
    /// </summary>
    /// <param name="sender">イベント発火元</param>
    /// <param name="e">イベント情報</param>
    private void OnVScroll(object? sender, ScrollEventArgs e)
    {
        int pos = this.GetVerticalScrollPosition();
        this._renderCommon?.OnVerticalScrollChanged(pos);
        this.OnVScrolled(pos);
        this.RedrawAll();
    }

    /// <summary>
    /// 横スクロール時
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnHScroll(object? sender, ScrollEventArgs e)
    {
        int time = (int)e.NewValue;
        if (this.GetRenderBeginTimeMs() == time)
        {
            return;
        }

        // 自動スクロールを解除
        this.CancelAutoScroll();

        this.OnRenderBeginMsChanged(time);
        this.UpdateRenderContent();
        this.RelocateSeekBars();
        this.RedrawAll();
    }

    /// <summary>
    /// レンダラを初期化する
    /// </summary>
    /// <param name="track"></param>
    private void InitializeRenderer(INeutrinoTrack? track)
    {
        this._partsLayout = new();
    }

    private SeekBarMode GetSeekBarMode()
    {
        if (!this.IsPlayMode || this._mouseMode == MouseControlMode.Seek)
            return SeekBarMode.Edit;
        else
            return SeekBarMode.Play;
    }

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
        this.RedrawAll();
        this.UpdateScrollLayout();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!this.IsFocused)
        {
            this.Focus();
            // Keyboard.Focus(this);
        }
    }

    /// <summary>
    /// マウス押下時の処理
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnMouseDown(object sender, PointerPressedEventArgs e)
    {
        Debug.WriteLine("Mouse down.");

        var renderLayout = this._renderLayout;
        var scaling = renderLayout.Scaling;
        var element = (Control)sender;

        var cursor = this._cursor;
        var current = e.GetCurrentPoint(this);

        var pressedKey = this._pressedKey;

        if (current.Properties.IsLeftButtonPressed)
        {
            Debug.WriteLine("Mouse captured.");
            e.Pointer.Capture(this.SKElement);

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
                        var editorMousePos = e.GetPosition(this.PART_Notes);

                        var timingEditor = this._timingEdit;

                        var timing = this._timingEdit.HitTest(editorMousePos);
                        if (timing != null)
                            {
                            timingEditor.Select(timing);

                            var timings = this.Track!.Timings;
                            int idx = timings.IndexOf(timing);

                            this.ChangeMouseMode(MouseControlMode.EditTiming, new TimingEditingInfo(timing, timing.EditedTimeMs)
                                {
                                LowerTime = idx <= 1 ? 0 : timings[idx - 1].EditedTimeMs,
                                UpperTime = (idx == -1 || (timings.Count <= (idx + 1))) ? null : timings[idx + 1].EditedTimeMs,
                                });

                                e.Handled = true;
                                return;
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

                        if (pressedKey.modifiers == KeyModifiers.Shift)
                        {
                            // Shiftキーが押されている場合は範囲選択モードにする
                            if (renderLayout.EditArea.IsContains(mousePosition))
                            {
                                cursor = new Cursor(StandardCursorType.SizeWestEast);
                                var editingInfo = new RangeSelectingInfo(
                                    renderLayout.ScoreArea.IsContainsY(mousePos.Y),
                                    adjustedTime, adjustedTime);
                                this.ChangeMouseMode(MouseControlMode.RangeSelect, editingInfo);
                                this._rangeSelection = editingInfo;
                            }

                            this.UpdateRenderContent();
                            this.UpdateNoteElements();
                            e.Handled = true;
                        }
                        else if (pressedKey.modifiers == KeyModifiers.Control)
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
                                        this.RedrawWaveform();
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
                                        this.UpdateDynamicsArea();
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

                                this.UpdateRenderContent();
                                this.RedrawWaveform();
                            }
                            else if (dynamicsArea != null && dynamicsArea.IsContains(mousePosition))
                            {
                                double coe = this.GetDynamicsCoeFromMousePosition(renderLayout, mousePosition);
                                double frequency = DynamicsCoeToFrequency(this.Track!, coe);

                                this.ChangeMouseMode(MouseControlMode.EditDynamics, new DynamicsEditingInfo(adjustedTime, frequency));

                                this.EditDynamics(adjustedTime, EnumerableUtil.ToEnumerable(frequency));

                                this.UpdateRenderContent();
                                this.UpdateDynamicsArea();
                            }

                            e.Handled = true;
                        }
                    }
                }
            }
        }

        this.SetCursor(cursor);
    }

    /// <summary>
    /// マウスホイールイベント発火時
    /// </summary>
    /// <param name="sender">イベント発火元</param>
    /// <param name="e">イベント情報</param>
    private void OnScoreMouseWheel(object sender, PointerWheelEventArgs e)
    {
        var modifiers = e.KeyModifiers;

        if (modifiers == KeyModifiers.Control)
        {
            // Ctrl+スクロール時
            // 横倍率を変更

            double zoomLevel = this.ScaleX;

            double delta = e.Delta.Y;
            if (delta > 0)
            {
                // 横伸長率リストから次に大きい拡大率(拡大方向)を取得して適用する
                this.ChangeHorizontalScale(
                    HorizontalZoomLevels.GetNextUpper(zoomLevel), e);
            }
            else if (delta < 0)
            {
                // 横伸長率リストから次に小さい拡大率(縮小方向)を取得して適用する
                this.ChangeHorizontalScale(
                    HorizontalZoomLevels.GetNextLower(zoomLevel), e);
            }
        }
        else if (modifiers == (KeyModifiers.Control | KeyModifiers.Shift))
        {
            // Ctrl+Shift+スクロール時
            // 縦倍率を変更
            int currentHeight = this.KeyHeight;
            double delta = e.Delta.Y;
            if (delta > 0)
            {
                // 横伸長率リストから次に大きい拡大率(拡大方向)を取得して適用する
                this.ChangeKeyHeightSize(
                    KeySizes.GetNextUpper(currentHeight), e);
            }
            else if (delta < 0)
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
            bool isHorizontal = modifiers == KeyModifiers.Shift;
            var targetScrollBar = isHorizontal
                ? this.hScrollBar1
                : this.vScrollBar1;

            if (isHorizontal)
                // 横スクロールの場合は自動スクロールを無効化
                this.CancelAutoScroll();

            int change = (int)targetScrollBar.LargeChange;
            double delta = e.Delta.Y;
            if (delta > 0)
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
                this.RedrawAll();
                this.RelocateSeekBars();
            }
            else if (delta < 0)
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
                this.RedrawAll();
                this.RelocateSeekBars();
            }
        }
    }

    /// <summary> 
    /// 横の拡大率を変更する
    /// </summary>
    /// <param name="newZoomLevel"></param>
    private void ChangeHorizontalScale(double newZoomLevel, PointerEventArgs e)
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
    private void ChangeKeyHeightSize(int newHeight, PointerEventArgs e)
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
        this.SetVerticalScrollPosition((int)((this.GetVerticalScrollPosition() * zoom) + delta));
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

    private Point _seekMousePoint = default;


    /// <summary>
    /// マウス移動時の処理
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnMouseMove(object sender, PointerEventArgs e)
    {
        // カーソル
        var cursor = Cursor.Default;
        var current = e.GetCurrentPoint(this);
        var pressedKey = this._pressedKey;

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
            this._seekMousePoint = e.GetCurrentPoint(this.SKElement).Position;

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

                // タイミング編集中のマウス移動
                if (editingInfo is TimingEditingInfo timingEditing)
                {
                    // マウスの位置を取得、オフセット分補正する
                    var mousePosition = this.GetPhysicalMousePosition(renderLayout, e);

                    // マウスで選択中の時間を計算し、範囲内に収める
                    int adjustedTime = GetConditionTimeRoundFrame(renderLayout, beginTime, mousePosition);
                    adjustedTime = Math.Max(adjustedTime, timingEditing.LowerTime);
                    if (timingEditing.UpperTime is { } upperTime)
                        adjustedTime = Math.Min(adjustedTime, upperTime);

                    // レイアウト、編集中のタイミング情報を更新
                    int adjustedX = this.GetScoreLocationX(renderLayout, adjustedTime);
                    timingEditing.Target.EditingTimeMs = adjustedTime;
                    timingEditing.CurrentTimeMs = adjustedTime;

                    // タイミング編集関連の要素を再配置
                    this.UpdateTimingElements();
                    e.Handled = true;
                }
            }
        }
        else if (this.EditMode == EditMode.AudioFeatures)
        {
            var renderLayout = this._renderLayout;
            var mousePosition = this.GetPhysicalMousePosition(renderLayout, e);

            if (renderLayout.EditArea.IsContains(mousePosition))
                cursor = new Cursor(StandardCursorType.Arrow);

            if (current.Properties.IsLeftButtonPressed)
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

                    this.UpdateRenderContent();
                    this.RedrawWaveform();
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

                    this.UpdateRenderContent();
                    this.UpdateDynamicsArea();
                }
                else if (editingInfo is RangeSelectingInfo rangeSelection)
                {
                    // 範囲選択モード
                    cursor = new Cursor(StandardCursorType.SizeWestEast);
                    rangeSelection.UpdateEndTime(frameAdjustedTime);
                    this.UpdateRenderContent();
                    this.UpdateSelectionArea();
                }
                else if (editingInfo is PitchSeekingInfo pitchSeeking)
                {
                    // ピッチ編集中
                    double currentPitch = this.GetPitch12FromMousePosition(renderLayout, mousePosition);

                    int beginTime2 = pitchSeeking.EndTime < pitchSeeking.EndTime ? pitchSeeking.EndTime : pitchSeeking.BeginTime;
                    int length = NeutrinoUtil.MsToFrameIndex(Math.Abs(pitchSeeking.EndTime - pitchSeeking.BeginTime) + 1);

                    this.AddPitch12(beginTime2, Enumerable.Repeat(currentPitch - pitchSeeking.Pitch, length));

                    pitchSeeking.SetPitch(currentPitch);

                    this.UpdateRenderContent();
                    this.RedrawWaveform();
                }
                else if (editingInfo is DynamicsSeekingInfo dynamicsSeeking)
                {
                    // ダイナミクス一括編集中
                    double currentCoe = this.GetDynamicsCoeFromMousePosition(renderLayout, mousePosition);

                    int beginTime2 = dynamicsSeeking.EndTime < dynamicsSeeking.EndTime ? dynamicsSeeking.EndTime : dynamicsSeeking.BeginTime;
                    int length = NeutrinoUtil.MsToFrameIndex(Math.Abs(dynamicsSeeking.EndTime - dynamicsSeeking.BeginTime) + 1);

                    this.AddDynamicsCoe(beginTime2, Enumerable.Repeat(currentCoe - dynamicsSeeking.Coe, length));

                    dynamicsSeeking.SetCoe(currentCoe);

                    this.UpdateRenderContent();
                    this.UpdateDynamicsArea();
                }

                e.Handled = true;
            }
            else if (current.Properties.IsLeftButtonPressed)
            {
                if (this._mouseMode == MouseControlMode.None)
                {
                    var rangeSelection = this._rangeSelection;
                    if (rangeSelection != null)
                    {
                        if (pressedKey.modifiers == KeyModifiers.Control)
                        {
                            // Ctrlキーが押されている場合は一括編集モードにする
                            if (renderLayout.ScoreArea.IsContainsY(mousePosition.Y))
                            {
                                cursor = new Cursor(StandardCursorType.SizeNorthSouth);
                            }
                            else if (renderLayout.HasDynamicsArea && renderLayout.DynamicsArea.IsContainsY(mousePosition.Y))
                            {
                                cursor = new Cursor(StandardCursorType.SizeNorthSouth);
                            }
                        }
                    }
                }
            }
        }
        else
        {
            // this.RelocatePuttableNoteRectangle(e);

            // var scaling = this._renderCommon.ScreenLayout.Scaling;
            var renderLayout = this._renderLayout;
            var mousePosition = this.GetPhysicalMousePosition(renderLayout, e);

            if (IsWithinEditArea(this._renderLayout, mousePosition))
            {
                if (this._isTimingEditable)
                {
                    var editorMousePos = e.GetPosition(this.PART_Notes);

                    var timingEditor = this._timingEdit;
                    var timing = this._timingEdit.HitTest(editorMousePos);
                    if (timing != null)
                        {
                        // TODO: マウスカーソルの処理を見直す
                        cursor = new Cursor(StandardCursorType.SizeWestEast);
                        timingEditor.Select(timing);
                        }
                    else
                    {
                        timingEditor.UnselectAll();
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
    private void OnMouseUp(object sender, PointerReleasedEventArgs e)
    {
        //Debug.WriteLine("Mouse up.");

        var current = e.GetCurrentPoint(this);

        if (!current.Properties.IsLeftButtonPressed)
        {
            // this.SKElement.ReleaseMouseCapture();
            e.Pointer.Capture(null);

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
                    this._timingEdit.UnselectAll();
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

    private (KeyModifiers modifiers, Key key) _pressedKey = default;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        this._pressedKey = (e.KeyModifiers, e.Key);
        Debug.WriteLine(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        this._pressedKey = default;

        var editInfo = this._editingInfo;
        if (editInfo is TimingEditingInfo timingInfo)
        {
            this.CancelTimingEdit();
            this._timingEdit.UnselectAll();
        }

        if (this.EditMode == EditMode.AudioFeatures)
        {
            if (this._rangeSelection is { } select)
            {
                if (select.IsScoreArea)
                {
                    // ピッチ編集エリアを選択中

                    if (e.KeyModifiers == KeyModifiers.None)
                    {
                        (int beginTime, int endTime) = select.GetOrdererRange();
                        int lenggth = NeutrinoUtil.MsToFrameIndex(endTime - beginTime + 1);

                        if (e.Key is (Key.Up or Key.Down))
                        {
                            // 上下キーのみの場合は1音分上下させる
                            this.AddPitch12(beginTime, Enumerable.Repeat(e.Key == Key.Up ? 1.0d : -1.0d, lenggth));

                            this.Resync();
                            this.UpdateRenderContent();
                            this.RedrawWaveform();
                            e.Handled = true;
                        }
                    }
                    else if (e.KeyModifiers == KeyModifiers.Shift)
                    {
                        (int beginTime, int endTime) = select.GetOrdererRange();
                        int lenggth = NeutrinoUtil.MsToFrameIndex(endTime - beginTime + 1);

                        if (e.Key is (Key.Up or Key.Down))
                        {
                            // Shift+上下キーの場合は1オクターブ分上下させる

                            this.AddPitch12(beginTime, Enumerable.Repeat(e.Key == Key.Up ? 12.0d : -12.0d, lenggth));

                            this.Resync();
                            this.UpdateRenderContent();
                            this.RedrawWaveform();
                            e.Handled = true;
                        }
                    }
                }
            }
        }
    }

    private Cursor _cursor = Cursor.Default;
    private RangeSelectingInfo? _rangeSelection;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetCursor(Cursor cursor)
    {
        if (this._cursor != cursor)
            this.Cursor = this._cursor = cursor;
    }

    private void RelocateSeekBarForSeeking(PointerEventArgs e)
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

    private void RelocateNoteRectangle(PointerEventArgs e)
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
        int scrollPosition = this.GetVerticalScrollPosition();

        return (int)(renderLayout.ScoreArea.Height - ((scrollPosition + renderLayout.ScoreArea.RelativeY(mousePosition.Y)) / renderLayout.PhysicalKeyHeight));
    }

    private void MoveBorder(EditorRenderLayout renderLayout, int keyIndex, int time, int duration)
    {
        var scaling = renderLayout.Scaling;

        int keyIndexForTop = RenderConfig.KeyCount - keyIndex - 1;
        double posY = scaling.ToScaled((keyIndexForTop * renderLayout.PhysicalKeyHeight) - this.GetVerticalScrollPosition() + renderLayout.ScoreArea.Y);

        double posX = renderLayout.GetRenderPosXFromTime(time);
        double width = renderLayout.GetRenderPosXFromTime(duration);

        var element = this.PART_Rectangle;
        var beforeMargin = element.Margin;
        element.Margin = new(posX, posY, beforeMargin.Right, beforeMargin.Bottom);
        element.Width = width + 1;
        element.Height = (int)scaling.ToScaled(renderLayout.PhysicalKeyHeight) + 1;

        element.IsVisible = true;
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
        int scrollPosition = this.GetVerticalScrollPosition();
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
            if (track != null)
            {
                var target = editing.Target;
                target.DetermineEdit();
                track?.ChangeTiming(target);
            }
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
            editing.Target.CancelEdit();

            // タイミング編集関連の要素を再配置
            this.UpdateTimingElements();
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
            this.RedrawWaveform();
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
        this.UpdateDynamicsArea();
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
            this.UpdateDynamicsArea();
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
            this.RedrawWaveform();
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
            this.UpdateDynamicsArea();
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
            this.RedrawAll();
        }
        else if ((width - AutoScrollInnerWidth) < posX)
        {
            int degree = (int)((posX <= (width + AutoScrollOuterWidth)) ? (posX - width + AutoScrollInnerWidth) : (AutoScrollInnerWidth + AutoScrollOuterWidth));
            this.SetRenderBeginMsOffset((int)(degree / scaleX));
            // TODO: 再レンダリングの処理を見直す
            this.UpdateRenderContent();
            this.RedrawAll();
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

    private static LayoutPoint ToUnscaled(RenderScaleInfo scaling, ref Point point)
        => new((int)scaling.ToUnscaled(point.X), (int)scaling.ToUnscaled(point.Y));

    private LayoutPoint GetPhysicalMousePosition(EditorRenderLayout renderLayout)
    {
        var position = this._seekMousePoint;
        return ToUnscaled(renderLayout.Scaling, ref position);
    }

    private LayoutPoint GetPhysicalMousePosition(EditorRenderLayout renderLayout, PointerEventArgs e)
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
    public void CancelAutoScroll()
    {
        if (this.IsAutoScroll)
        {
            this.IsAutoScroll = false;
        }
    }

    /// <summary>内部の編集シークバーの選択位置</summary>
    public TimeSpan _tempSelectionTime;

    private void UpdateTempSelectionTime(TimeSpan selectionTime)
    {
        this._tempSelectionTime = selectionTime;
        this.RelocateSelectionSeekBar(selectionTime);
    }

    private void OnTrackFeatureChanged(object? sender, EventArgs e) => Dispatcher.UIThread.InvokeAsync(() =>
    {
        // 再描画
        this.UpdateRenderContent();
        this.RedrawAll();
    }
    , DispatcherPriority.Render);

    private void OnTrackTimingEstimated(object? sender, EventArgs e) => Dispatcher.UIThread.InvokeAsync(() =>
    {
        // 再描画
        this.LoadTiming();
    }
    , DispatcherPriority.Normal);

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


    private void RedrawRuler()
    {
        this.RulerLayer.InvalidateVisual();
    }

    private void RedrawBackground()
    {
        this.BackgroundLayer.InvalidateVisual();
    }

    private void RedrawWaveform()
    {
        this.WaveformLayer.InvalidateVisual();
    }

    private void RedrawAll()
    {
        // 要素の再配置
        this.UpdateTimingElements();
        // TODO: 現時点でUpdateTimingElementsの中ですべての要素の再配置をしている。今後処理分割したらコメントを外す
        // this.UpdateNoteElements();
        // this.UpdatePharseElements();

        // 
        this.RedrawRuler();
        this.RedrawBackground();
        this.RedrawWaveform();
    }

    private void UpdateTimingElements()
    {
        this._timingEdit.RelocateRenderElements();

        // HACK: 処理を外せるようにする
        this.UpdateScreenContent();
    }

    private void UpdateNoteElements()
    {
        this.UpdateScreenContent();
    }

    private void UpdatePharseElements()
    {
        this.UpdateScreenContent();
    }

    private void UpdateSelectionArea()
    {
        // TODO: 実装
    }
    private void UpdateDynamicsArea()
    {
        // TODO: 実装
    }
}
