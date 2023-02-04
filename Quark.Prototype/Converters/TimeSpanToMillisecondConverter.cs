using System;
using System.Globalization;
using System.Windows.Data;

namespace Quark.Converters;

[ValueConversion(typeof(TimeSpan), typeof(double))]
public class TimeSpanToMillisecondConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is null ? 0d : ((TimeSpan)value).TotalMilliseconds;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is null ? TimeSpan.Zero : TimeSpan.FromMilliseconds((double)value);
}
