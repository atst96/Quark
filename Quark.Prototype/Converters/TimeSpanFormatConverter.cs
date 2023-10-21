using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Quark.Converters;

/// <summary>
/// TimeSpanのコンバータ
/// TimeSpanを文字列に変換する
/// </summary>
[ValueConversion(typeof(TimeSpan), typeof(string))]
public class TimeSpanFormatConverter : DependencyObject, IValueConverter
{
    /// <summary>フォーマット</summary>
    public string Format
    {
        get => (string)this.GetValue(FormatProperty);
        set => this.SetValue(FormatProperty, value);
    }

    /// <summary><see cref="Format"/>の依存関係プロパティ</summary>
    public static readonly DependencyProperty FormatProperty =
        DependencyProperty.Register(nameof(Format), typeof(string), typeof(TimeSpanFormatConverter),
            new PropertyMetadata(@"hh\:mm\:ss\.fff"));

    /// <summary>
    /// TimeSpan型から文字列に変換する
    /// </summary>
    /// <param name="value"></param>
    /// <param name="targetType"></param>
    /// <param name="parameter"></param>
    /// <param name="culture"></param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value switch
        {
            null => DependencyProperty.UnsetValue,
            TimeSpan timeSpan => timeSpan.ToString(this.Format),
            _ => throw new NotImplementedException(),
        };

    /// <summary>
    /// 文字列からTimeSpan型に変換する
    /// </summary>
    /// <exception cref="NotSupportedException">TimeSpan型</exception>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value switch
        {
            null => DependencyProperty.UnsetValue,
            string timeSpanString => TimeSpan.Parse(timeSpanString),
            _ => throw new NotSupportedException(),
        };
}
