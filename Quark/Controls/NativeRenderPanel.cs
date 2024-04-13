using System;
using Avalonia.Controls;
using Avalonia.Media;

namespace Quark.Controls;

public class NativeRenderPanel : Decorator
{
    public event EventHandler<DrawingContext>? Rendering;

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        this.Rendering?.Invoke(this, context);
    }
}
