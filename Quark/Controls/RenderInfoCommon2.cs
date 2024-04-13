using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Quark.ImageRender;

namespace Quark.Controls;

internal class RenderInfoCommon2
{
    /// <summary>カラーテーマ</summary>
    public required ColorTheme ColorTheme { get; init; }

    public required EditorRenderLayout ScreenLayout { get; init; }

    public int VScrollPosition { get; private set; }

    public void OnVScroll(int position)
    {
        this.VScrollPosition = position;
    }
}
