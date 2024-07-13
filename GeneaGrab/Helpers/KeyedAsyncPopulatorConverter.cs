using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Data.Converters;
using Microsoft.VisualStudio.Threading;

namespace GeneaGrab.Helpers;

public class KeyedAsyncPopulatorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (!targetType.IsAssignableFrom(typeof(Func<string?, CancellationToken, Task<IEnumerable<object>>>))) throw new InvalidOperationException("Target must be an AsyncPopulator");
        if (value == null) return null;
        if (parameter is not string key) throw new ArgumentException("parameter must be an key name");
        if (value is not Func<string, string?, Task<IEnumerable<object>>> function) throw new ArgumentException("value must be a KeyedAsyncPopulator function");
        return new Func<string?, CancellationToken, Task<IEnumerable<object>>>((s, token) => function.Invoke(key, s).WithCancellation(token));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
