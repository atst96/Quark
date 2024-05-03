using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Quark.Neutrino;

namespace Quark.Controls;

/// <summary>
/// タイミング編集用コントロール
/// </summary>
[PseudoClasses(PseudoClassSelected)]
public class TimingHandle : TemplatedControl
{
    /// <summary>選択状態の疑似クラス</summary>
    private const string PseudoClassSelected = ":selected";

    /// <summary>部品識別子(音素名)</summary>
    private const string PART_Text = "PART_Text";

    /// <summary>内部でキャッシュしておく描画幅の計算値</summary>
    private double? _tempWidth = null;

    private Control? _phonemeElement;

    /// <summary>タイミング情報</summary>
    public PhonemeTiming Timing { get; }

    /// <summary>音素名</summary>
    public string Phoneme
    {
        get => this.GetValue(PhonemeProperty);
        set => this.SetValue(PhonemeProperty, value);
    }

    public static readonly StyledProperty<string> PhonemeProperty = AvaloniaProperty.Register<TimingHandle, string>(nameof(Phoneme), defaultValue: string.Empty);

    /// <summary>縦位置のオフセット</summary>
    public int YOffset
    {
        get => this.GetValue(YOffsetProperty);
        set => this.SetValue(YOffsetProperty, value);
    }

    public static readonly StyledProperty<int> YOffsetProperty = AvaloniaProperty.Register<TimingHandle, int>(nameof(YOffset), defaultValue: 0);

    public bool IsSelected { get; private set; }

    public TimingHandle(PhonemeTiming timing)
    {
        this.Timing = timing;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        var proeprty = change.Property;
        if (proeprty == YOffsetProperty)
        {
            this.ApplyPhonemeLayout();
        }
        else if (proeprty == PhonemeProperty)
        {
            // テキストの幅が変わったので再計算
            this._tempWidth = null;
        }
    }

    private const int BarWidth = 1;
    private const int HorizontalMargin = 4;

    /// <summary>描画幅を計算する。</summary>
    /// <remarks>レイアウト変更がない場合はキャッシュした値を返す</remarks>
    public double RenderWidth
    {
        get
        {
            if (this._tempWidth is { } width)
                return width;

            width = BarWidth + (HorizontalMargin * 2);

            if (this.Phoneme?.Length > 0)
            {
                var measure = new FormattedText(this.Phoneme, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(this.FontFamily), this.FontSize, null);
                width += Math.Ceiling(measure.Width);
                width += 2; // 余白
            }

            this._tempWidth = width;
            return width;
        }
    }

    /// <summary>音素名の描画時の高さ</summary>
    public int PhonemeRenderHeight => 14;

    /// <summary>
    /// テンプレート適用時の処理
    /// </summary>
    /// <param name="e">イベント発火情報</param>
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        this._phonemeElement = e.NameScope.Find<Control>(PART_Text);
        this.ApplyPhonemeLayout();
    }

    /// <summary>
    /// レイアウト更新時処理
    /// </summary>
    private void ApplyPhonemeLayout()
    {
        if (this._phonemeElement is not { } element)
            return;

        element.Margin = new(0, 0, 0, this.YOffset * this.PhonemeRenderHeight);
    }

    /// <summary>
    /// 要素を選択状態にする
    /// </summary>
    public void Select()
    {
        this.IsSelected = true;
        this.PseudoClasses.Add(PseudoClassSelected);
    }

    /// <summary>
    /// 要素の選択状態を解除する
    /// </summary>
    public void Unselect()
    {
        this.IsSelected = false;
        this.PseudoClasses.Remove(PseudoClassSelected);
    }
}
