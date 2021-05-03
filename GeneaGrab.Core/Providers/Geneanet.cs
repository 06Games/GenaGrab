﻿using Newtonsoft.Json.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GeneaGrab.Providers
{
    public class Geneanet : ProviderAPI
    {
        public bool CheckURL(Uri URL) => URL.Host == "www.geneanet.org" && URL.AbsolutePath.StartsWith("/archives");
        public async Task<RegistryInfo> Infos(Uri URL)
        {
            var Location = new Location(Data.Providers["Geneanet"]);
            var Registry = new Registry(Location) { URL = URL.OriginalString };

            var regex = Regex.Match(Registry.URL, "(?:idcollection=(?<col>\\d*).*page=(?<page>\\d*))|(?:\\/(?<col>\\d+)(?:\\z|\\/(?<page>\\d*)))");
            Registry.ID = regex.Groups["col"]?.Value;
            if (string.IsNullOrEmpty(Registry.ID)) return null;

            var client = new WebClient();
            string pages = null;
            await Task.Run(() => pages = client.DownloadString(new Uri($"https://www.geneanet.org/archives/registres/api/?idcollection={Registry.ID}")));

            Registry.URL = $"https://www.geneanet.org/archives/registres/view/{Registry.ID}";
            string page = null;
            await Task.Run(() => page = client.DownloadString(new Uri(Registry.URL)));
            var infos = Regex.Match(page, "<h3>(?<location>.*)\\(.*\\| (?<from>.*) - (?<to>.*)<\\/h3>\\n.*<div class=\"note\">(?<note>(\\n\\s*.*)*?)\\n\\s*<\\/div>\\n\\s*<p class=\"noteShort\">"); //https://regex101.com/r/3Ou7DP/1
            Location.Name = infos.Groups["location"].Value.Trim(' ');

            var notes = TryParseNotes(infos.Groups["note"].Value.Replace("\n", "").Replace("\r", ""));
            Registry.Types = notes.types;
            Registry.Notes = notes.notes;
            Location.District = notes.location;
            Registry.LocationID = Location.ID = Location.ToString();

            Registry.From = Data.ParseDate(infos.Groups["from"].Value);
            Registry.To = Data.ParseDate(infos.Groups["to"].Value);
            Registry.Pages = JObject.Parse($"{{results: {pages}}}").Value<JArray>("results").Select(p => new RPage { Number = p.Value<int>("page"), URL = p.Value<string>("chemin_image") }).ToArray();
            int.TryParse(regex.Groups["page"].Success ? regex.Groups["page"].Value : "1", out var _p);

            Data.AddOrUpdate(Data.Locations, Location.ID, Location);
            Data.AddOrUpdate(Data.Registries, Registry.ID, Registry);
            return new RegistryInfo { ProviderID = "Geneanet", LocationID = Location.ID, RegistryID = Registry.ID, PageNumber = _p };
        }
        public static (List<Registry.Type> types, string location, string notes) TryParseNotes(string notes)
        {
            var types = new List<Registry.Type>();
            var typesMatch = Regex.Match(notes, "((?<globalType>.*) - .* : )?(?<type>.+?)( - ((?<betterType>.*?)\\.|-|).*)?<div class=\\\"analyse\\\">.*<\\/div>"); //https://regex101.com/r/SE97Xj/1
            var global = (typesMatch.Groups["globalType"] ?? typesMatch.Groups["type"])?.Value.Trim(' ').ToLowerInvariant();
            foreach (var t in (typesMatch.Groups["betterType"] ?? typesMatch.Groups["type"])?.Value.Split(',')) 
                if (TryGetType(t.Trim(' '), out var type)) types.Add(type);

            bool TryGetType(string type, out Registry.Type t)
            {
                var civilStatus = global.Contains("état civil");
                if (type.Contains("naissances")) t = civilStatus ? Registry.Type.Birth : Registry.Type.BirthTable;
                else if (type.Contains("baptemes")) t = Registry.Type.Baptism;
                else if (type.Contains("promesses de mariage")) t = Registry.Type.Banns;
                else if (type.Contains("mariages")) t = civilStatus ? Registry.Type.Marriage : Registry.Type.MarriageTable;
                else if (type.Contains("décès")) t = civilStatus ? Registry.Type.Death : Registry.Type.DeathTable;
                else if (type.Contains("sépultures") || type.Contains("inhumation")) t = Registry.Type.Burial;

                else if (type.Contains("recensements")) t = Registry.Type.Census;
                else if (type.Contains("archives notariales")) t = Registry.Type.Notarial;
                else if (type.Contains("registres matricules")) t = Registry.Type.Military;

                else if (type.Contains("autres") || type.Contains("archives privées")) t = Registry.Type.Other;
                else
                {
                    t = Registry.Type.Unknown;
                    return false;
                }
                return true;
            }

            var location = Regex.Match(notes, ".*Paroisse de (?<location>.*)\\.|-|<.*").Groups["location"]?.Value;
            var note = Regex.Match(notes, ".*<div class=\"analyse\">(?<notes>.+)<\\/div>").Groups["notes"]?.Value;

            return (types, location, note ?? notes);
        }

        public async Task<RPage> GetTile(string RegistryID, RPage page, int zoom) => await GetTiles(RegistryID, page, zoom, false);
        public async Task<RPage> GetTiles(string RegistryID, RPage current, double zoom, bool progress)
        {
            if (await Data.TryGetImageFromDrive(RegistryID, current, zoom)) return current;

            var chemin_image = Uri.EscapeDataString($"doc/{current.URL}");
            var baseURL = $"https://www.geneanet.org/zoomify/?path={chemin_image}/";

            if (!current.TileSize.HasValue)
            {
                var args = await Grabber.Zoomify(baseURL);
                current.Width = args.w;
                current.Height = args.h;
                current.TileSize = args.tileSize;
            }

            var data = Grabber.NbTiles(current, zoom);
            if (current.MaxZoom == -1) current.MaxZoom = Grabber.CalculateIndex(current);
            current.Zoom = zoom < current.MaxZoom ? (int)Math.Ceiling(zoom) : current.MaxZoom;

            if (current.Image == null) current.Image = new Image<Rgb24>(current.Width, current.Height);
            var tasks = new Dictionary<Task<Image>, (int tileSize, int scale, Point pos)>();
            for (int y = 0; y < data.tiles.Y; y++)
                for (int x = 0; x < data.tiles.X; x++)
                    tasks.Add(Grabber.GetTile($"{baseURL}TileGroup0/{current.Zoom}-{x}-{y}.jpg"), (current.TileSize.Value, data.diviser, new Point(x, y)));

            await Task.WhenAll(tasks.Keys);
            foreach (var tile in tasks) current.Image = current.Image.MergeTile(tile.Key.Result, tile.Value);

            Data.Registries[RegistryID].Pages[current.Number - 1] = current;
            await Data.SaveImage(Data.Registries[RegistryID], current);
            return current;
        }

        public async Task<RPage> Download(string RegistryID, RPage page) => await GetTiles(RegistryID, page, Grabber.CalculateIndex(page), true);
    }
}
