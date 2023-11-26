using SkiaSharp;

namespace Quark.ImageRender;

/// <summary>
/// 色情報
/// </summary>
public class ColorInfo
{
    /// <summary>白鍵の背景色</summary>
    public SKPaint WhiteKeyBackgroundBrush { get; } = new()
    {
        Color = new SKColor(250, 250, 250)
    };

    /// <summary>白鍵の枠線色</summary>
    public SKPaint WhiteKeyBorderBrush { get; } = new()
    {
        Color = new SKColor(80, 80, 80),
        StrokeWidth = 1,
        IsStroke = true
    };

    /// <summary>黒鍵の色</summary>
    public SKPaint BlackKeyBackgroundBrush { get; } = new()
    {
        Color = new SKColor(50, 50, 50)
    };

    /// <summary>黒鍵行の色</summary>
    public SKPaint WhiteKeyPaint { get; } = new SKPaint { Color = new SKColor(255, 255, 255) };

    /// <summary>黒鍵行の色</summary>
    public SKPaint BlackKeyPaint { get; } = new SKPaint { Color = new SKColor(230, 230, 230) };

    /// <summary>行の境界色</summary>
    public SKPaint WhiteKeyGridPaint { get; } = new SKPaint { Color = new SKColor(230, 230, 230), StrokeWidth = 1 };
}
