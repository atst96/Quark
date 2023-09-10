using Quark.Controls;
using Quark.Controls.Editing;
using Quark.Drawing;
using Quark.Projects.Tracks;

namespace Quark.ImageRender;

public class RenderInfoCommon
{
    public required INeutrinoTrack? Track { get; init; }

    public required RenderRangeInfo RenderRange { get; init; }

    public required EditorRenderLayout ScreenLayout { get; init; }

    public required ColorInfo ColorInfo { get; init; }

    public required RangeSelectingInfo? SelectionRange { get; set; }

    public RangeScoreRenderInfo? RangeScoreRenderInfo { get; set; }
}
