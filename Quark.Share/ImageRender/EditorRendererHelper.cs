using Quark.Projects.Tracks;

namespace Quark.ImageRender;

public static class EditorRendererHelper
{
    public static EditorRendererBase GetRenderer(INeutrinoTrack? track, EditorPartsLayoutResolver partsLayout)
    {
        return track switch
        {
            NeutrinoV1Track => new EditorV1Renderer(partsLayout),
            NeutrinoV2Track => new EditorV2Renderer(partsLayout),
            _ => new EditorUnsupportRenderer(partsLayout),
        };
    }
}
