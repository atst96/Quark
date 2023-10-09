using SkiaSharp;

namespace Quark.ImageRender.PianoRoll;

public class TimingRenderer
{
    private EditorPartsLayoutResolver _partLayout;

    // TODO: 変数で変更できるようにする
    /// <summary>非選択時の色</summary>
    private static SKColor _foregroundColor = SKColors.DarkBlue;

    // TODO: 変数で変更できるようにする
    /// <summary>選択時の色</summary>
    private static SKColor _selectedColor = SKColors.Red;

    /// <summary>非選択時の縦線</summary>
    private SKPaint _foregroundLineBrush = null!;

    /// <summary非選択時の文字</summary>
    private SKPaint _foregroundTextBrush = null!;

    /// <summary>選択時の縦線</summary>
    private SKPaint _selectedLineBrush = null!;

    /// <summary>選択時の文字</summary>
    private SKPaint _selectedTextBrush = null!;

    /// <summary>フォントの高さのキャッシュ</summary>
    private int? _textHeight;

    public TimingRenderer(EditorPartsLayoutResolver partsLayout)
    {
        this._partLayout = partsLayout;
        this.OnRenderParamterUpdated();
    }

    private void OnRenderParamterUpdated()
    {
        var font = this._partLayout.TimingLabelFont;

        this._foregroundLineBrush = LineBrush(_foregroundColor);
        this._foregroundTextBrush = FontToBrush(font, _foregroundColor);

        this._selectedLineBrush = LineBrush(_selectedColor);
        this._selectedTextBrush = FontToBrush(font, _selectedColor);
    }

    private static SKPaint LineBrush(SKColor color)
        => new() { Color = color };

    private static SKPaint FontToBrush(SKFont font, SKColor color)
        => new(font) { Color = color, SubpixelText = true, IsAntialias = true };

    public void Render(SKCanvas g, RenderInfoCommon ri)
    {
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
