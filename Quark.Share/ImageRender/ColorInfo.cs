using SkiaSharp;

namespace Quark.ImageRender;

/// <summary>
/// 色情報
/// </summary>
public class ColorInfo
{
    /// <summary>黒鍵行の色</summary>
    public SKPaint WhiteKeyPaint { get; } = new SKPaint { Color = new SKColor(255, 255, 255) };

    /// <summary>黒鍵行の色</summary>
    public SKPaint BlackKeyPaint { get; } = new SKPaint { Color = new SKColor(230, 230, 230) };

    /// <summary>行の境界色</summary>
    public SKPaint WhiteKeyGridPaint { get; } = new SKPaint { Color = new SKColor(230, 230, 230), StrokeWidth = 1 };
}
