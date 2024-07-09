using System;
using System.Collections;
using System.ComponentModel;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;

namespace GeneaGrab.Helpers;

public sealed class EnumerateExtension(Type type) : MarkupExtension
{
    #region MarkupExtension Members

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (Type == null) throw new InvalidOperationException(@"The EnumType property is not specified.");

        var actualType = Nullable.GetUnderlyingType(Type) ?? Type;
        TypeConverter typeConverter;
        ICollection standardValues;

        if ((typeConverter = TypeDescriptor.GetConverter(actualType)) == null || (standardValues = typeConverter.GetStandardValues(serviceProvider as ITypeDescriptorContext)) == null)
            throw new ArgumentException($@"The type '{Type}' has no standard values.", "value");

        var items = Type == actualType ? new object[standardValues.Count] : new object[standardValues.Count + 1];
        var index = 0;

        if (Converter == null)
            foreach (var standardValue in standardValues)
                items[index++] = standardValue;
        else
        {
            var culture = ConverterCulture ?? CultureInfo.CurrentCulture;
            foreach (var standardValue in standardValues) items[index++] = Converter.Convert(standardValue, typeof(object), ConverterParameter, culture);
            if (Type != actualType) items[index] = Converter.Convert(null, typeof(object), ConverterParameter, culture);
        }

        return items;
    }

    #endregion

    #region Properties

    [ConstructorArgument("type")] private Type? Type { get; set; } = type;
    public IValueConverter? Converter { get; set; }
    public CultureInfo? ConverterCulture { get; set; }
    public object? ConverterParameter { get; set; }

    #endregion
}
