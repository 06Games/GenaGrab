using System;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;
using GeneaGrab.Strings;

namespace GeneaGrab.Helpers;

public static class ResourceExtensions
{
    public enum Resource { Core, UI }

    public static string? GetLocalized(string key, Resource src = Resource.Core) => GetLocalized(key, CultureInfo.CurrentUICulture, src);
    public static string? GetLocalized(string key, CultureInfo culture, Resource src = Resource.Core)
    {
        var manager = src switch
        {
            Resource.Core => Strings.Core.ResourceManager,
            Resource.UI => UI.ResourceManager,
            _ => throw new ArgumentOutOfRangeException(nameof(src), src, null)
        };

        return manager.GetString(key.Replace('/', '.'), culture);
    }
}
public class ResourceConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (!targetType.IsAssignableFrom(typeof(string))) throw new NotSupportedException();
        return GetLocalized(value?.ToString(), parameter as string, culture);
    }
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (!targetType.IsEnum || value is not string translatedText) throw new NotSupportedException();
        return targetType.GetEnumValues().Cast<object>().FirstOrDefault(enumValue => GetLocalized(enumValue?.ToString(), parameter as string, culture) == translatedText);
    }

    public static string? GetLocalized(string? value, string? param, CultureInfo culture)
    {
        if (value is null) return null;
        var res = ResourceExtensions.Resource.UI;

        var cat = param;
        var parameters = param?.Split('@');
        if (parameters is not null && parameters.Length == 2)
        {
            if (Enum.TryParse(parameters[0], out ResourceExtensions.Resource parsedRes)) res = parsedRes;
            cat = parameters[1];
        }

        return ResourceExtensions.GetLocalized(cat == null ? value : $"{cat}.{value}", culture, res) ?? value;
    }
}
