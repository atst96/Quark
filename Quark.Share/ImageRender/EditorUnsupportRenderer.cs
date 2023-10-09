using SkiaSharp;

namespace Quark.ImageRender;

public class EditorUnsupportRenderer : EditorRendererBase
{
    public EditorUnsupportRenderer(EditorPartsLayoutResolver partsLayout) : base(partsLayout)
    {
    }

    protected override void RenderPitchToScoreImage(SKCanvas g, RenderInfoCommon ri)
    {
        // 処理しない
    }

    protected override void RenderDynamicsToDynamicsArea(SKCanvas g, RenderInfoCommon ri)
    {
        // 処理しない
    }
}
