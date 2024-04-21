using System.Collections.Generic;
using System.IO;
using GeneaGrab.Core.Models;
using GeneaGrab.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace GeneaGrab.Services;

public static class SettingsService
{
    private static readonly string FilePath = Path.Combine(LocalData.AppData, "Settings.json");

    private static SettingsData? settingsData;
    public static SettingsData SettingsData
    {
        get
        {
            if (settingsData is not null) return settingsData;
            if (!File.Exists(FilePath)) return settingsData = new SettingsData();

            var data = File.ReadAllText(FilePath);
            return settingsData = JsonConvert.DeserializeObject<SettingsData>(data) ?? new SettingsData();
        }
        set
        {
            if (Equals(settingsData, value)) return;
            settingsData = value;
            Save();
        }
    }

    public static void Save()
    {
        var data = JsonConvert.SerializeObject(settingsData, Formatting.Indented);
        File.WriteAllText(FilePath, data);
    }
}
public class SettingsData
{
    [JsonProperty(nameof(Theme)), JsonConverter(typeof(StringEnumConverter))] private Theme theme;
    [JsonIgnore] public Theme Theme { get => theme; set => Set(ref theme, value); }

    [JsonProperty(nameof(Credentials))] private Dictionary<string, Credentials> credentials = new();
    [JsonIgnore] public Dictionary<string, Credentials> Credentials { get => credentials; set => Set(ref credentials, value); }

    [JsonProperty(nameof(ZoomMultiplier))] private double zoomMultiplier = 1.5;
    [JsonIgnore] public double ZoomMultiplier { get => zoomMultiplier; set => Set(ref zoomMultiplier, value); }

    private static void Set<T>(ref T storage, T value)
    {
        if (Equals(storage, value)) return;
        storage = value;
        SettingsService.Save();
    }
}
