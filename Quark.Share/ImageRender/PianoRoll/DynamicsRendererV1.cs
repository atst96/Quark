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
internal class DynamicsRendererV1
{
    private readonly RenderInfoCommon _renderInfo;

    public DynamicsRendererV1(RenderInfoCommon renderInfo)
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

        if (ri.Track is not NeutrinoV1Track track || rangeScoreInfo == null)
            return new(renderWidth, renderHeight, SKColorType.Rgb888x, SKAlphaType.Unknown);

        SKBitmap image;

        var phrases = track.Phrases!;

        // 描画対象のフレーズ情報
        var targetPhrases = phrases
                    .Where(p => beginTime <= p.EndTime && p.BeginTime <= endTime);

        // Mgcデータの次元数
        int dimension = 60; // mgc.Length / f0.Length

        // 背景を塗りつぶす
        // MEMO: アルファ値なしの場合は初期化不要
        //using (var g = new SKCanvas(image))
        //{
        //    g.DrawRect(0, 0, image.Width, image.Height, new SKPaint { Color = SKColors.Black, });
        //    g.Flush();
        //}

        // Mgcデータの下限値
        const double min = -30d;

        const int Period = RenderConfig.FramePeriod;

        var dynamicsGroups = targetPhrases
             .Where(p => p.Mgc is not null)
             .SelectMany(p => PhraseUtils.EnumerateGreaterThanForLowerRanges(p, p.Mgc!, min, dimension, p.BeginTime / Period))
             .OrderBy(i => i.PhraseBeginFrameIdx + i.BeginIndex)
             .GroupingAdjacentRange(i => i.TotalBeginIndex, i => i.TotalEndIndex);

        int offsetMs = (beginFrameIdx * Period) - beginTime;

        var list = new List<(SKPoint[] origPoints, SKPoint[] editedPoints)>(phrases.Length);

        using (var spectrumImage = new SKBitmap(frames, dimension, SKColorType.Rgb888x, SKAlphaType.Unknown))
        {
            var rawPixels = spectrumImage.GetPixelSpan();
            var pixels = MemoryMarshal.Cast<byte, RGBX>(
                MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(rawPixels), rawPixels.Length));

            foreach (var dynamicsGroup in dynamicsGroups)
            {
                int count = dynamicsGroup.Last().TotalEndIndex - dynamicsGroup.First().TotalBeginIndex + 1;
                var origPoints = new SKPoint[count];
                var editedPoints = new SKPoint[count];
                int pointsIdx = 0;

                foreach (var dynamics in dynamicsGroup)
                {
                    var phrase = dynamics.Phrase;
                    double[] mgc = phrase.Mgc!;
                    double[] editedMgc = phrase.GetEditedMgc()!;

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

                        double v = 0;
                        for (int dim = 0; dim < dimension; ++dim)
                        {
                            int x = frameIdx + dynamics.PhraseBeginFrameIdx - beginFrameIdx;
                            int y = (dimension - dim - 1) * frames;

                            double value = editedMgc[(frameIdx * dimension) + dim];

                            v += value;

                            // byte color = (byte)(baseColor - ((value - min) / (-min) * baseColor));
                            const byte baseColor = 100;
                            byte color = (byte)(Math.Min(v - min, -min) / (-min) * baseColor);

                            pixels[y + x].SetColor(allColor: color);
                        }

                        {
                            double value = mgc[frameIdx * dimension];
                            double value2 = editedMgc[frameIdx * dimension];

                            float x = scaling.ToDisplayScaling((offsetMs + ((frameIdx + dynamics.PhraseBeginFrameIdx) * Period) - beginTime) * renderInfo.WidthStretch);

                            origPoints[pointsIdx] = new(x, (float)((1 - ((value + (-min)) / (-min))) * renderHeight));
                            editedPoints[pointsIdx] = new(x, (float)((1 - ((value2 + (-min)) / (-min))) * renderHeight));
                            ++pointsIdx;
                        }
                    }
                }

                if (pointsIdx > 0)
                {
                    var range = 0..pointsIdx;
                    list.Add((origPoints[range], editedPoints[range]));
                }
            }

            image = spectrumImage.Resize(new SKImageInfo(renderWidth, renderHeight), SKFilterQuality.None);
        }


        using (var g = new SKCanvas(image))
        {
            foreach (var (origPoints, editedPoints) in list)
            {
                g.DrawPoints(SKPointMode.Polygon, origPoints, new SKPaint { IsStroke = true, Color = SKColors.SkyBlue, StrokeWidth = 1.5f });
                g.DrawPoints(SKPointMode.Polygon, editedPoints, new SKPaint { IsStroke = true, Color = SKColors.Blue, StrokeWidth = 1.5f });
            }
        }

        return image;
    }
}
