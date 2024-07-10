using System.Threading.Tasks;
using Newtonsoft.Json;

namespace GeneaGrab.Core.Helpers;

public static class Json
{
    public static Task<T> ToObjectAsync<T>(string value) => Task.Run(() => JsonConvert.DeserializeObject<T>(value));
    public static Task<string> StringifyAsync(object value) => Task.Run(() => JsonConvert.SerializeObject(value));
}
