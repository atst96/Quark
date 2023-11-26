using Quark.ImageRender.Parts;
using Quark.ImageRender.PianoRoll;
using Quark.ImageRender.Score;
using Quark.Utils;
using SkiaSharp;

namespace Quark.ImageRender;

public abstract class EditorRendererBase
{
    protected RenderInfoCommon? PreviousRenderInfo { get; private set; }

    private SKBitmap? _renderImage;
    private SKBitmap? _rulerImage;
    private SKBitmap? _dynamicImage;

    private readonly EditorPartsLayoutResolver _partsLayout;
    private readonly PianoRollBackgroundRenderer _backgroundRenderer;
    private readonly PianoRollKeysRenderer _keysRenderer;
    private readonly PianoRollNoteRenderer _noteRenderer;
    private readonly RulerRenderer _rulerRenderer;
    private readonly TimingRenderer _timingRenderer;
    private readonly RangeSelectionRenderer _selectingRenderer;

    public EditorRendererBase(EditorPartsLayoutResolver partsLayout)
    {
        this._partsLayout = partsLayout;
        this._backgroundRenderer = new();
        this._keysRenderer = new();
        this._noteRenderer = new();
        this._rulerRenderer = new();
        this._timingRenderer = new(partsLayout);
        this._selectingRenderer = new();
    }

    // TODO: 廃止する
    public TimingRenderer GetTimingRenderer()
        => this._timingRenderer;

    public void Render(SKCanvas g, RenderInfoCommon ri)
    {
        if (true)
        {
            DisposableUtil.ExchangeDisposable(ref this._renderImage, CreateImage(ri));
            DisposableUtil.ExchangeDisposable(ref this._rulerImage, this._rulerRenderer.CreateImage(ri));
            DisposableUtil.ExchangeDisposable(ref this._dynamicImage, this.CreateDynamicsImage(ri));
        }

        var renderLayout = ri.ScreenLayout;
        if (renderLayout is null)
            return;

        var rulerRect = renderLayout.RulerArea.ToSKRect();
        var scoreArea = renderLayout.ScoreArea;

        // 描画領域の更新

        // 鍵盤を描画
        this._keysRenderer.Render(g, ri);

        // スクロール位置から描画位置(y)を計算
        int scaledScoreY = ri.VScrollPosition;
        g.DrawBitmap(this._rulerImage, SKRect.Create(rulerRect.Size), rulerRect);
        g.DrawBitmap(this._renderImage,
            SKRect.Create(0, scaledScoreY, scoreArea.Width, scoreArea.Height),
            scoreArea.ToSKRect());

        this._timingRenderer.Render(g, ri);

        if (renderLayout.HasDynamicsArea)
        {
            var area = renderLayout.DynamicsArea;
            g.DrawBitmap(this._dynamicImage, area.X, area.Y);
        }

        this._selectingRenderer.Render(g, ri);

        int remainingHeight = renderLayout.EditArea.Height - renderLayout.ScoreImage.Height;
        if (remainingHeight > 0)
        {
            var editArea = renderLayout.EditArea;
            g.DrawRect(SKRect.Create(editArea.X, editArea.Y + editArea.Height - remainingHeight, editArea.Width, remainingHeight), new SKPaint() { Color = SKColors.White });
        }

        this.PreviousRenderInfo = ri;
    }

    private SKBitmap CreateImage(RenderInfoCommon ri)
    {
        var renderLayout = ri.ScreenLayout;

        var imageSize = renderLayout.ScoreImage;
        var image = new SKBitmap(imageSize.Width, imageSize.Height, isOpaque: true);

        using (var g = new SKCanvas(image))
        {
            // 背景の描画
            this._backgroundRenderer.Render(g, ri);

            var rangeScoreInfo = ri.RangeScoreRenderInfo;
            if (rangeScoreInfo != null)
            {
                // ノートと歌詞の描画
                this._noteRenderer.Render(g, ri);

                // 音程の描画
                this.RenderPitchToScoreImage(g, ri);
            }
        }

        return image;
    }

    private SKBitmap CreateDynamicsImage(RenderInfoCommon ri)
    {
        var renderLayout = ri.ScreenLayout;

        if (!renderLayout.HasDynamicsArea)
            return new(1, 1, SKColorType.Rgb888x, SKAlphaType.Unknown);

        var imageSize = renderLayout.DynamicsArea;
        var image = new SKBitmap(imageSize.Width, imageSize.Height, isOpaque: true);

        using (var g = new SKCanvas(image))
        {
            this.RenderDynamicsToDynamicsArea(g, ri);
        }

        return image;
    }

    protected abstract void RenderPitchToScoreImage(SKCanvas g, RenderInfoCommon ri);

    protected abstract void RenderDynamicsToDynamicsArea(SKCanvas g, RenderInfoCommon ri);
}
