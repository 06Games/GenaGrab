using System;
using Avalonia.Markup.Xaml;

namespace GeneaGrab.Helpers;

public sealed class EnumerateExtension(Type type) : MarkupExtension
{
    [ConstructorArgument("type")] private Type? Type { get; } = type;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (Type == null) throw new InvalidOperationException(@"The EnumType property is not specified.");
        var actualType = Nullable.GetUnderlyingType(Type) ?? Type;
        if (!actualType.IsEnum) throw new ArgumentException($@"The type '{Type}' has no standard values.");

        var standardValues = actualType.GetEnumValues();
        var items = Type == actualType ? new object?[standardValues.Length] : new object[standardValues.Length + 1];
        standardValues.CopyTo(items, 0);
        return items;
    }
}
