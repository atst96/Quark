using SkiaSharp;

namespace Quark.ImageRender;

public class RangeSelectionRenderer
{
    public RangeSelectionRenderer()
    {
    }

    public void Render(SKCanvas g, RenderInfoCommon renderInfo)
    {
        var selectionRange = renderInfo.SelectionRange;
        if (selectionRange == null)
            return;

        var renderLayout = renderInfo.ScreenLayout;

        (int beginTime, int endTime, _, _) = renderInfo.RenderRange;
        (int selectionBeginTime, int selectionEndTime) = selectionRange.GetOrdererRange();

        if (beginTime <= selectionEndTime && selectionBeginTime < endTime)
        {
            var area = selectionRange.IsScoreArea ? renderLayout.ScoreArea
                : renderLayout.HasDynamicsArea ? renderLayout.DynamicsArea
                : null;

            if (area != null)
            {
                int offset = area.X;
                int x = offset + renderLayout.GetRenderPosXFromTime(selectionBeginTime - beginTime);
                int width = renderLayout.GetRenderPosXFromTime(selectionEndTime - selectionBeginTime);

                g.DrawRect(x, area.Y, width, area.Height, new SKPaint
                {
                    Color = SKColors.OrangeRed.WithAlpha(30),
                    BlendMode = SKBlendMode.SrcOver,
                });
            }
        }
    }
}
