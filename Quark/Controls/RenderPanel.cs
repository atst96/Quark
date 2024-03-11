using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;

namespace Quark.Controls;
internal class RenderPanel : Control
{
    public event EventHandler<SKCanvas>? Rendering;

    private class CustomOp : ICustomDrawOperation, IDisposable
    {
        private readonly RenderPanel _panel;
        private readonly Rect _rect;

        public CustomOp(RenderPanel renderPanel, Rect rect)
        {
            this._panel = renderPanel;
            this._rect = rect;
        }

        public Rect Bounds
            => this._rect;

        public void Dispose() { }

        public bool Equals(ICustomDrawOperation? other)
            => object.ReferenceEquals(this, other);

        public bool HitTest(Point p)
            => true;

        public void Render(ImmediateDrawingContext context)
        {
            var feature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            using var lease = feature!.Lease();

            var canvas = lease.SkCanvas;

            this._panel.Rendering?.Invoke(this, canvas);
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = this.Bounds;

        context.Custom(new CustomOp(this, new Rect(bounds.Size)));
    }
}
