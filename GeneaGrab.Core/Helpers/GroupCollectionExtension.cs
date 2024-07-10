using System.Text.RegularExpressions;

namespace GeneaGrab.Core.Helpers;

public static class GroupCollectionExtension
{
    public static string TryGetValue(this GroupCollection data, string key)
    {
        var value = data[key];
        return value.Success ? value.Value : null;
    }
}
