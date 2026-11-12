using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SmartBite.Client.Converters;

/// <summary>True when the bound value equals the parameter. Lets a RadioButton edit an enum property.</summary>
public class EqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture) =>
        Equals(value, parameter);

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture
    ) => (bool)value ? parameter : Binding.DoNothing;
}

/// <summary>Visible when the value is null or false or empty text; Collapsed otherwise.</summary>
public class InverseVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture) =>
        value is null or false or "" ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture
    ) => throw new NotSupportedException();
}

/// <summary>Visible when the value is not null; Collapsed otherwise.</summary>
public class NotNullVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture) =>
        value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture
    ) => throw new NotSupportedException();
}
