using Quark.Controls;
using System.Runtime.CompilerServices;
using SkiaSharp;

namespace Quark.ImageRender;

public class ScoreImageRender
{
    /// <summary>白鍵の描画色</summary>
    public SKPaint WhiteKeyPaint { get; } = new() { Color = new SKColor(255, 255, 255) };

    /// <summary>黒鍵の描画色</summary>
    public SKPaint BlackKeyPaint { get; } = new() { Color = new SKColor(230, 230, 230) };

    /// <summary>白鍵・黒鍵の描画色</summary>
    public SKPaint WhiteKeyGridPaint { get; } = new() { Color = new SKColor(230, 230, 230), StrokeWidth = 1 };


    // TODO: 本対応でprivateにする
    /// <summary>
    /// 12音階の画像を生成する
    /// </summary>
    /// <param name="width">画像幅</param>
    /// <param name="keyHeight">1音あたりの高さ</param>
    /// <param name="scaling">スケーリング情報</param>
    /// <returns></returns>
    internal (SKBitmap bmp, int width, int height) CreatePianoOctaveBmp(int width, int keyHeight, RenderScaleInfo scaling)
    {
        const int keys = 12;

        int height = keyHeight * keys;
        int renderHeight = scaling.ToDisplayScaling(height);
        int renderWidth = scaling.ToDisplayScaling(width);
        int renderKeyHeight = scaling.ToDisplayScaling(keyHeight);

        var whiteKeyBrush = this.WhiteKeyPaint;
        var whiteGridPen = this.WhiteKeyGridPaint;
        var blackKeyBrush = this.BlackKeyPaint;

        var image = new SKBitmap(renderWidth, renderHeight, isOpaque: true);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int GetYPos(int key) => renderHeight - ((key + 1) * renderKeyHeight);

        using (var g = new SKCanvas(image))
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void DrawRect(int key, SKPaint brush)
            {
                int y = GetYPos(key);
                g.DrawRect(new(0, y, renderWidth, renderKeyHeight + y), brush);
            }

            // ストライプの描画
            DrawRect(0, whiteKeyBrush); // C
            DrawRect(1, blackKeyBrush); // C#
            DrawRect(2, whiteKeyBrush); // D
            DrawRect(3, blackKeyBrush); // D#
            DrawRect(4, whiteKeyBrush); // E
            DrawRect(5, whiteKeyBrush); // F
            DrawRect(6, blackKeyBrush); // F#
            DrawRect(7, whiteKeyBrush); // G
            DrawRect(8, blackKeyBrush); // G#
            DrawRect(9, whiteKeyBrush); // A
            DrawRect(10, blackKeyBrush); // A#
            DrawRect(11, whiteKeyBrush); // B

            // 白鍵の境界を描画
            g.DrawLine(0, GetYPos(4), renderWidth, GetYPos(4), whiteGridPen);
            g.DrawLine(0, GetYPos(11), renderWidth, GetYPos(11), whiteGridPen);
        }

        return (image, width, height);
    }

    // TODO: 本対応でprivateにする
    internal static SKPath CreateBreathMark(float top, float left, float width, float height)
    {
        float halfWidth = (width - 1) / 2;

        SKPoint[] points =
        {
            // 左上
            new(left - halfWidth, top),
            // 下
            new(left, top + height),
            // 右上
            new(left + halfWidth, top),
        };

        var path = new SKPath();
        path.AddPoly(points, close: false);

        return path;
    }
}
