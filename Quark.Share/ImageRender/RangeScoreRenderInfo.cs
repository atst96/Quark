using Quark.Drawing;
using Quark.Models.Scores;

namespace Quark.ImageRender;

public class RangeScoreRenderInfo
{
    public required PartScore Score { get; init; }

    public required IList<VerticalLineInfo> NoteLines { get; init; }

    public required IList<VerticalLineInfo> RulerLines { get; init; }
}
