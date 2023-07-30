using Quark.Compatibles;
using System.Runtime.InteropServices;
using Quark.Projects.Tracks;
using Quark.Utils;
using SkiaSharp;
using static Quark.Controls.ViewDrawingBoxInfo;
using Quark.Extensions;

namespace Quark.ImageRender.Score;

/// <summary>
/// 音量を描画する
/// </summary>
internal class DynamicsRendererV2
{
    private readonly RenderInfoCommon _renderInfo;

    public DynamicsRendererV2(RenderInfoCommon renderInfo)
    {
        this._renderInfo = renderInfo;
    }

    public SKBitmap CreateImage()
    {
        var ri = this._renderInfo;

        var rangeScoreInfo = ri.RangeScoreRenderInfo;
        var rangeInfo = ri.RenderRange;

        var renderInfo = ri.PartRenderInfo;

        var scaling = renderInfo.Scaling;

        // 描画開始・終了位置
        int beginTime = rangeInfo.BeginTime;
        int endTime = rangeInfo.EndTime;

        // フレームの描画範囲
        int beginFrameIdx = beginTime / RenderConfig.FramePeriod;
        int endFrameIdx = beginFrameIdx + rangeInfo.FramesCount;
        int frames = endFrameIdx - beginFrameIdx;
        (int renderWidth, int renderHeight) = (renderInfo.RenderDisplayWidth, renderInfo.DynamicRenderHeight);

        if (ri.Track is not NeutrinoV2Track track || rangeScoreInfo == null)
            return new(renderWidth, renderHeight, SKColorType.Rgb888x, SKAlphaType.Unknown);

        SKBitmap image;

        var phrases = track.Phrases!;

        // 描画対象のフレーズ情報
        var targetPhrases = phrases
                    .Where(p => beginTime <= p.EndTime && p.BeginTime <= endTime);

        // Mgcデータの次元数
        int dimension = 100; // mgc.Length / f0.Length

        // 背景を塗りつぶす
        // MEMO: アルファ値なしの場合は初期化不要
        //using (var g = new SKCanvas(image))
        //{
        //    g.DrawRect(0, 0, image.Width, image.Height, new SKPaint { Color = SKColors.Black, });
        //    g.Flush();
        //}

        // Mgcデータの下限値
        const float lower = -6.0f;
        const float upper = 1.0f;
        const float period = upper - lower;

        const int Period = RenderConfig.FramePeriod;

        var dynamicsGroups = targetPhrases
             .Where(p => p.Mspec is not null)
             .SelectMany(p => PhraseUtils.EnumerateGreaterThanForLowerRanges(p, p.Mspec!, lower, dimension, p.BeginTime / Period))
             .OrderBy(i => i.PhraseBeginFrameIdx + i.BeginIndex)
             .GroupingAdjacentRange(i => i.TotalBeginIndex, i => i.TotalEndIndex);

        int offsetMs = (beginFrameIdx * Period) - beginTime;

        var list = new List<(SKPoint[] origValues, SKPoint[] editedValues, SKPoint[] min, SKPoint[] max)>(phrases.Length);

        using (var spectrumImage = new SKBitmap(frames, dimension, SKColorType.Rgb888x, SKAlphaType.Unknown))
        {
            var rawPixels = spectrumImage.GetPixelSpan();
            var pixels = MemoryMarshal.Cast<byte, RGBX>(
                MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(rawPixels), rawPixels.Length));

            foreach (var dynamicsGroup in dynamicsGroups)
            {
                int count = dynamicsGroup.Last().TotalEndIndex - dynamicsGroup.First().TotalBeginIndex + 1;
                var editedPoints = new SKPoint[count];
                var origPoints = new SKPoint[count];
                var minPoints = new SKPoint[count];
                var maxPoints = new SKPoint[count];
                int pointsIdx = 0;

                foreach (var dynamics in dynamicsGroup)
                {
                    var phrase = dynamics.Phrase;
                    float[] origMspec = phrase.Mspec!;
                    float[] editedMspec = phrase.GetEditedMspec()!;

                    // 描画開始／終了インデックス
                    (int beginIdx, int endIdx) = DrawUtil.GetDrawRange(
                        dynamics.PhraseBeginFrameIdx + dynamics.BeginIndex, dynamics.EndIndex - dynamics.BeginIndex + 1,
                        beginFrameIdx, endFrameIdx, 0);

                    if (beginIdx >= endIdx)
                        continue;

                    int f = beginIdx - dynamics.PhraseBeginFrameIdx;

                    for (int idx = 0, length = (endIdx - beginIdx); idx < length; ++idx)
                    {
                        int frameIdx = idx + f;

                        (float min, float max, float dynamicsValue) = editedMspec.AsSpan(frameIdx * dimension, dimension).MinMaxAvg();

                        for (int dim = 0; dim < dimension; ++dim)
                        {
                            int x = frameIdx + dynamics.PhraseBeginFrameIdx - beginFrameIdx;
                            int y = (dimension - dim - 1) * frames;

                            double value = editedMspec[(frameIdx * dimension) + dim];

                            // byte color = (byte)(baseColor - ((value - min) / (-min) * baseColor));
                            const byte baseColor = 255;
                            byte color = (byte)((value - lower) / (period) * baseColor);

                            pixels[y + x].SetColor(allColor: color);
                        }

                        {
                            double origValue = origMspec.AsSpan(frameIdx * dimension, dimension).Average();

                            float x = scaling.ToDisplayScaling((offsetMs + ((frameIdx + dynamics.PhraseBeginFrameIdx) * Period) - beginTime) * renderInfo.WidthStretch);

                            editedPoints[pointsIdx] = new(x, (float)((1 - ((dynamicsValue - lower) / period)) * renderHeight));
                            origPoints[pointsIdx] = new(x, (float)((1 - ((origValue - lower) / period)) * renderHeight));
                            minPoints[pointsIdx] = new(x, (float)((1 - ((min - lower) / period)) * renderHeight));
                            maxPoints[pointsIdx] = new(x, (float)((1 - ((max - lower) / period)) * renderHeight));

                            ++pointsIdx;
                        }
                    }
                }

                if (pointsIdx > 0)
                {
                    var range = 0..pointsIdx;
                    list.Add((origPoints[range], editedPoints[range], minPoints[range], maxPoints[range]));
                }
            }

            image = spectrumImage.Resize(new SKImageInfo(renderWidth, renderHeight), SKFilterQuality.None);
        }

        using (var g = new SKCanvas(image))
        {
            foreach (var (origPoints, editedPoints, minPoints, maxPoints) in list)
            {
                // 最小値を描画
                g.DrawPoints(SKPointMode.Polygon, minPoints, new SKPaint { IsStroke = true, Color = SKColors.SkyBlue, StrokeWidth = 1.5f });
                // 最大値の描画
                g.DrawPoints(SKPointMode.Polygon, maxPoints, new SKPaint { IsStroke = true, Color = SKColors.SkyBlue, StrokeWidth = 1.5f });
                // 変更前の値を描画
                g.DrawPoints(SKPointMode.Polygon, origPoints, new SKPaint { IsStroke = true, Color = SKColors.LightBlue, StrokeWidth = 1.5f });
                // 変更後の値を描画
                g.DrawPoints(SKPointMode.Polygon, editedPoints, new SKPaint { IsStroke = true, Color = SKColors.Blue, StrokeWidth = 1.5f });
            }
        }

        return image;
    }
}
