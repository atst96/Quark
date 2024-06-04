using Avalonia.Media;
using Quark.ImageRender;

namespace Quark.Renderers;

public interface IPitchRenderer
{
    /// <summary>
    /// ピッチを描画する
    /// </summary>
    /// <param name="drawingContext">描画コンテキスト</param>
    /// <param name="renderInfo"></param>
    public void Render(DrawingContext drawingContext, RenderInfoCommon renderInfo);
}
