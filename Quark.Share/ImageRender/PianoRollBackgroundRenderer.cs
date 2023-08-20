using Quark.Drawing;
using Quark.Models.Neutrino;
using SkiaSharp;
using System.Runtime.CompilerServices;
using static Quark.Controls.EditorRenderLayout;

namespace Quark.ImageRender;

internal class PianoRollBackgroundRenderer
{
    private readonly RenderInfoCommon _renderInfo;

    public PianoRollBackgroundRenderer(RenderInfoCommon renderInfo)
    {
        this._renderInfo = renderInfo;
    }

    /// <summary>
    /// 12音階の画像を生成する
    /// </summary>
    /// <param name="width">画像幅</param>
    /// <param name="keyHeight">1音あたりの高さ</param>
    /// <param name="scaling">スケーリング情報</param>
    /// <returns></returns>
    private SKBitmap CreatePianoOctaveBmp()
    {
        const int KeyCount = 12;
        const int DefaultWidth = 100;

        var ri = this._renderInfo;

        var renderLayout = ri.ScreenLayout;
        var scaling = ri.ScreenLayout.Scaling;

        int renderKeyHeight = renderLayout.PhysicalKeyHeight;
        int width = scaling.ToDisplayScaling(DefaultWidth);
        int height = renderKeyHeight * KeyCount;

        var colorInfo = this._renderInfo.ColorInfo;
        var whiteKeyBrush = colorInfo.WhiteKeyPaint;
        var whiteGridPen = colorInfo.WhiteKeyGridPaint;
        var blackKeyBrush = colorInfo.BlackKeyPaint;

        var image = new SKBitmap(width, height, isOpaque: true);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int GetYPos(int key) => height - ((key + 1) * renderKeyHeight);

        using (var g = new SKCanvas(image))
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void DrawRect(int key, SKPaint brush)
            {
                int y = GetYPos(key);
                g.DrawRect(new(0, y, width, renderKeyHeight + y), brush);
            }

            // ストライプの描画
            DrawRect(0, whiteKeyBrush); // C
            DrawRect(1, blackKeyBrush); // C#
            DrawRect(2, whiteKeyBrush); // D
            DrawRect(3, blackKeyBrush); // D#
            DrawRect(4, whiteKeyBrush); // E
            DrawRect(5, whiteKeyBrush); // F
            DrawRect(6, blackKeyBrush); // F#
            DrawRect(7, whiteKeyBrush); // G
            DrawRect(8, blackKeyBrush); // G#
            DrawRect(9, whiteKeyBrush); // A
            DrawRect(10, blackKeyBrush); // A#
            DrawRect(11, whiteKeyBrush); // B

            // 白鍵の境界を描画
            g.DrawLine(0, GetYPos(4), width, GetYPos(4), whiteGridPen);
            g.DrawLine(0, GetYPos(11), width, GetYPos(11), whiteGridPen);
        }

        return image;
    }

    public void Render(SKCanvas g)
    {
        var _renderInfo = this._renderInfo.ScreenLayout;
        var rangeInfo = this._renderInfo.RenderRange;
        var scaling = _renderInfo.Scaling;

        // 描画領域
        (int renderWidth, int renderHeight) = _renderInfo.ScoreImage.Size;
        (int width, int height) = _renderInfo.ScoreImage.Size;

        var partImage = this.CreatePianoOctaveBmp();
        int partImageWidth = partImage.Width;
        int partImageHeight = partImage.Height;

        int offset = partImageHeight - (height % partImageHeight);

        int scoreWidth = width;
        int scoreHeight = height;

        int vCount = (int)Math.Ceiling((double)scoreHeight / partImageHeight);
        int hCount = (int)Math.Ceiling((double)scoreWidth / partImageWidth);

        int[] xList = Enumerable.Range(0, hCount)
            .Select(x => x * partImageWidth)
            .ToArray();

        for (int yCount = 0; yCount < vCount; ++yCount)
        {
            int y = (yCount * partImageHeight) - offset;

            foreach (int x in xList)
            {
                g.DrawBitmap(partImage, x, y);
            }
        }

        var rangeScoreInfo = this._renderInfo.RangeScoreRenderInfo;
        if (rangeScoreInfo != null)
        {
            var track = this._renderInfo.Track;

            long totalFrameCount = track.GetTotalFramesCount();

            // 描画開始・終了位置
            int beginTime = rangeInfo.BeginTime;
            int endTime = rangeInfo.EndTime;

            // フレームの描画範囲
            int beginFrameIdx = beginTime / RenderConfig.FramePeriod;
            int endFrameIdx = beginFrameIdx + rangeInfo.FramesCount;

            // 描画対象のフレーズ情報
            var targetPhrases = this._renderInfo.Track.Phrases
                .Where(p => beginTime <= p.EndTime && p.BeginTime <= endTime);

            // フレーズ枠の描画
            foreach (var phrase in targetPhrases)
            {
                SKColor? color = phrase.Status switch
                {
                    PhraseStatus.WaitEstimate => new SKColor(0, 0, 255, 10),
                    PhraseStatus.EstimateProcessing => new SKColor(0, 0, 255, 20),
                    PhraseStatus.WaitAudioRender => new SKColor(255, 0, 0, 10),
                    PhraseStatus.AudioRenderProcessing => new SKColor(255, 0, 0, 20),
                    _ => (SKColor?)null
                };

                if (color == null)
                    continue;

                int ofx = 0;
                int x = _renderInfo.GetRenderPosXFromTime(phrase.BeginTime - beginTime);
                // scaling.ToDisplayScaling((phrase.BeginTime - beginTime) * _renderInfo.WidthStretch);
                if (x < 0)
                {
                    ofx = x;
                    x = 0;
                }

                int y = 0;
                int w = _renderInfo.GetRenderPosXFromTime(phrase.EndTime - phrase.BeginTime) + ofx;
                if ((x + w) > _renderInfo.ScoreArea.Width)
                    w = _renderInfo.ScoreArea.Width - x;
                int h = renderHeight;

                g.DrawRect(x, y, w, h, new SKPaint { Color = color.Value });
            }

            // 罫線の描画
            foreach (var noteLine in rangeScoreInfo.NoteLines)
            {
                float scaledX = _renderInfo.GetRenderPosXFromTime((int)noteLine.Time - beginTime);

                var lineColor = noteLine.LineType switch
                {
                    LineType.Measure => SKColors.Black,
                    LineType.Whole => SKColors.DarkGray,
                    LineType.Note2th => SKColors.DarkGray,
                    LineType.Note4th => SKColors.DarkGray,
                    _ => SKColors.LightGray,
                };

                g.DrawLine(
                    scaledX, 0,
                    scaledX, _renderInfo.ScoreImage.Height,
                    new SKPaint { Color = lineColor, StrokeWidth = 1 });
            }
        }
    }
}
