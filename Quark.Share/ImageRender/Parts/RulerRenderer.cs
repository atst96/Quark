using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        var renderInfo = ri.PartRenderInfo;

        int renderWidth = renderInfo.RenderWidth;
        int renderHeight = renderInfo.RenderRulerHeight;

        var image = new SKBitmap(renderWidth, renderHeight, isOpaque: true);

        using (var g = new SKCanvas(image))
        {
            this.Draw(g);
        }

        return image;
    }

    public void Draw(SKCanvas g)
    {
        var ri = this._renderInfo;

        var rangeScoreInfo = ri.RangeScoreRenderInfo;
        var renderInfo = ri.PartRenderInfo;
        var renderRange = ri.RenderRange;

        var scaling = renderInfo.Scaling;

        int renderWidth = renderInfo.RenderWidth;
        int renderHeight = renderInfo.RenderRulerHeight;

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
                float scaledX = scaling.ToDisplayScaling(((int)rulerLine.Time - beginTime) * renderInfo.WidthStretch);

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
