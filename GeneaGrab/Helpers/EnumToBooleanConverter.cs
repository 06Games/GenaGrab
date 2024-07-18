using System;
using System.ComponentModel;
using System.Globalization;
using Avalonia.Data.Converters;

namespace GeneaGrab.Helpers;

public class EnumToBooleanConverter : IValueConverter
{
    public Type EnumType { get; set; } = null!;
    public object? EnumValue { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (EnumType == null) throw new ArgumentException("An enum type must be set!");
        EnumValue ??= Enum.ToObject(EnumType, 0);
        if (EnumValue == null || !Enum.IsDefined(EnumType, EnumValue)) throw new ArgumentException("value must be a valid Enum object!");

        var enumValue = value switch
        {
            string enumString => Enum.TryParse(EnumType, enumString, out var enumObj) ? enumObj : null,
            Enum enumObj => enumObj,
            _ => throw new ArgumentException("parameter must be an Enum name or object!")
        };
        if (enumValue == null || !Enum.IsDefined(EnumType, enumValue)) throw new InvalidEnumArgumentException();
        return EnumValue.Equals(enumValue);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
