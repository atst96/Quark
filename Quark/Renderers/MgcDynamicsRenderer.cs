using System.Numerics;
using Avalonia.Media;
using Quark.ImageRender;
using Quark.Projects.Tracks;

namespace Quark.Renderers;

internal class MgcDynamicsRenderer<TPhrase, TNumber> : DynamicsRenderer
    where TPhrase : IMgcDynamicsPhrase<TNumber>
    where TNumber : IFloatingPointIeee754<TNumber>
{
    private IMgcDynamicsPhraseTrack<TPhrase, TNumber> _track;

    public MgcDynamicsRenderer(IMgcDynamicsPhraseTrack<TPhrase, TNumber> track)
    {
        this._track = track;
    }

    public override void Render(DrawingContext drawingContext, RenderInfoCommon renderInfo)
    {
        // TODO
        // DynamicsRendererV1から移植
    }
}
