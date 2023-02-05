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
using System.Windows.Shapes;
using Quark.Models.MusicXML;
using Quark.Models.Scores;
using Quark.Projects.Tracks;
using Quark.Utils;
using SkiaSharp;

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
    private const double MaxHScrollHeight = 1000;

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
    private float _frameWidth = 0.8f;
    private ScalingConverter _scaling;
    private RenderInfo _renderInfo;

    private int _rulerHeight = 24;

    private SKPaint lyricsTypography = new(new SKFont(SKTypeface.FromFamilyName("MS UI Gothic"), 12));

    public PlotEditor()
    {
        this.InitializeComponent();

        vScrollBar1.Minimum = 0;
        vScrollBar1.Maximum = MaxVScrollHeight;
        vScrollBar1.LargeChange = 1;
        vScrollBar1.ViewportSize = MaxVScrollHeight / 10;

        hScrollBar1.Minimum = 0;
        hScrollBar1.Maximum = MaxHScrollHeight;
        hScrollBar1.LargeChange = 1;
        hScrollBar1.ViewportSize = MaxHScrollHeight / 10;

        this.Loaded += this.OnContentLoaded;

        this._scaling = new ScalingConverter(VisualTreeHelper.GetDpi(this).DpiScaleX);
    }

    private void OnContentLoaded(object sender, RoutedEventArgs e)
    {
        this.Loaded -= this.OnContentLoaded;

        var window = Window.GetWindow(this);
        window.DpiChanged += this.OnDpiChnaged;
    }

    /// <summary>
    /// 画面のDPI変更時
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnDpiChnaged(object sender, DpiChangedEventArgs e)
    {
        this._scaling = new ScalingConverter(e.NewDpi.DpiScaleX);
        this._renderInfo = new RenderInfo(this._scaling, this.GetRenderWidth(), this._frameWidth, 1.0f);
        this.Redraw();
    }

    internal NeutrinoV1Track? Track
    {
        get => (NeutrinoV1Track)this.GetValue(TrackProperty);
        set => this.SetValue(TrackProperty, value);
    }

    public static readonly DependencyProperty TrackProperty =
        DependencyProperty.Register(nameof(Track), typeof(NeutrinoV1Track), typeof(PlotEditor), new PropertyMetadata(null, OnTrackPropertyChanged));

    private static void OnTrackPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PlotEditor editor)
        {
            if (e.NewValue is NeutrinoV1Track track)
            {
                editor.Load(track);
            }

            editor.UpdatePointer();
        }
    }

    public TimeSpan SelectionTime
    {
        get => (TimeSpan)this.GetValue(SelectionTimeProperty);
        set => this.SetValue(SelectionTimeProperty, value);
    }

    // Using a DependencyProperty as the backing store for SelectionTime.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty SelectionTimeProperty =
        DependencyProperty.Register(nameof(SelectionTime), typeof(TimeSpan), typeof(PlotEditor), new PropertyMetadata(TimeSpan.Zero, OnSelectionTimePropertyChanged));

    private static void OnSelectionTimePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((PlotEditor)d).UpdatePointer((TimeSpan)e.NewValue);

    private void UpdatePointer() => this.UpdatePointer(this.SelectionTime);

    private void UpdatePointer(TimeSpan time)
    {
        long totalFrameCount = this._framesCount;

        (int rulerHeight, var scaling) = (this._rulerHeight, this._scaling);

        int scoreHeight = KeyHeight * KeyCount;

        // 描画領域
        int renderWidth = this.GetRenderWidth();
        int width = scaling.ToRenderImageScaling(renderWidth);
        int height = KeyHeight * KeyCount;
        int renderHeight = scaling.ToDisplayScaling(height);

        int scoreYOffset = scaling.ToDisplayScaling(rulerHeight);

        int scoreRenderWidth = renderWidth;
        int scoreWidth = width;
        int scoreRenderHeight = scaling.ToDisplayScaling(scoreHeight);

        // 描画するフレーム数
        int viewFrames = (int)Math.Ceiling(((double)renderWidth / this._frameWidth));
        int framesCount = viewFrames;

        // 開始フレーム位置
        int beginFrameIdx = (int)Math.Ceiling(this.GetHorizontalScrollCore() * totalFrameCount);
        int endFrameIdx = beginFrameIdx + framesCount;

        int currentFrameIdx = (int)(time.TotalMilliseconds / 200);
        Debug.WriteLine(time);

        var lineElement = this.PART_SelectionTime;
        var renderElement = this.SKElement;
        if (beginFrameIdx <= currentFrameIdx && currentFrameIdx <= endFrameIdx)
        {
            double x = scaling.ToDisplayScaling((currentFrameIdx - beginFrameIdx) * _frameWidth);

            lineElement.X1 = x;
            lineElement.X2 = x;
            lineElement.Y1 = 0;
            lineElement.Y2 = renderElement.ActualHeight;

            lineElement.Visibility = Visibility.Visible;
        }
        else
        {
            lineElement.Visibility = Visibility.Collapsed;
        }
    }

    private (SKBitmap bmp, int width, int height) CreatePianoOctaveBmp(int width, int keyHeight, ScalingConverter scaling)
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
            SKRect DrawRect(int key)
            {
                int y = GetYPos(key);
                return new(0, y, renderWidth, renderKeyHeight + y);
            };

            // 白鍵の描画
            foreach (int key in new int[] {
                0, // C#
                2, // D#
                4, // F#
                5, // G#
                7, // A#
                9, // A#
                11, // A#
            })
            {
                g.DrawRect(DrawRect(key), whiteKeyBrush);
            }

            // 黒鍵の描画
            foreach (int key in new int[] {
                1, // C#
                3, // D#
                6, // F#
                8, // G#
                10, // A#
            })
            {
                g.DrawRect(DrawRect(key), blackKeyBrush);
            }

            // 白鍵の境界を描画
            g.DrawLine(0, GetYPos(4), renderWidth, GetYPos(4), whiteGridPen);
            g.DrawLine(0, GetYPos(11), renderWidth, GetYPos(11), whiteGridPen);
        }

        return (image, width, height);
    }

    private int GetRenderWidth() => (int)this.SKElement.CanvasSize.Width;

    private int GetRenderHeight() => (int)this.SKElement.CanvasSize.Height;

    private (int width, int height) GetCanvasSize()
    {
        var size = this.SKElement.CanvasSize;

        return ((int)size.Width, (int)size.Height);
    }

    private void UpdateRenderImage()
    {
        (int width, int height) = (this.GetRenderWidth(), this.GetRenderHeight());
        if (width > 0 && height > 0)
        {
            using (this._renderImage)
            {
                var renderInfo = this._renderInfo = new RenderInfo(this._scaling, width, this._frameWidth, 1.0f);
                this._renderImage = this.CreateRenderImage(renderInfo);
                this._rulerImage = this.CreateRulerImage(renderInfo);
            }
        }
    }

    private void Redraw()
    {
        this.UpdateRenderImage();
        this.SKElement.InvalidateVisual();
    }

    private void OnRenderSizeChanged(object sender, SizeChangedEventArgs e)
    {
        this.Redraw();
        this.UpdatePointer();
    }

    private double GetVerticalScrollCoe()
    {
        return (double)vScrollBar1.Value / MaxVScrollHeight;
    }

    private double GetHorizontalScrollCore()
    {
        return (double)hScrollBar1.Value / MaxHScrollHeight;
    }

    private SKBitmap CreateRenderImage(RenderInfo renderInfo)
    {
        (int rulerHeight, var scaling) = (this._rulerHeight, this._scaling);

        int scoreHeight = renderInfo.ScoreHeight;

        // 描画領域
        int renderWidth = renderInfo.RenderWidth;
        int width = renderInfo.ImageWidth;
        int height = renderInfo.ImageHeight;
        int renderHeight = renderInfo.RenderHeight;

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
                int viewFrames = (int)Math.Ceiling(((double)renderWidth / this._frameWidth));
                int framesCount = viewFrames + (offsetFrames * 2);
                // float renderOffset =  viewFrames * this._frameWidth;

                // 開始フレーム位置
                int beginFrameIdx = (int)Math.Ceiling(this.GetHorizontalScrollCore() * totalFrameCount);
                int endFrameIdx = beginFrameIdx + framesCount;

                int offsetTemp = 1;
                int beginFrameIdxOffsetted = beginFrameIdx - offsetTemp;
                int endFrameIdxOffsetted = endFrameIdx + offsetTemp;

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
                            scaling.ToDisplayScaling(beginIndex * _frameWidth),
                            scoreYOffset + scaling.ToDisplayScaling(height - (score.Pitch * KeyHeight)),
                            scaling.ToDisplayScaling((score.EndFrame - score.BeginFrame) * _frameWidth),
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
                        // TODO: 範囲外の描画を行わないようにする
                        int beginIndex = dynamics.Index - beginFrameIdx;
                        var points = new SKPoint[dynamics.Values.Length];

                        for (int i = 0; i < dynamics.Values.Length; ++i)
                        {
                            points[i] = new SKPoint(
                                scaling.ToDisplayScaling((i + beginIndex) * _frameWidth),
                                scoreYOffset + scaling.ToDisplayScaling(height - dynamicsOffset - (lower + diff * ((dynamics.Values[i] + 30f) / 30f))));
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
                        // TODO: 範囲外の描画を行わないようにする
                        int beginIndex = pitch.Index - beginFrameIdx;
                        var points = new SKPoint[pitch.Values.Length];

                        for (int i = 0; i < pitch.Values.Length; ++i)
                        {
                            points[i] = new SKPoint(
                                scaling.ToDisplayScaling((i + beginIndex) * _frameWidth),
                                scoreYOffset + scaling.ToDisplayScaling(height - pitchOffset - ((float)FrequencyToScale(pitch.Values[i]) * KeyHeight)));
                        }

                        g.DrawPoints(SKPointMode.Polygon, points, new SKPaint { Color = SKColors.Red, StrokeWidth = 1.5f, IsAntialias = true });

                    }
                }
            }
        }

        return image;
    }

    private SKBitmap CreateRulerImage(RenderInfo renderInfo)
    {
        var scaling = renderInfo.Scaling;

        long totalFrameCount = this._framesCount;

        int offsetFrames = 1;


        int renderWidth = renderInfo.RenderWidth;
        int rulerHeight = this._rulerHeight;
        int renderHeight = renderInfo.RenderRulerHeight;
        var result = this._currentViewScore;

        var image = new SKBitmap(renderWidth, renderHeight);

        (var partImage, int octWidth, int octHeight) = CreatePianoOctaveBmp(100, KeyHeight, scaling);

        using (var g = new SKCanvas(image))
        {

            // 描画するフレーム数
            int viewFrames = renderInfo.GetRenderFrames();
            int framesCount = viewFrames/* + (offsetFrames * 2)*/;

            // 開始フレーム位置
            int beginFrameIdx = (int)Math.Ceiling(this.GetHorizontalScrollCore() * totalFrameCount);
            int endFrameIdx = beginFrameIdx + framesCount;

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
                        int x = scaling.ToDisplayScaling(TimeToFrame(time - beginTime) * _frameWidth);

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

    private static int FrameToTime(decimal frameIdx) => (int)(frameIdx * FrameUnit);
    private static int TimeToFrame(decimal time) => (int)(time / FrameUnit);

    private void PaintGraphics(SKCanvas g)
    {
        if (DesignerProperties.GetIsInDesignMode(this))
        {
            // デザインモード時は描画処理を行わない
            return;
        }

        (int width, int height) = (this.GetRenderWidth(), this.GetRenderHeight());
        var keysBackground = this._renderImage;
        if (width <= 0 || height <= 0 || keysBackground is null)
        {
            return;
        }

        // 描画領域の更新
        int renderHeight = KeyHeight * KeyCount;

        var renderInfo = this._renderInfo;
        int rulerHeight = renderInfo.RenderRulerHeight;

        // スクロール位置から描画位置(y)を計算
        int renderY = (int)Math.Floor(this.GetVerticalScrollCoe() * (renderHeight - height - rulerHeight));
        g.DrawBitmap(keysBackground, SKRect.Create(0, rulerHeight + renderY, width, height - rulerHeight), SKRect.Create(0, rulerHeight, width, height - rulerHeight));
        g.DrawBitmap(this._rulerImage, SKRect.Create(0, 0, width, rulerHeight), SKRect.Create(0, 0, width, rulerHeight));
    }

    private void Load(NeutrinoV1Track track)
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

    private void OnPaintSurface(object sender, SkiaSharp.Views.Desktop.SKPaintSurfaceEventArgs e)
    {
        this.PaintGraphics(e.Surface.Canvas);
    }

    private static double FrequencyToScale(double freqency)
    {
        // http://signalprocess.binarized.work/2019/03/26/convert_frequency_to_cent/
        return 12 * Math.Log2(freqency / 440) + 69;
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

    private void OnVScroll(object sender, ScrollEventArgs e)
    {
        if (e.ScrollEventType != ScrollEventType.First)
        {
            this.SKElement.InvalidateVisual();
        }
    }

    private void OnHScroll(object sender, ScrollEventArgs e)
    {
        if (e.ScrollEventType != ScrollEventType.First)
        {
            this.Redraw();
            this.UpdatePointer();
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

    private void OnScoreMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var targetScrollBar = Keyboard.PrimaryDevice.Modifiers == ModifierKeys.Shift
            ? this.hScrollBar1 : this.vScrollBar1;

        if (e.Delta > 0)
        {
            targetScrollBar.Value -= targetScrollBar.LargeChange;
            this.Redraw();
        }
        else if (e.Delta < 0)
        {
            targetScrollBar.Value += targetScrollBar.LargeChange;
            this.Redraw();
        }
    }
}
