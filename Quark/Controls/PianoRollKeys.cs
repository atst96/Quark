using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Quark.Controls;

public class PianoRollKeys : Control
{
    /// <summary>オクターブ当たりの全キー数</summary>
    private const int KeysPerOctave = 12;

    /// <summary>1オクターブあたりの白鍵数</summary>
    private const int WhiteKeysPerOctave = 7;

    /// <summary>縦のスクロール位置</summary>
    private int _vScrollPosition = 0;

    /// <summary>カラーテーマ</summary>
    public ColorTheme ColorTheme
    {
        get => this.GetValue<ColorTheme>(ColorThemeProperty);
        set => this.SetValue(ColorThemeProperty, value);
    }

    /// <summary>描画時のフォントサイズ</summary>
    private static readonly int _fontSize = 11;

    /// <summary>描画時のフォント</summary>
    private static readonly Typeface _typeface = new(new FontFamily("Segoe UI"));

    /// <summary><see cref="ColorTheme"/>のプロパティ</summary>
    public static readonly AvaloniaProperty<ColorTheme> ColorThemeProperty = AvaloniaProperty.Register<PlotEditor, ColorTheme>(nameof(ColorTheme), ColorTheme.Light);

    /// <summary>描画するキー数</summary>
    public int KeyCount { get; } = 88;

    /// <summary>キーの高さ</summary>
    public int KeyHeight
    {
        get => this.GetValue(KeyHeightProperty);
        set => this.SetValue(KeyHeightProperty, value);
    }

    /// <summary><see cref="KeyHeight"/>のプロパティ</summary>
    public static readonly StyledProperty<int> KeyHeightProperty = AvaloniaProperty.Register<PianoRollKeys, int>(nameof(KeyHeight), defaultValue: 7);

    /// <summary>
    /// プロパティ変更時
    /// </summary>
    /// <param name="change"></param>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        var property = change.Property;
        if (property == KeyHeightProperty)
        {
            this.InvalidateVisual();
        }
    }

    /// <summary>スクロール時</summary>
    public void OnScroll(int position)
    {
        this._vScrollPosition = position;
        this.InvalidateVisual();

    }

    /// <summary>白鍵の相対位置</summary>
    private static readonly ImmutableDictionary<int, int> whiteKeyPosition = new Dictionary<int, int>
    {
        [0] = 0, // C
        [2] = 1, // D
        [4] = 2, // E
        [5] = 3, // F
        [7] = 4, // G
        [9] = 5, // A
        [11] = 6, // B
    }
    .ToImmutableDictionary();

    /// <summary>黒鍵の相対位置</summary>
    private static readonly ImmutableDictionary<int, double> blackKeyPosition = new Dictionary<int, double>
    {
        [1] = 1.4, // C#
        [3] = 2.65, // D#
        [6] = 4.4, // F#
        [8] = 5.5, // G#
        [10] = 6.65, // A#
    }
    .ToImmutableDictionary();

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var colorTheme = this.ColorTheme;

        var bound = this.Bounds;

        double offsetY = -this._vScrollPosition;

        int keyHeight = this.KeyHeight;
        int keyCount = this.KeyCount;
        int areaHeight = keyHeight * keyCount;

        var whiteKeyBackgroundBrush = colorTheme.WhiteKeyBackgroundBrush;
        var whiteKeyBorderPen = colorTheme.WhiteKeyBorderPen;
        var blackKeyBackgroundBrush = colorTheme.BlackKeyBackgroundBrush;

        context.PushRenderOptions(new RenderOptions()
        {
            TextRenderingMode = TextRenderingMode.Antialias,
            EdgeMode = EdgeMode.Aliased,
        });

        context.FillRectangle(whiteKeyBackgroundBrush, new Rect(bound.Size));

        double w = bound.Width;
        context.DrawLine(whiteKeyBorderPen, new(w, 0), new(w, bound.Height));

        double renderKeyHeight = keyHeight * KeysPerOctave / WhiteKeysPerOctave;

        // 描画できるキー数
        int renderKeyCount = (int)Math.Ceiling((double)areaHeight / keyHeight);
        int baseKeyIdx = 0;
        if (areaHeight < bound.Height)
            baseKeyIdx = (int)Math.Ceiling((bound.Height - areaHeight - offsetY) / keyHeight);
        int maxKeyIdx = baseKeyIdx + renderKeyCount;

        // 白鍵の描画
        for (int keyIdx = baseKeyIdx; keyIdx < maxKeyIdx; ++keyIdx)
        {
            int keyCode = keyIdx % KeysPerOctave;
            if (!whiteKeyPosition.TryGetValue(keyCode, out int renderKeyIdx))
                continue;

            int octave = keyIdx / KeysPerOctave;

            double underLineY = Math.Round(areaHeight - ((octave * keyHeight * KeysPerOctave) + (renderKeyIdx * renderKeyHeight)) + offsetY);
            context.DrawLine(whiteKeyBorderPen, new(0, underLineY), new(w, underLineY));

            if (keyCode == 0)
            {
                // Cの場合は音番号を描画
                int hPadding = 4;

                var text = this.GetFormattedText($"C{octave + 1}");
                context.DrawText(text, new Point(w - text.Width - hPadding, underLineY - renderKeyHeight + (renderKeyHeight / 2) - (text.Height / 2)));
            }
        }

        // 黒鍵の描画
        for (int keyIdx = baseKeyIdx; keyIdx < maxKeyIdx; ++keyIdx)
        {
            int keyCode = keyIdx % KeysPerOctave;
            if (!blackKeyPosition.TryGetValue(keyCode, out double renderKeyIdx))
                continue;

            int octave = keyIdx / KeysPerOctave;

            double w2 = w * 0.6d;

            double renderKeyHeight2 = renderKeyHeight * .6d;
            double padding = (renderKeyHeight - renderKeyHeight2) / 2d;

            double underLineY = Math.Round(areaHeight - ((octave * keyHeight * KeysPerOctave) + (((renderKeyIdx) * renderKeyHeight) - padding)) + offsetY);

            context.FillRectangle(blackKeyBackgroundBrush, new(0, underLineY, w2, renderKeyHeight2));
        }
    }

    private FormattedText GetFormattedText(string text)
        => new(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, _typeface, _fontSize, this.ColorTheme.WhiteKeyBorderPen.Brush);
}
