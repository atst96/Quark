using Quark.Compatibles;
using System.Runtime.InteropServices;
using Quark.Projects.Tracks;
using Quark.Utils;
using SkiaSharp;
using static Quark.Controls.EditorRenderLayout;
using Quark.Extensions;
using System.Runtime.CompilerServices;

namespace Quark.ImageRender.Score;

/// <summary>
/// 音量を描画する
/// </summary>
internal class DynamicsRendererV1
{
    public DynamicsRendererV1()
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ToLinear(double value) => NeutrinoUtil.MgcToLinear(value);

    public void Render(SKCanvas g, RenderInfoCommon ri)
    {
        var rangeScoreInfo = ri.RangeScoreRenderInfo;
        var rangeInfo = ri.RenderRange;
        var renderLayout = ri.ScreenLayout;
        var scaling = renderLayout.Scaling;

        // 描画開始・終了位置
        int beginTime = rangeInfo.BeginTime;
        int endTime = rangeInfo.EndTime;

        // フレームの描画範囲
        int beginFrameIdx = beginTime / RenderConfig.FramePeriod;
        int endFrameIdx = beginFrameIdx + rangeInfo.FramesCount;
        int frames = endFrameIdx - beginFrameIdx;

        if (!renderLayout.HasDynamicsArea || ri.Track is not NeutrinoV1Track track || rangeScoreInfo == null)
            return;

        (int renderWidth, int renderHeight) = renderLayout.DynamicsArea.Size;

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

        var dynamicsGroups = targetPhrases
             .Where(p => p.Mgc is not null)
             .SelectMany(p => PhraseUtils.EnumerateGreaterThanForLowerRanges(p, p.Mgc!, min, dimension, NeutrinoUtil.MsToFrameIndex(p.BeginTime)))
             .OrderBy(i => i.PhraseBeginFrameIdx + i.BeginIndex)
             .GroupingAdjacentRange(i => i.TotalBeginIndex, i => i.TotalEndIndex);

        int offsetMs = NeutrinoUtil.FrameIndexToMs(beginFrameIdx) - beginTime;

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

                        for (int dim = 0; dim < dimension; ++dim)
                        {
                            int x = frameIdx + dynamics.PhraseBeginFrameIdx - beginFrameIdx;
                            int y = (dimension - dim - 1) * frames;

                            double value = ToLinear(editedMgc[(frameIdx * dimension) + dim]);

                            const float baseColor = 100;
                            byte color = (byte)Math.Max(0, Math.Min(baseColor, Math.Min(value - min, -min) / (-min) * baseColor));

                            pixels[y + x].SetColor(allColor: color);
                        }

                        {
                            double value = ToLinear(mgc[frameIdx * dimension]);
                            double value2 = ToLinear(editedMgc[frameIdx * dimension]);

                            float x = renderLayout.GetRenderPosXFromTime(offsetMs + NeutrinoUtil.MsToFrameIndex(frameIdx + dynamics.PhraseBeginFrameIdx) - beginTime);

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

            {
                int offsetX = renderLayout.GetRenderPosXFromTime(offsetMs);
                g.DrawBitmap(spectrumImage.Resize(new SKImageInfo(renderWidth, renderHeight), SKFilterQuality.None), offsetX, 0);


                var origBrush = new SKPaint { IsStroke = true, Color = SKColors.SkyBlue.WithAlpha(100), StrokeWidth = 1.5f };
                var editedBrush = new SKPaint { IsStroke = true, Color = SKColors.DeepSkyBlue, StrokeWidth = 1.5f };

                foreach (var (origPoints, editedPoints) in list)
                {
                    g.DrawPoints(SKPointMode.Polygon, origPoints, origBrush);
                    g.DrawPoints(SKPointMode.Polygon, editedPoints, editedBrush);
                }
            }
        }
    }
}
