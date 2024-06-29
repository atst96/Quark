using Avalonia.Media;
using Quark.ImageRender;

namespace Quark.Renderers;

public interface IVisualRenderer
{
    /// <summary>編集前の値描画フラグ</summary>
    public bool IsDrawOriginal { get; set; }

    /// <summary>
    /// 編集値を描画する
    /// </summary>
    /// <param name="drawingContext">描画コンテキスト</param>
    /// <param name="renderInfo"></param>
    public void Render(DrawingContext drawingContext, RenderInfoCommon renderInfo);
}
