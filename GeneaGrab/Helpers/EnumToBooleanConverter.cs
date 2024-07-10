using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace GeneaGrab.Helpers;

public class EnumToBooleanConverter : IValueConverter
{
    public Type EnumType { get; set; } = null!;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter is not string enumString) throw new ArgumentException("parameter must be an Enum name!");
        if (value == null || !Enum.IsDefined(EnumType, value)) throw new ArgumentException("value must be an Enum!");
        var enumValue = Enum.Parse(EnumType, enumString);
        return enumValue.Equals(value);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter is string enumString) return Enum.Parse(EnumType, enumString);
        throw new ArgumentException("parameter must be an Enum name!");
    }
}
