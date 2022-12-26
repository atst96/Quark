using Avalonia;
using Avalonia.Controls.Primitives;

namespace Quark.Views.Components;

public class LabelledControl : HeaderedContentControl
{
    /// <summary>
    /// Defines the <see cref="LabelWidth"/> property.
    /// </summary>
    public static readonly StyledProperty<double> LabelWidthProperty =
        AvaloniaProperty.Register<LabelledControl, double>(nameof(LabelWidth), double.NaN);

    public double LabelWidth
    {
        get => this.GetValue(LabelWidthProperty);
        set => this.SetValue(LabelWidthProperty, value);
    }
}
