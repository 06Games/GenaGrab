using System;
using System.Security.Cryptography;
using System.Text;
using Integrative.Encryption;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

namespace GeneaGrab.Core.Models;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class AuthentificationNeededAttribute : Attribute { }
public interface IAuthentification
{
    public void Authenticate(Credentials credentials);
}
public class Credentials
{
    public string Username
    {
        get;
        set;
    }
    [JsonConverter(typeof(PasswordConverter))] public string Password
    {
        get;
        set;
    }

    private sealed class PasswordConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => true;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var entropy = Array.Empty<byte>();
            var cipher = Array.Empty<byte>();
            var plain = value?.ToString();
            if (plain != null)
            {
                var plaintext = Encoding.UTF8.GetBytes(plain); // Data to protect
                entropy = RandomNumberGenerator.GetBytes(20); // Generate additional entropy (will be used as the Initialization vector)
                cipher = CrossProtect.Protect(plaintext, entropy, DataProtectionScope.CurrentUser);
            }
            JToken.FromObject(new
            {
                Entropy = Convert.ToBase64String(entropy),
                Cipher = Convert.ToBase64String(cipher)
            }).WriteTo(writer);
        }
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var obj = JToken.ReadFrom(reader);
            if (!obj.HasValues) return null;
            byte[] plain;
            try
            {
                plain = CrossProtect.Unprotect(Convert.FromBase64String(obj.Value<string>("Cipher")), Convert.FromBase64String(obj.Value<string>("Entropy")), DataProtectionScope.CurrentUser);
            }
            catch (Exception e)
            {
                Log.Error(e, "Couldn't decipher password");
                return null;
            }
            if (objectType == typeof(byte[])) return plain;
            return Encoding.UTF8.GetString(plain);
        }
    }
}
