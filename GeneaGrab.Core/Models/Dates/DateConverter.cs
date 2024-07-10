using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GeneaGrab.Core.Models.Dates;

internal class DateConverter : JsonConverter<Date>
{
    public override Date ReadJson(JsonReader reader, Type objectType, Date existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.String) return Date.ParseDate((string)reader.Value);
        if (reader.TokenType != JsonToken.StartObject) return null;

        var jObject = JObject.Load(reader);

        var dt = jObject.Value<DateTime>("DateTime");
        if (!Enum.TryParse(jObject.Value<string>("Precision"), out Precision precision)) precision = default;
        return jObject.Value<string>("Calendar") switch
        {
            nameof(JulianDate) => new JulianDate(dt, precision),
            nameof(FrenchRepublicanDate) => new FrenchRepublicanDate(dt, precision),
            _ => new GregorianDate(dt, precision)
        };
    }
    public override void WriteJson(JsonWriter writer, Date value, JsonSerializer serializer)
    {
        writer.WriteStartObject();
        WriteProperty("Calendar", value.GetType().Name);
        WriteProperty("Precision", Enum.GetName(typeof(Precision), value.Precision));
        writer.WritePropertyName("DateTime");
        serializer.Serialize(writer, value.GregorianDateTime);
        writer.WriteEndObject();

        void WriteProperty(string propertyName, object propertyValue)
        {
            writer.WritePropertyName(propertyName);
            writer.WriteValue(propertyValue);
        }
    }
}
