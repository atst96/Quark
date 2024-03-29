﻿using Quark.Constants;
using Quark.Drawing;
using Quark.Models.Scores;

namespace Quark.ImageRender;

public class RangeScoreRenderInfo
{
    public required ScoreInfo Score { get; init; }

    public required IList<TimingHandle> Timings { get; init; }

    public required IList<VerticalLineInfo> NoteLines { get; init; }

    public required IList<VerticalLineInfo> RulerLines { get; init; }
}
