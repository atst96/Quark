using Quark.Controls;
using Quark.Drawing;
using SkiaSharp;

namespace Quark.ImageRender.Parts;
internal class RulerRenderer
{
    private readonly RenderInfoCommon _renderInfo;

    public RulerRenderer(RenderInfoCommon renderInfo)
    {
        this._renderInfo = renderInfo;
    }

    public SKBitmap CreateImage()
    {
        var ri = this._renderInfo;

        var renderLayout = ri.ScreenLayout;
        var renderArea = renderLayout.RulerArea;
        var image = new SKBitmap(renderArea.Width, renderArea.Height, isOpaque: true);

        using (var g = new SKCanvas(image))
        {
            this.Draw(g, renderArea);
        }

        return image;
    }

    public void Draw(SKCanvas g, LayoutRect renderArea)
    {
        var ri = this._renderInfo;

        var rangeScoreInfo = ri.RangeScoreRenderInfo;
        var renderLayout = ri.ScreenLayout;
        var renderRange = ri.RenderRange;

        (int renderWidth, int renderHeight) = renderArea.Size;

        g.DrawRect(0, 0, renderWidth, renderHeight, new SKPaint() { Color = SKColors.Black });

        if (rangeScoreInfo != null)
        {
            // 描画開始・終了位置
            int beginTime = renderRange.BeginTime;

            // 小節、4分音符、8分音符時の描画位置
            float measureLineY = 0.0f;
            float beat4thLineY = renderHeight * 0.4f;
            float beat8thLineY = renderHeight * 0.7f;

            foreach (var rulerLine in rangeScoreInfo.RulerLines)
            {
                float scaledX = renderLayout.GetRenderPosXFromTime((int)rulerLine.Time - beginTime);

                var linePosY = rulerLine.LineType switch
                {
                    LineType.Measure => measureLineY,
                    LineType.Whole or LineType.Note2th or LineType.Note4th => beat4thLineY,
                    _ => beat8thLineY,
                };

                g.DrawLine(
                    scaledX, linePosY,
                    scaledX, renderHeight,
                    new SKPaint { StrokeWidth = 1, Color = SKColors.White });
            }


        }
    }
}
