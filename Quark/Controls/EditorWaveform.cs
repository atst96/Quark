using Avalonia.Controls;
using Avalonia.Media;
using Quark.ImageRender;
using Quark.Projects.Tracks;
using Quark.Renderers;

namespace Quark.Controls;

internal class EditorWaveform : Control
{
    private INeutrinoTrack? _track;
    private IVisualRenderer? _renderer;
    private RenderInfoCommon? _renderInfo;

    public void OnTrackChanged(INeutrinoTrack? track)
    {
        if (track != null)
        {

            this._track = track;
            this._renderer = PitchRenderer.Create(track);
        }
        else
        {
            this._track = null;
            this._renderer = null;
            this._renderInfo = null;
        }

        this.Redraw();
    }

    /// <summary>
    /// エディタのレイアウト変更時
    /// </summary>
    /// <param name="renderInfo">描画情報</param>
    public void OnParentEditorLayoutUpdated(RenderInfoCommon renderInfo)
    {
        this._renderInfo = renderInfo;
        this.Redraw();
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var renderer = this._renderer;
        var renderInfo = this._renderInfo;

        if (renderer != null && renderInfo != null)
        {
            renderer.Render(context, renderInfo);
        }
    }

    /// <summary>
    /// 再描画する
    /// </summary>
    private void Redraw()
        => this.InvalidateVisual();
}
