﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GeneaGrab
{
    /// <summary>Data on the registry</summary>
    public class Registry : IEquatable<Registry>
    {
        public Registry() { }
        public Registry(Provider provider) => ProviderID = provider.ID;
        public string ProviderID { get; set; }
        [JsonIgnore] public Provider Provider => Data.Providers[ProviderID];
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)] public string[] LocationDetails { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)] public string Location { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)] public string LocationID { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)] public string District { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)] public string DistrictID { get; set; }

        public string ID { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)] public string CallNumber { get; set; }
        public string URL { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)] public string ArkURL { get; set; }
        [JsonProperty(ItemConverterType = typeof(Newtonsoft.Json.Converters.StringEnumConverter))] public IEnumerable<RegistryType> Types { get; set; } = Array.Empty<RegistryType>();
        [JsonIgnore]
        public string TypeToString => Types.Any() ? string.Join(", ", Types.Select(t =>
        {
            var type = Enum.GetName(typeof(RegistryType), t);
            return Data.Translate($"Registry/Type/{type}", type);
        })) : null;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)] public string Notes { get; set; }

        public Core.Models.Dates.Date From { get; set; }
        public Core.Models.Dates.Date To { get; set; }
        [JsonIgnore]
        public string Dates
        {
            get
            {
                if (From != null && To != null && From == To) return From.ToString(Core.Models.Dates.Precision.Days);
                else if (From != null && To != null)
                {
                    var from = From;
                    var to = To;
                    var format = Core.Models.Dates.Precision.Years;
                    if (from.Precision >= Core.Models.Dates.Precision.Days && to.Precision >= Core.Models.Dates.Precision.Days && (from.Day != to.Day || from.Day.Value != 1)) format = Core.Models.Dates.Precision.Days;
                    else if (from.Precision >= Core.Models.Dates.Precision.Months && to.Precision >= Core.Models.Dates.Precision.Months && (from.Month != to.Month || from.Month.Value != 1)) format = Core.Models.Dates.Precision.Months;
                    return $"{from.ToString(format)} - {to.ToString(format)}";
                }
                else if (From != null) return $"{From.ToString(Core.Models.Dates.Precision.Days)} - ?";
                else if (To != null) return $"? - {To.ToString(Core.Models.Dates.Precision.Days)}";
                return null;
            }
        }


        [JsonIgnore]
        public string Name
        {
            get
            {
                //Type
                var name = TypeToString ?? "";

                //Dates
                var dates = Dates;
                if (!string.IsNullOrEmpty(dates)) name += $" ({dates})";

                //Notes
                if (!string.IsNullOrEmpty(Notes)) name += $" - {Notes.Split('\n').FirstOrDefault()}";

                return name;
            }
        }
        public override string ToString() => Name;

        [JsonConverter(typeof(PagesConverter))] public RPage[] Pages { get; set; }


        public bool Equals(Registry other) => ID == other?.ID;
        public override bool Equals(object obj) => Equals(obj as Registry);
        public static bool operator ==(Registry one, Registry two) => one?.ID == two?.ID;
        public static bool operator !=(Registry one, Registry two) => !(one == two);
        public override int GetHashCode() => ID.GetHashCode();
    }
    public class DateFormatConverter : Newtonsoft.Json.Converters.IsoDateTimeConverter { public DateFormatConverter(string format) => DateTimeFormat = format; }
    public class PagesConverter : JsonConverter<RPage[]>
    {
        public override RPage[] ReadJson(JsonReader reader, Type objectType, RPage[] existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            return serializer.Deserialize<Dictionary<int, RPage>>(reader).Select(kv =>
            {
                kv.Value.Number = kv.Key;
                return kv.Value;
            }).ToArray();
        }
        public override void WriteJson(JsonWriter writer, RPage[] value, JsonSerializer serializer) => serializer.Serialize(writer, value.ToDictionary(k => k.Number, v => v));
    }

    /// <summary>Data on the page of the registry</summary>
    public class RPage
    {
        [JsonIgnore] public int Number { get; set; }
        public override string ToString() => Number.ToString();
        /// <summary>Ark URL</summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)] public string URL { get; set; }
        /// <summary>Used internaly to download the image</summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)] public string DownloadURL { get; set; }

        public int Zoom { get; set; } = -1;
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore), System.ComponentModel.DefaultValue(-1)] public int MaxZoom { get; set; } = -1;
        /// <summary>Total width of the image</summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)] public int Width { get; set; }
        /// <summary>Total height of the image</summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)] public int Height { get; set; }
        /// <summary>Tiles size (if applicable)</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)] public int? TileSize { get; set; }

        /// <summary>Notes about the page (user can edit this information)</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)] public string Notes { get; set; }
        /// <summary>Any additional information the grabber needs</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)] public object Extra { get; set; }
    }
}
