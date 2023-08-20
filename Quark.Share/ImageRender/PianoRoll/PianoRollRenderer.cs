using Quark.ImageRender.Score;
using SkiaSharp;

namespace Quark.ImageRender.PianoRoll;

internal class PianoRollRenderer
{
    private readonly RenderInfoCommon _renderInfo;

    private PianoRollBackgroundRenderer _backgroundRenderer;

    private PianoRollNoteRenderer _noteRenderer;

    private PitchRendererV2 _pitchRenderer;

    public PianoRollRenderer(RenderInfoCommon renderInfo)
    {
        this._renderInfo = renderInfo;

        this._backgroundRenderer = new(renderInfo);
        this._noteRenderer = new(renderInfo);
        this._pitchRenderer = new(renderInfo);
    }

    public SKBitmap CreateImage()
    {
        var ri = this._renderInfo;
        var renderLayout = this._renderInfo.ScreenLayout;

        var imageSize = renderLayout.ScoreImage;
        var image = new SKBitmap(imageSize.Width, imageSize.Height, isOpaque: true);

        using (var g = new SKCanvas(image))
        {
            // 背景の描画
            this._backgroundRenderer.Render(g);

            var rangeScoreInfo = ri.RangeScoreRenderInfo;
            if (rangeScoreInfo != null)
            {
                // ノートと歌詞の描画
                this._noteRenderer.Render(g);

                // 音程の描画
                this._pitchRenderer.Render(g);
            }
        }

        return image;
    }
}
