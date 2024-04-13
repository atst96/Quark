using Quark.Drawing;
using Quark.Projects.Tracks;

namespace Quark.Controls;

internal class TrackRenderInfo
{
    /// <summary>トラック</summary>
    public required INeutrinoTrack Track { get; init; }

    /// <summary>描画範囲情報</summary>
    public required RenderRangeInfo RenderRange { get; init; }

    /// <summary>レイアウト情報</summary>
    public required EditorRenderLayout ScreenLayout { get; init; }
}
