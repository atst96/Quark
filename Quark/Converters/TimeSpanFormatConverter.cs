using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace Quark.Converters;
public class TimeSpanFormatConverter : AvaloniaObject, IValueConverter
{
    /// <summary>フォーマット</summary>
    public string Format
    {
        get => this.GetValue<string>(FormatProperty);
        set => this.SetValue(FormatProperty, value);
    }

    /// <summary><see cref="Format"/>の依存関係プロパティ</summary>
    public static readonly AvaloniaProperty<string> FormatProperty =
        AvaloniaProperty.Register<TimeSpanFormatConverter, string>(nameof(Format), defaultValue: @"hh\:mm\:ss\.fff");

    /// <summary>
    /// TimeSpan型から文字列に変換する
    /// </summary>
    /// <param name="value"></param>
    /// <param name="targetType"></param>
    /// <param name="parameter"></param>
    /// <param name="culture"></param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is null ? null : value switch
        {
            TimeSpan timeSpanValue => timeSpanValue.ToString(this.Format),
            _ => throw new NotImplementedException(),
        };

    /// <summary>
    /// 文字列からTimeSpan型に変換する
    /// </summary>
    /// <exception cref="NotSupportedException">TimeSpan型</exception>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is null ? null : value switch
        {
            string stringValue => TimeSpan.Parse(stringValue),
            _ => throw new NotSupportedException(),
        };
}
