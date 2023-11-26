using System.Diagnostics;
using System.Runtime.CompilerServices;
using SkiaSharp;

namespace Quark.ImageRender;

internal class PianoRollKeysRenderer
{
    /// <summary>白鍵数</summary>
    private const int WhiteKeyCount = 7;
    /// <summary>黒鍵数</summary>
    private const int BlackKeyCount = 5;
    /// <summary>1オクターブあたりの鍵数(白鍵数+黒鍵数)</summary>
    private const int KeyCount = 12;

    public PianoRollKeysRenderer()
    {
    }

    private SKBitmap CreatePianoOctaveKeysBmp(RenderInfoCommon ri)
    {
        var renderLayout = ri.ScreenLayout;
        int renderKeyHeight = renderLayout.PhysicalKeyHeight;
        int width = renderLayout.ScoreArea.X;
        int height = renderKeyHeight * KeyCount;

        var colors = ri.ColorInfo;

        var whiteKeySolidBrush = colors.WhiteKeyBackgroundBrush;
        var blackKeyBackgroundBrush = colors.BlackKeyBackgroundBrush;
        var whiteKeyBorderBrush = colors.WhiteKeyBorderBrush;

        var image = new SKBitmap(width, height, isOpaque: true);

        using (var g = new SKCanvas(image))
        {
            // 背景の描画
            g.DrawRect(new SKRect(0, 0, width, height), whiteKeySolidBrush);

            float whiteKeysHeight = (float)height / WhiteKeyCount;

            // 白鍵の描画
            var whiteKeyLinePoints = new SKPoint[12];
            for (int i = 0; i < 6; ++i)
            {
                float h = (i + 1) * whiteKeysHeight;
                whiteKeyLinePoints[(i * 2) + 0] = new(0, h);
                whiteKeyLinePoints[(i * 2) + 1] = new(width, h);
            }
            g.DrawPoints(SKPointMode.Lines, whiteKeyLinePoints, whiteKeyBorderBrush);

            // 黒鍵の描画
            int[] blacks = [0, 1, 2, 4, 5];
            float blackKeyWidth = width * 0.6f;
            float blackKeyYOffset = (float)height / BlackKeyCount * .5f;
            foreach (int i in blacks)
            {
                float y = (i * whiteKeysHeight) + blackKeyYOffset;
                g.DrawRect(SKRect.Create(0, y, blackKeyWidth, renderKeyHeight), blackKeyBackgroundBrush);
            }

            // 外部枠線の描画
            SKPoint[] points = [
                new(width - 1, 0),
                new(width - 1, height),
                new(0, height - 1),
            ];
            g.DrawPoints(SKPointMode.Polygon, points, whiteKeyBorderBrush);
        }

        return image;
    }

    public void Render(SKCanvas g, RenderInfoCommon ri)
    {
        var layout = ri.ScreenLayout;

        // 描画領域
        var keysImage = this.CreatePianoOctaveKeysBmp(ri);
        int keyImageHeight = keysImage.Height;
        int scoreY = layout.ScoreArea.Y;
        int scoreHeight = layout.ScoreImage.Height;
        int scoreAreaHeight = layout.ScoreArea.Height;

        // 1オクターブ単位での描画開始位置
        int scrollPosition = ri.VScrollPosition;
        int beginYOffset = -((scrollPosition % keyImageHeight) - (scoreHeight % keyImageHeight)) + scoreY;

        int renderHeight = Math.Min(scoreHeight - scrollPosition, scoreAreaHeight);

        int tileIdx = 0;
        int maxTilingPos = (int)Math.Ceiling((double)(renderHeight - (beginYOffset - scoreY)) / keyImageHeight);

        if (scoreY < beginYOffset)
            tileIdx = -1;

        for (; tileIdx < maxTilingPos; ++tileIdx)
        {
            // 鍵盤の描画位置を計算
            int x = 0;
            int srcY = 0;
            int dstY = (tileIdx * keyImageHeight) + beginYOffset;
            int w = keysImage.Width;
            int h = keyImageHeight;

            // 不要な上辺があるならそれを省いた描画位置と高さに補正する
            int topUnnecessary = scoreY - dstY;
            if (topUnnecessary > 0)
            {
                h = keyImageHeight - topUnnecessary;
                srcY = topUnnecessary;
                dstY = scoreY;
            }

            // 下辺にはみ出しがあればはみ出しのない高さに補正する
            int bottomUnnecessary = dstY + h - layout.EditArea.Height;
            if (bottomUnnecessary > 0)
                h -= bottomUnnecessary;

            // 鍵盤の画像を描画する
            if (h > 0)
                g.DrawBitmap(keysImage, SKRect.Create(x, srcY, w, h), SKRect.Create(x, dstY, w, h));
        }

        // 描画領域内で描画しない部分があれば単色で埋める
        int remaining = scoreAreaHeight - renderHeight;
        if (remaining > 0)
        {
            g.DrawRect(
                SKRect.Create(0, scoreY + renderHeight, keysImage.Width, remaining),
                ri.ColorInfo.WhiteKeyBackgroundBrush);
        }
    }
}
