using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Quark.Converters;

[ValueConversion(typeof(bool), typeof(Visibility))]
internal class BoolToVisibilityConverter : IValueConverter
{
    public Visibility TrueVisibility { get; set; } = Visibility.Visible;

    public Visibility FalseVisibility { get; set; } = Visibility.Collapsed;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? this.TrueVisibility : this.FalseVisibility;
        }

        return DependencyProperty.UnsetValue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            return visibility == TrueVisibility;
        }

        return DependencyProperty.UnsetValue;
    }
}
