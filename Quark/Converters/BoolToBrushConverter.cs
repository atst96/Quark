using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Quark.Converters;

internal class BoolToBrushConverter : IValueConverter
{
    public IBrush? TrueBrush { get; set; }

    public IBrush? FalseBrush { get; set; }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? this.TrueBrush : this.FalseBrush;
        }

        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value == this.TrueBrush;
    }
}
