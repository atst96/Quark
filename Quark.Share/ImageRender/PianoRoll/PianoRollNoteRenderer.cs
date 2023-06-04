using SkiaSharp;

namespace Quark.ImageRender.Score;

/// <summary>
/// ピアノロールのノートを描画するクラス
/// </summary>
internal class PianoRollNoteRenderer
{
    private SKPaint lyricsTypography = new(new SKFont(SKTypeface.FromFamilyName("MS UI Gothic"), 12));

    private readonly RenderInfoCommon _renderInfo;

    public PianoRollNoteRenderer(RenderInfoCommon renderInfo)
    {
        this._renderInfo = renderInfo;
    }
    public void Render(SKCanvas g)
    {
        var ri = this._renderInfo;

        var rangeScoreInfo = ri.RangeScoreRenderInfo;
        if (rangeScoreInfo == null)
            return;

        var rangeInfo = ri.RenderRange;

        var renderInfo = ri.PartRenderInfo;

        var scaling = renderInfo.Scaling;

        // 描画開始・終了位置
        int beginTime = rangeInfo.BeginTime;
        int endTime = rangeInfo.EndTime;

        // 描画領域
        int height = renderInfo.UnscaledScoreHeight;
        int keyHeight = renderInfo.KeyHeight;

        // スコアの描画
        foreach (var score in rangeScoreInfo.Score.Phrases)
        {
            float y = height - (float)(score.Pitch * keyHeight);

            var rect = SKRect.Create(
                scaling.ToDisplayScaling((score.BeginTime - beginTime) * renderInfo.WidthStretch),
                scaling.ToDisplayScaling(height - score.Pitch * keyHeight),
                scaling.ToDisplayScaling((score.EndTime - score.BeginTime) * renderInfo.WidthStretch),
                scaling.ToDisplayScaling(keyHeight));

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

            if (score.Breath)
            {
                g.DrawPath(
                    ScoreImageRender.CreateBreathMark(rect.Top - height - 1, rect.Left + rect.Width, 9, 14),
                    new SKPaint() { Color = SKColors.Blue, IsStroke = true, StrokeWidth = 1.6f, IsAntialias = true });
            }

            // 歌詞
            g.DrawText(score.Lyrics, new SKPoint(rect.Left, rect.Top), lyricsTypography);

        }
    }
}
