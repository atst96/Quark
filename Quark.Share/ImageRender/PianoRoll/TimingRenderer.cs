using SkiaSharp;

namespace Quark.ImageRender.PianoRoll;

public class TimingRenderer
{
    // TODO: 変数で変更できるようにする
    /// <summary>フォント</summary>
    private static SKFont _font = new(SKTypeface.FromFamilyName("Segoe UI"), 18);

    // TODO: 変数で変更できるようにする
    /// <summary>非選択時の色</summary>
    private static SKColor _foregroundColor = SKColors.DarkBlue;

    // TODO: 変数で変更できるようにする
    /// <summary>選択時の色</summary>
    private static SKColor _selectedColor = SKColors.Red;

    /// <summary>描画情報</summary>
    private readonly RenderInfoCommon _renderInfo;

    /// <summary>非選択時の縦線</summary>
    private SKPaint _foregroundLineBrush = new() { Color = _foregroundColor };

    /// <summary非選択時の文字</summary>
    private SKPaint _foregroundTextBrush = new(_font)
    {
        Color = _foregroundColor,
        SubpixelText = true,
        IsAntialias = true,
    };

    /// <summary>選択時の縦線</summary>
    private SKPaint _selectedLineBrush = new() { Color = _selectedColor };

    /// <summary>選択時の文字</summary>
    private SKPaint _selectedTextBrush = new(_font)
    {
        Color = _selectedColor,
        SubpixelText = true,
        IsAntialias = true,
    };

    /// <summary>フォントの高さのキャッシュ</summary>
    private int? _textHeight;


    public TimingRenderer(RenderInfoCommon renderInfo)
    {
        this._renderInfo = renderInfo;
    }

    /// <summary>
    /// フォントの高さを取得する
    /// </summary>
    /// <returns></returns>
    public int GetTextHeight()
        => this._textHeight ??= ((int)Math.Ceiling(this._foregroundTextBrush.FontMetrics.CapHeight) + 4);

    /// <summary>文字列描画時の幅を取得</summary>
    public int MeasureTextWidth(string content)
        => (int)Math.Ceiling(this._foregroundTextBrush.MeasureText(content));

    public void Render(SKCanvas g)
    {
        var ri = this._renderInfo;

        var rangeScoreInfo = ri.RangeScoreRenderInfo;
        if (rangeScoreInfo == null)
            return;

        var renderLayout = ri.ScreenLayout;
        var handles = rangeScoreInfo.Timings;

        if (handles == null)
            return;

        int y = renderLayout.ScoreArea.Y;
        int lineEndY = y + renderLayout.ScoreArea.Height;

        foreach (var handle in handles)
        {
            float x = handle.X;

            // 描画情報を取得
            var (paint, textPaint) = handle.IsSelected
                ? (this._selectedLineBrush, this._selectedTextBrush)
                : (this._foregroundLineBrush, this._foregroundTextBrush);

            // 縦線を描画
            g.DrawLine(x, y, x, lineEndY, paint);

            // 横線を描画
            g.DrawText(handle.TimingInfo.Phoneme, handle.PhonemeLocation, textPaint);
        }
    }
}
