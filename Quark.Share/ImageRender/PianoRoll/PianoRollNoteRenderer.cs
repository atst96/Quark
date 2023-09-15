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
        var renderLayout = ri.ScreenLayout;

        // 描画開始・終了位置
        int beginTime = rangeInfo.BeginTime;
        int endTime = rangeInfo.EndTime;

        // 描画領域
        int height = renderLayout.ScoreImage.Height;
        int keyHeight = renderLayout.PhysicalKeyHeight;

        // スコアの描画
        foreach (var score in rangeScoreInfo.Score.Phrases)
        {
            float y = height - (float)(score.Pitch * keyHeight);

            var rect = SKRect.Create(
                renderLayout.GetRenderPosXFromTime(score.BeginTime - beginTime),
                height - (score.Pitch * keyHeight),
                renderLayout.GetRenderPosXFromTime(score.EndTime - score.BeginTime),
                keyHeight);
            var roundRect = new SKRoundRect(rect, 2);

            g.DrawRoundRect(roundRect, new SKPaint
            {
                Color = new(164, 197, 245),
                Style = SKPaintStyle.Fill,
            });
            g.DrawRoundRect(roundRect, new SKPaint
            {
                Color = new SKColor(74, 126, 187),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.0f,
                IsStroke = true,
            });

            if (score.Breath)
            {
                // TOD: ブレスマークのサイズを可変にする
                const float bressMarkWidth = 11f;
                const float bressMarkHeight = 16f;
                g.DrawPath(
                    CreateBreathMark(rect.Left + rect.Width, rect.Top, bressMarkWidth, bressMarkHeight),
                    new SKPaint() { Color = new(44, 96, 157), IsStroke = true, StrokeWidth = 2f, IsAntialias = true });
            }

            // 歌詞
            g.DrawText(score.Lyrics, new(rect.Left, rect.Top), lyricsTypography);

        }
    }

    /// <summary>
    /// ブレスマークのパスを生成する
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="width">描画幅</param>
    /// <param name="height">描画高</param>
    /// <returns></returns>
    private static SKPath CreateBreathMark(float x, float y, float width, float height)
    {
        float halfWidth = (width - 1) / 2;
        float offsetX = y - height;

        SKPoint[] points =
        {
            // 左上
            new(x - halfWidth, offsetX),
            // 下
            new(x, offsetX + height),
            // 右上
            new(x + halfWidth, offsetX),
        };

        var path = new SKPath();
        path.AddPoly(points, close: false);

        return path;
    }
}
