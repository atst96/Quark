using Quark.ImageRender.Score;
using SkiaSharp;

namespace Quark.ImageRender;

public class EditorV1Renderer : EditorRendererBase
{
    private readonly PitchRendererV1 _pitchRenderer = new();
    private readonly DynamicsRendererV1 _dynamicsRenderer = new();

    public EditorV1Renderer(EditorPartsLayoutResolver partsLayout) : base(partsLayout)
    {
    }

    protected override void RenderPitchToScoreImage(SKCanvas g, RenderInfoCommon ri)
        => this._pitchRenderer.Render(g, ri);

    protected override void RenderDynamicsToDynamicsArea(SKCanvas g, RenderInfoCommon ri)
        => this._dynamicsRenderer.Render(g, ri);
}
