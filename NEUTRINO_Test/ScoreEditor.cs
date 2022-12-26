using SkiaSharp;
using SkiaSharp.Views.Desktop;
using System.Data;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Runtime.CompilerServices;

namespace NEUTRINO_Test;

public partial class ScoreEditor : UserControl
{
    private const int KeyCount = 88;

    private const int MaxVScrollHeight = 1000;
    private const int MaxHScrollHeight = 1000;

    private SKPaint _whiteKeyPaint = new SKPaint { Color = new SKColor(255, 255, 255) };
    private SKPaint _blackKeyPaint = new SKPaint { Color = new SKColor(230, 230, 230) };
    private SKPaint _whiteKeyGridPaint = new SKPaint { Color = new SKColor(230, 230, 230), StrokeWidth = 1 };

    public int KeyHeight = 12;
    private SKBitmap _pianoBmp;
    private SKBitmap _renderImage;

    private object @object = new object();

    private bool _isLoaded = false;
    private long _framesCount = -1;
    private List<Class1> _pitches;
    private MusicXmlPhrase _score;
    private float _frameWidth = 0.8f;

    public ScoreEditor()
    {
        this.InitializeComponent();

        this._pianoBmp = CreatePianoOctaveBmp(100, KeyHeight);

        this.editorPanel.SizeChanged += OnRenderSizeChanged;

        vScrollBar1.Minimum = 0;
        vScrollBar1.Maximum = MaxVScrollHeight;
        vScrollBar1.LargeChange = 1;

        hScrollBar1.Minimum = 0;
        hScrollBar1.Maximum = MaxHScrollHeight;
        hScrollBar1.LargeChange = 1;

        this._renderImage = CreateRenderImage();
    }

    private SKPaint lyricsTypography = new(new SKFont(SKTypeface.FromFamilyName("MS UI Gothic"), 12));

    public void Load(string f0, string mgc, string lab, string score)
    {
        // 音響情報解析
        var accoustic = SoundFileAnalyzer.Analyze(f0, mgc, lab);

        // 楽譜情報解析
        var scores = MusicXMLAnalyzer.Analyzer(score);

        this._pitches = Parse(accoustic.Pitches, 0.0d);
        // this._pitches2 = accoustic.Pitches;
        // this._volumes = Parse(accoustic.Volumes, 0.0d);
        this._score = scores;
        this._framesCount = accoustic.FramesCount;
        this._isLoaded = true;

        this.Redraw();
    }

    private SKBitmap CreatePianoOctaveBmp(int width, int keyHeight)
    {
        const int keys = 12;

        int height = keyHeight * keys;

        var whiteKeyBrush = this._whiteKeyPaint;
        var whiteGridPen = this._whiteKeyGridPaint;
        var blackKeyBrush = this._blackKeyPaint;

        var image = new SKBitmap(width, height);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int GetYPos(int key) => height - ((key + 1) * keyHeight);

        using (var g = new SKCanvas(image))
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            SKRect DrawRect(int key) => new(0, GetYPos(key), width, keyHeight + GetYPos(key));

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
            g.DrawLine(0, GetYPos(4), width, GetYPos(4), whiteGridPen);
            g.DrawLine(0, GetYPos(11), width, GetYPos(11), whiteGridPen);
        }

