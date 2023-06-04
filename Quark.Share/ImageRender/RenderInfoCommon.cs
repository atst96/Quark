using Quark.Controls;
using Quark.Drawing;
using Quark.Projects.Tracks;

namespace Quark.ImageRender;

internal class RenderInfoCommon
{
    public required INeutrinoTrack? Track { get; init; }

    public required RenderRangeInfo RenderRange { get; init; }

    public required ViewDrawingBoxInfo PartRenderInfo { get; init; }

    public required ColorInfo ColorInfo { get; init; }

    public RangeScoreRenderInfo? RangeScoreRenderInfo { get; set; }
}
