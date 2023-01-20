using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Quark.Models.MusicXML;
using Quark.Projects.Tracks;
using Quark.Utils;
using SkiaSharp;

namespace Quark.Controls;

/// <summary>
/// PlotEditor.xaml の相互作用ロジック
/// </summary>
public partial class PlotEditor : UserControl
{
    private const int KeyCount = 88;

    private const double MaxVScrollHeight = 1000;
    private const double MaxHScrollHeight = 1000;

    private SKPaint _whiteKeyPaint = new SKPaint { Color = new SKColor(255, 255, 255) };
    private SKPaint _blackKeyPaint = new SKPaint { Color = new SKColor(230, 230, 230) };
    private SKPaint _whiteKeyGridPaint = new SKPaint { Color = new SKColor(230, 230, 230), StrokeWidth = 1 };

    public int KeyHeight = 12;
    private SKBitmap _renderImage;

    private bool _isLoaded = false;
    private long _framesCount = -1;
    private List<Class1> _pitches;
    private List<Class1> _dynamics;
    private MusicXmlPhrase _score;
    private float _frameWidth = 0.8f;
    private ScalingConverter _scaling;

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
        this.Redraw();
    }

    internal NeutrinoTrack? Track
    {
        get => this.GetValue(TrackProperty) as NeutrinoTrack;
        set => this.SetValue(TrackProperty, value);
    }

    public static readonly DependencyProperty TrackProperty =
        DependencyProperty.Register(nameof(Track), typeof(NeutrinoTrack), typeof(PlotEditor), new PropertyMetadata(null, OnPropertyChanged));

    private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PlotEditor editor)
        {
            if (e.NewValue is NeutrinoTrack track)
            {
                editor.Load(track);
            }
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

    private void UpdateRenderImage()
    {
        (int width, int height) = (this.GetRenderWidth(), this.GetRenderHeight());
        if (width > 0 && height > 0)
        {
            using (this._renderImage)
            {
                this._renderImage = CreateRenderImage();
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
    }

    private double GetVerticalScrollCoe()
    {
        return (double)vScrollBar1.Value / MaxVScrollHeight;
    }

    private double GetHorizontalScrollCore()
    {
        return (double)hScrollBar1.Value / MaxHScrollHeight;
    }

    private SKBitmap CreateRenderImage()
    {
        (int rulerHeight, var scaling) = (this._rulerHeight, this._scaling);

        int scoreHeight = KeyHeight * KeyCount;

        // 描画領域
        int renderWidth = this.GetRenderWidth();
        int width = scaling.ToRenderImageScaling(renderWidth);
        int height = rulerHeight + (KeyHeight * KeyCount);
        int renderHeight = scaling.ToDisplayScaling(height);

        int scoreYOffset = scaling.ToDisplayScaling(rulerHeight);

        int scoreRenderWidth = renderWidth;
        int scoreWidth = width;
        int scoreRenderHeight = scaling.ToDisplayScaling(scoreHeight);

        var image = new SKBitmap(renderWidth, renderHeight);

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

                int renderWidth = this.GetRenderWidth();

                int offsetFrames = 1;

                // 描画するフレーム数
                int viewFrames = (int)Math.Ceiling(((double)renderWidth / this._frameWidth));
                int framesCount = viewFrames + (offsetFrames * 2);
                // float renderOffset =  viewFrames * this._frameWidth;

                // 開始フレーム位置
                int beginFrameIdx = (int)Math.Ceiling(this.GetHorizontalScrollCore() * (totalFrameCount - framesCount));
                int endFrameIdx = beginFrameIdx + framesCount;

                int offsetTemp = 1;
                int beginFrameIdxOffsetted = beginFrameIdx - offsetTemp;
                int endFrameIdxOffsetted = endFrameIdx + offsetTemp;

                // スコアの描画
                var scores = this._score.Frames.Where(i => i.BeginFrame <= endFrameIdx && i.EndFrame >= beginFrameIdx).ToArray();
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
                                scoreYOffset + scaling.ToDisplayScaling(height - dynamicsOffset - (lower + diff * ((dynamics.Values[i] + 4f) / 4f))));
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

        // スクロール位置から描画位置(y)を計算
        int renderY = (int)Math.Floor(this.GetVerticalScrollCoe() * (renderHeight - height));
        var rect1 = SKRect.Create(0, renderY, width, height); // test
        var rect2 = SKRect.Create(0, 0, width, height); // test
        g.DrawBitmap(keysBackground, SKRect.Create(0, renderY, width, height), SKRect.Create(0, 0, width, height));

    }

    private void Load(NeutrinoTrack track)
    {
        var features = track.GetFeatures();

        // 楽譜情報解析
        this._score = MusicXmlUtil.Parse(track.MusicXml);
        this._pitches = Parse(features.F0!, 0.0f);
        this._dynamics = Parse(GetDynamicsFromMspec(features.Mspec, features.F0), -3.99f);
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


    public static List<Class1> Parse(IReadOnlyCollection<float> values, float min)
    {
        var items = new List<Class1>(values.Count());

        int idx = 0;

        int tempIdx = 0;
        List<float>? tempItems = null;

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
                    tempItems = new List<float>();
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
        }
    }

    /// <summary>
    /// 暫定で平均値をしておく
    /// </summary>
    /// <param name="mspec"></param>
    /// <param name="f0"></param>
    /// <returns></returns>
    private IReadOnlyCollection<float> GetDynamicsFromMspec(float[] mspec, float[] f0)
    {
        int dimentions = mspec.Length / f0.Length;

        int length = mspec.Length / dimentions;
        var list = new float[length];

        for (int i = 0; i < length; ++i)
        {
            float value = 0.0f;
            for (int d = 0, start = (i * dimentions); d < dimentions; ++d)
            {
                value += mspec[start + d];
            }

            list[i] = value / dimentions;
        }

        return list;
    }
}
