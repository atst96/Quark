using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Quark.Drawing;

namespace Quark.Controls;

public class PianoRollRuler : Control
{
    /// <summary>カラーテーマ</summary>
    public ColorTheme ColorTheme
    {
        get => this.GetValue<ColorTheme>(ColorThemeProeprty);
        set => this.SetValue(ColorThemeProeprty, value);
    }

    /// <summary><see cref="ColorTheme"/>のプロパティ</summary>
    private static readonly AvaloniaProperty<ColorTheme> ColorThemeProeprty = AvaloniaProperty.Register<PianoRollRuler, ColorTheme>(nameof(ColorTheme), defaultValue: ColorTheme.Light);

    public EditorRenderLayout? LayoutInfo
    {
        get => this.GetValue<EditorRenderLayout?>(LayoutInfoProperty);
        set => this.SetValue(LayoutInfoProperty, value);
    }

    private static readonly AvaloniaProperty<EditorRenderLayout?> LayoutInfoProperty = AvaloniaProperty.Register<PianoRollRuler, EditorRenderLayout?>(nameof(LayoutInfo), null);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        var property = change.Property;
        if (property == LayoutInfoProperty || property == ColorThemeProeprty)
        {
            this.InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var backgroundBrush = Brushes.Black;

        var renderLayout = this.LayoutInfo;
        if (renderLayout == null)
        {
            // 描画情報を受信していない段階で描画要求された場合は背景のみ描画
            var bounds = this.Bounds;
            if (bounds != default)
                context.FillRectangle(backgroundBrush, bounds);

            return;
        }

        context.PushRenderOptions(new RenderOptions
        {
            EdgeMode = EdgeMode.Aliased,
            TextRenderingMode = TextRenderingMode.Alias,
        });

        // 描画領域
        //(int renderWidth, int renderHeight) = renderLayout.RulerArea.Size;

        //var rangeScoreInfo = ri.RangeScoreRenderInfo;
        //var renderRange = ri.RenderRange;

        //var pen = new Pen(Brushes.White, 1);
        //context.FillRectangle(backgroundBrush, new Rect(0, 0, renderWidth, renderHeight));

        //if (rangeScoreInfo != null)
        //{
        //    // 描画開始・終了位置
        //    int beginTime = renderRange.BeginTime;

        //    // 小節、4分音符、8分音符時の描画位置
        //    float measureLineY = 0.0f;
        //    float beat4thLineY = renderHeight * 0.4f;
        //    float beat8thLineY = renderHeight * 0.7f;

        //    foreach (var rulerLine in rangeScoreInfo.RulerLines)
        //    {
        //        float scaledX = renderLayout.GetRenderPosXFromTime((int)rulerLine.Time - beginTime);

        //        var linePosY = rulerLine.LineType switch
        //        {
        //            LineType.Measure => measureLineY,
        //            LineType.Whole or LineType.Note2th or LineType.Note4th => beat4thLineY,
        //            _ => beat8thLineY,
        //        };

        //        context.DrawLine(pen, new Point(scaledX, linePosY), new Point(scaledX, renderHeight));
        //    }
        //}
    }
}
