using Quark.Compatibles;
using System.Runtime.InteropServices;
using Quark.Projects.Tracks;
using Quark.Utils;
using SkiaSharp;
using Quark.Extensions;
using System.Runtime.CompilerServices;

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float ToLinear(float value) => NeutrinoUtil.MspecToLinear(value);

    public SKBitmap CreateImage()
    {
        var ri = this._renderInfo;

        var rangeScoreInfo = ri.RangeScoreRenderInfo;
        var rangeInfo = ri.RenderRange;

        var renderLayout = ri.ScreenLayout;

        var scaling = renderLayout.Scaling;

        // 描画開始・終了位置
        int beginTime = rangeInfo.BeginTime;
        int endTime = rangeInfo.EndTime;

        // フレームの描画範囲
        int beginFrameIdx = NeutrinoUtil.MsToFrameIndex(beginTime);
        int endFrameIdx = beginFrameIdx + rangeInfo.FramesCount;
        int frames = endFrameIdx - beginFrameIdx;

        if (!renderLayout.HasDynamicsArea)
            return new(1, 1, SKColorType.Rgb888x, SKAlphaType.Unknown);

        (int renderWidth, int renderHeight) = renderLayout.DynamicsArea.Size;

        if (ri.Track is not NeutrinoV2Track track || rangeScoreInfo == null)
            return new(renderWidth, renderHeight, SKColorType.Rgb888x, SKAlphaType.Unknown);

        var image = new SKBitmap(renderWidth, renderHeight, false);

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

        float lower = -6.0f;

        float padding = -lower;
        float linearLower = ToLinear(-6.0f + padding);
        float linearUpper = ToLinear(1.0f + padding);
        float linearDelta = linearUpper - linearLower;

        var dynamicsGroups = targetPhrases
             .Where(p => p.Mspec is not null)
             .SelectMany(p => PhraseUtils.EnumerateGreaterThanForLowerRanges(p, p.Mspec!, lower, dimension, NeutrinoUtil.MsToFrameIndex(p.BeginTime)))
             .OrderBy(i => i.PhraseBeginFrameIdx + i.BeginIndex)
             .GroupingAdjacentRange(i => i.TotalBeginIndex, i => i.TotalEndIndex);

        int offsetMs = NeutrinoUtil.FrameIndexToMs(beginFrameIdx) - beginTime;

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
                    float[] editedMspec = phrase.GetEditingMspec()!;

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

                            float value = ToLinear(editedMspec[(frameIdx * dimension) + dim] + padding);

                            const float baseColor = 100f;
                            byte color = (byte)Math.Max(0, Math.Min(baseColor, (value - linearLower) / linearDelta * baseColor));

                            pixels[y + x].SetColor(allColor: color);
                        }

                        {
                            double origValue = origMspec.AsSpan(frameIdx * dimension, dimension).Average();

                            float x = renderLayout.GetRenderPosXFromTime(offsetMs + NeutrinoUtil.FrameIndexToMs(frameIdx + dynamics.PhraseBeginFrameIdx) - beginTime);

                            dynamicsValue = ToLinear(dynamicsValue + padding);
                            origValue = ToLinear((float)(origValue + padding));
                            min = ToLinear(min + padding);
                            max = ToLinear(max + padding);

                            editedPoints[pointsIdx] = new(x, (float)((1 - ((dynamicsValue - linearLower) / linearDelta)) * renderHeight));
                            origPoints[pointsIdx] = new(x, (float)((1 - ((origValue - linearLower) / linearDelta)) * renderHeight));
                            minPoints[pointsIdx] = new(x, (float)((1 - ((min - linearLower) / linearDelta)) * renderHeight));
                            maxPoints[pointsIdx] = new(x, (float)((1 - ((max - linearLower) / linearDelta)) * renderHeight));

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

            using (var g = new SKCanvas(image))
            {
                int offsetX = renderLayout.GetRenderPosXFromTime(offsetMs);
                g.DrawBitmap(spectrumImage.Resize(new SKImageInfo(renderWidth, renderHeight), SKFilterQuality.None), offsetX, 0);

                var rangeBrush = new SKPaint { IsStroke = true, Color = SKColors.SkyBlue.WithAlpha(100), StrokeWidth = 1.5f };
                var origBrush = new SKPaint { IsStroke = true, Color = SKColors.SkyBlue.WithAlpha(100), StrokeWidth = 1.5f };
                var editedBrush = new SKPaint { IsStroke = true, Color = SKColors.DeepSkyBlue, StrokeWidth = 1.5f };

                foreach (var (origPoints, editedPoints, minPoints, maxPoints) in list)
                {
                    // 最小値を描画
                    g.DrawPoints(SKPointMode.Polygon, minPoints, rangeBrush);
                    // 最大値の描画
                    g.DrawPoints(SKPointMode.Polygon, maxPoints, rangeBrush);
                    // 変更前の値を描画
                    g.DrawPoints(SKPointMode.Polygon, origPoints, origBrush);
                    // 変更後の値を描画
                    g.DrawPoints(SKPointMode.Polygon, editedPoints, editedBrush);
                }
            }
        }

        return image;
    }
}
