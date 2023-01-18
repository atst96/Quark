using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Quark.Converters;

[ValueConversion(typeof(bool), typeof(Brush))]
internal class BoolToBrushConverter : IValueConverter
{
    public Brush? TrueBrush { get; set; }

    public Brush? FalseBrush { get; set; }

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? this.TrueBrush : this.FalseBrush;
        }

        return DependencyProperty.UnsetValue;
    }

    public object? ConvertBack(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
