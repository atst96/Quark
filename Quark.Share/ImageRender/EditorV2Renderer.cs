using Quark.ImageRender.Score;
using SkiaSharp;

namespace Quark.ImageRender;

public class EditorV2Renderer : EditorRendererBase
{
    private readonly PitchRendererV2 _pitchRenderer = new();
    private readonly DynamicsRendererV2 _dynamicsRenderer = new();

    public EditorV2Renderer(EditorPartsLayoutResolver partsLayout) : base(partsLayout)
    {
    }

    protected override void RenderPitchToScoreImage(SKCanvas g, RenderInfoCommon ri)
        => this._pitchRenderer.Render(g, ri);

    protected override void RenderDynamicsToDynamicsArea(SKCanvas g, RenderInfoCommon ri)
        => this._dynamicsRenderer.Render(g, ri);
}
