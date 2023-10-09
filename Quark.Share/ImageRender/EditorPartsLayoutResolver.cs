using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace Quark.ImageRender;

public class EditorPartsLayoutResolver
{
    /// <summary>フォントの高さのキャッシュ</summary>
    private int? _textHeight;
    private SKFontMetrics _timingLabelFontMatrix;

    public SKFont TimingLabelFont { get; private set; } = null!;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Is<T>(ref T left, ref T right)
    {
        bool @is = EqualityComparer<T>.Default.Equals(left, right);
        if (@is) left = right;

        return @is;
    }

    public EditorPartsLayoutResolver()
    {
        this.UpdateTimingLabelFont(new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 18));
    }

    public void UpdateTimingLabelFont(SKFont font)
    {
        this.TimingLabelFont = font;
        this._textHeight = null;
        font.GetFontMetrics(out this._timingLabelFontMatrix);
    }

    /// <summary>
    /// フォントの高さを取得する
    /// </summary>
    /// <returns></returns>
    public int GetTextHeight()
        => this._textHeight ??= ((int)Math.Ceiling(this._timingLabelFontMatrix.CapHeight) + 4);

    /// <summary>文字列描画時の幅を取得</summary>
    public int MeasureTextWidth(string content)
        => (int)Math.Ceiling(this.TimingLabelFont.MeasureText(MemoryMarshal.Cast<char, ushort>(content)));
}