        return image;
    }

    private int GetRenderWidth() => this.editorPanel.Width;

    private int GetRenderHeight() => this.editorPanel.Height;

    private void UpdateRenderImage()
    {
        using (this._renderImage)
        {
            this._renderImage = CreateRenderImage();
        }
    }

    private void Redraw()
    {
        this.UpdateRenderImage();
        this.editorPanel.Invalidate();
    }

    private void OnRenderSizeChanged(object? sender, EventArgs e)
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
        (int width, int height) = (this.GetRenderWidth(), (KeyHeight * KeyCount));

        var image = new SKBitmap(width, height);


        var partImage = this._pianoBmp;

        using (var g = new SKCanvas(image))
        {
            int imageWidth = partImage.Width;
            int imageHeight = partImage.Height;
            int offset = imageHeight - (height % imageHeight);

            int vCount = (int)Math.Ceiling((double)height / imageHeight);
            int hCount = (int)Math.Ceiling((double)width / imageWidth);

            int[] xList = Enumerable.Range(0, hCount)
                .Select(x => x * imageWidth)
                .ToArray();

            for (int yCount = 0; yCount < vCount; ++yCount)
            {
                int y = (yCount * imageHeight) - offset;

                foreach (int x in xList)
                {
                    g.DrawBitmap(partImage, x, y);
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

                // スコアの描画
                var scores = this._score.Frames.Where(i => i.BeginFrame <= endFrameIdx && i.EndFrame >= beginFrameIdx).ToArray();
                {
                    for (int i = 0; i < scores.Length; ++i)
                    {
                        var score = scores[i];

                        int beginIndex = score.BeginFrame - beginFrameIdx;

                        float y = height - (float)(score.Pitch * KeyHeight);
                        var rect = SKRect.Create(beginIndex * _frameWidth, height - (score.Pitch * KeyHeight), (score.EndFrame - score.BeginFrame) * _frameWidth, KeyHeight);
                        g.DrawRect(rect, new SKPaint
                        {
                            Color = new SKColor(Color.LightSkyBlue.R, Color.LightSkyBlue.G, Color.LightSkyBlue.B),
                            Style = SKPaintStyle.Fill,
                        });
                        g.DrawRect(rect, new SKPaint
                        {
                            Color = new SKColor(Color.DarkBlue.R, Color.DarkBlue.G, Color.DarkBlue.B),
                            Style = SKPaintStyle.Stroke,
                            StrokeWidth = 1.0f,
                            IsStroke = true,
                        });
                        //// 歌詞
                        //g.DrawText(score.Lyrics, new SKPoint(rect.Left, rect.Top), lyricsTypography);
                    }
                }

                // ピッチの描画
                var pitches = this._pitches.Where(i => i.Index <= endFrameIdx && (i.Index + i.Values.Length) >= beginFrameIdx);

                float pitchOffset = (float)KeyHeight / 2;

                foreach (var pitch in pitches)
                {
                    // TODO: 範囲外の描画を行わないようにする
                    int beginIndex = pitch.Index - beginFrameIdx;
                    var points = new SKPoint[pitch.Values.Length];

                    for (int i = 0; i < pitch.Values.Length; ++i)
                    {
                        points[i] = new SKPoint((i + beginIndex) * _frameWidth, height - pitchOffset - ((float)FrequencyToScale(pitch.Values[i]) * KeyHeight));
                    }

                    g.DrawPoints(SKPointMode.Polygon, points, new SKPaint { Color = new SKColor(255, 0, 0), StrokeWidth = 1f, IsAntialias = true });

                }
            }
        }

        return image;
    }

    private void PaintGraphics(SKCanvas g, Rectangle rectangle = default)
    {
        if (DesignMode)
        {
            return;
        }

        (int width, int height) = (this.GetRenderWidth(), this.GetRenderHeight());
        var keysBackground = this._renderImage;

        // 描画領域の更新
        int renderHeight = KeyHeight * KeyCount;

        // スクロール位置から描画位置(y)を計算
        int renderY = (int)Math.Floor(this.GetVerticalScrollCoe() * (renderHeight - height));
        g.DrawBitmap(keysBackground, SKRect.Create(0, renderY, width, height), SKRect.Create(0, 0, width, height));

    }

    private void OnPaintSurface(object sender, SKPaintGLSurfaceEventArgs e)
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
        if (e.Type != ScrollEventType.First)
        {
            this.editorPanel.Invalidate();
        }
    }

    private void OnHScroll(object sender, ScrollEventArgs e)
    {
        if (e.Type != ScrollEventType.First)
        {
            this.Redraw();
        }
    }
}
