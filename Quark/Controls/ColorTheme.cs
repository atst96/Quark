using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Media;

namespace Quark.Controls;

public class ColorTheme : AvaloniaObject
{
    /// <summary>白鍵の背景色</summary>
    public SolidColorBrush WhiteKeyBackgroundBrush { get; } = BrushFromRgb(250, 250, 250);

    /// <summary>白鍵の枠線色</summary>
    public Pen WhiteKeyBorderPen { get; } = new(BrushFromRgb(80, 80, 80), 1);

    /// <summary>黒鍵の色</summary>
    public SolidColorBrush BlackKeyBackgroundBrush { get; } = BrushFromRgb(50, 50, 50);

    /// <summary>黒鍵行の色</summary>
    public SolidColorBrush ScoreWhiteKeyBackgroundBrush { get; } = BrushFromRgb(255, 255, 255);

    /// <summary>黒鍵行の色</summary>
    public SolidColorBrush ScoreBlackKeyBackgroundBrush { get; } = BrushFromRgb(230, 230, 230);

    /// <summary>行の境界色</summary>
    public Pen ScoreKeyBorderPen { get; } = new Pen(BrushFromRgb(230, 230, 230), 1);

    /// <summary>ライトテーマ</summary>
    public static ColorTheme Light { get; } = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SolidColorBrush BrushFromRgb(byte r, byte g, byte b)
        => new(Color.FromRgb(r, g, b));
}
