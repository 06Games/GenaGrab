﻿using Newtonsoft.Json.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GeneaGrab.Providers
{
    public class Geneanet : ProviderAPI
    {
        public bool TryGetRegistryID(Uri URL, out RegistryInfo info)
        {
            info = null;
            if (URL.Host != "www.geneanet.org" || !URL.AbsolutePath.StartsWith("/archives")) return false;

            var regex = Regex.Match(URL.OriginalString, "(?:idcollection=(?<col>\\d*).*page=(?<page>\\d*))|(?:\\/(?<col>\\d+)(?:\\z|\\/(?<page>\\d*)))");
            info = new RegistryInfo
            {
                RegistryID = regex.Groups["col"]?.Value,
                ProviderID = "Geneanet",
                PageNumber = int.TryParse(regex.Groups["page"].Success ? regex.Groups["page"].Value : "1", out var _p) ? _p : 1
            };
            return true;
        }

        public async Task<RegistryInfo> Infos(Uri URL)
        {
            var Registry = new Registry(Data.Providers["Geneanet"]) { URL = URL.OriginalString };

            var regex = Regex.Match(Registry.URL, "(?:idcollection=(?<col>\\d*).*page=(?<page>\\d*))|(?:\\/(?<col>\\d+)(?:\\z|\\/(?<page>\\d*)))");
            Registry.ID = regex.Groups["col"]?.Value;
            if (string.IsNullOrEmpty(Registry.ID)) return null;

            var client = new HttpClient();
            string pages = await client.GetStringAsync($"https://www.geneanet.org/archives/registres/api/?idcollection={Registry.ID}");

            Registry.URL = $"https://www.geneanet.org/archives/registres/view/{Registry.ID}";
            string page = await client.GetStringAsync(Registry.URL);
            var infos = Regex.Match(page, "<h3>(?<location>.*)\\(.*\\| (?<from>.*) - (?<to>.*)<\\/h3>\\n.*<div class=\"note\">(?<note>(\\n\\s*.*)*?)\\n\\s*<\\/div>\\n\\s*<p class=\"noteShort\">"); //https://regex101.com/r/3Ou7DP/1
            Registry.Location = Registry.LocationID = infos.Groups["location"].Value.Trim(' ');

            var notes = TryParseNotes(infos.Groups["note"].Value.Replace("\n", "").Replace("\r", ""));
            Registry.Types = notes.types;
            Registry.Notes = notes.notes;
            Registry.District = Registry.DistrictID = string.IsNullOrWhiteSpace(notes.location) ? null : notes.location;

            Registry.From = Data.ParseDate(infos.Groups["from"].Value);
            Registry.To = Data.ParseDate(infos.Groups["to"].Value);
            Registry.Pages = JObject.Parse($"{{results: {pages}}}").Value<JArray>("results").Select(p => new RPage { Number = p.Value<int>("page"), URL = p.Value<string>("chemin_image") }).ToArray();
            int.TryParse(regex.Groups["page"].Success ? regex.Groups["page"].Value : "1", out var _p);

            var marqueurs = Regex.Matches(page, "<option value=\\\"(?<index>\\d*)\\\" id=\\\".*\\\" ?>(?<year>\\d*)-(?<month>\\d*)-(?<type>.*)<\\/option>");
            foreach (var pageMarqueurs in marqueurs.Cast<Match>().GroupBy(m => m.Groups["index"].Value))
            {
                if (!int.TryParse(pageMarqueurs.Key ?? "0", out int i) || i < 1) continue;
                Registry.Pages[i - 1].Notes = string.Join(" - ", pageMarqueurs.Select(marqueur => marqueur.Groups["year"].Value)) + "\n\n"
                                            + string.Join("\n", pageMarqueurs.Select(marqueur => $"{marqueur.Groups["month"]}/{marqueur.Groups["year"]} ({marqueur.Groups["type"]})"));
            }

            Data.AddOrUpdate(Data.Providers["Geneanet"].Registries, Registry.ID, Registry);
            return new RegistryInfo { ProviderID = "Geneanet", RegistryID = Registry.ID, PageNumber = _p };
        }
        static (List<RegistryType> types, string location, string notes) TryParseNotes(string notes)
        {
            var types = new List<RegistryType>();
            var typesMatch = Regex.Match(notes, "((?<globalType>.*) - .* : )?(?<type>.+?)( - (?<betterType>.*?)(\\..*| -.*)?)?<div class=\\\"analyse\\\">.*<\\/div>"); //https://regex101.com/r/SE97Xj/3
            var global = (typesMatch.Groups["globalType"] ?? typesMatch.Groups["type"])?.Value.Trim(' ').ToLowerInvariant();
            foreach (var t in (typesMatch.Groups["betterType"] ?? typesMatch.Groups["type"])?.Value.Split(','))
                if (TryGetType(t.Trim(' ').ToLowerInvariant(), out var type)) types.Add(type);

            bool TryGetType(string type, out RegistryType t)
            {
                var civilStatus = global.Contains("état civil");
                if (type.Contains("naissances")) t = civilStatus ? RegistryType.Birth : RegistryType.BirthTable;
                else if (type.Contains("baptemes")) t = RegistryType.Baptism;
                else if (type.Contains("promesses de mariage")) t = RegistryType.Banns;
                else if (type.Contains("mariages")) t = civilStatus ? RegistryType.Marriage : RegistryType.MarriageTable;
                else if (type.Contains("décès")) t = civilStatus ? RegistryType.Death : RegistryType.DeathTable;
                else if (type.Contains("sépultures") || type.Contains("inhumation")) t = RegistryType.Burial;

                else if (type.Contains("recensements")) t = RegistryType.Census;
                else if (type.Contains("etat des âmes")) t = RegistryType.LiberStatutAnimarum;
                else if (type.Contains("archives notariales")) t = RegistryType.Notarial;
                else if (type.Contains("registres matricules")) t = RegistryType.Military;

                else if (type.Contains("autres") || type.Contains("archives privées")) t = RegistryType.Other;
                else
                {
                    t = RegistryType.Unknown;
                    return false;
                }
                return true;
            }

            var location = Regex.Match(notes, ".*Paroisse de (?<location>.*)\\.|-|<.*").Groups["location"]?.Value;
            var note = Regex.Match(notes, ".*<div class=\"analyse\">(?<notes>.+)<\\/div>").Groups["notes"]?.Value;

            return (types, location, note ?? notes);
        }

        public Task<string> Ark(Registry Registry, RPage Page) => Task.FromResult($"{Registry.URL}/{Page.Number}");
        public Task<RPage> Thumbnail(Registry Registry, RPage page, Action<Progress> progress) => GetTiles(Registry, page, 0, progress);
        public Task<RPage> Preview(Registry Registry, RPage page, Action<Progress> progress) => GetTiles(Registry, page, Zoomify.CalculateIndex(page) * 0.75F, progress);
        public Task<RPage> Download(Registry Registry, RPage page, Action<Progress> progress) => GetTiles(Registry, page, Zoomify.CalculateIndex(page), progress);
        public static async Task<RPage> GetTiles(Registry Registry, RPage current, double zoom, Action<Progress> progress)
        {
            if (await Data.TryGetImageFromDrive(Registry, current, zoom)) return current;

            progress?.Invoke(Progress.Unknown);
            var chemin_image = Uri.EscapeDataString($"doc/{current.URL}");
            var baseURL = $"https://www.geneanet.org/zoomify/?path={chemin_image}/";
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("UserAgent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:75.0) Gecko/20100101 Firefox/75.0");

            if (!current.TileSize.HasValue)
            {
                var args = await Zoomify.ImageData(baseURL, client);
                current.Width = args.w;
                current.Height = args.h;
                current.TileSize = args.tileSize;
            }

            if (current.MaxZoom == -1) current.MaxZoom = Zoomify.CalculateIndex(current);
            current.Zoom = zoom < current.MaxZoom ? (int)Math.Ceiling(zoom) : current.MaxZoom;
            var (tiles, diviser) = Zoomify.NbTiles(current, current.Zoom);

            progress?.Invoke(0);
            if (current.Image == null) current.Image = new Image<Rgb24>(current.Width, current.Height);
            var tasks = new Dictionary<Task<Image>, (int tileSize, int scale, Point pos)>();
            for (int y = 0; y < tiles.Y; y++)
                for (int x = 0; x < tiles.X; x++)
                    tasks.Add(Grabber.GetImage($"{baseURL}TileGroup0/{current.Zoom}-{x}-{y}.jpg", client).ContinueWith((task) =>
                    {
                        progress?.Invoke(tasks.Keys.Count(t => t.IsCompleted) / (float)tasks.Count);
                        return task.Result;
                    }), (current.TileSize.Value, diviser, new Point(x, y)));

            await Task.WhenAll(tasks.Keys).ConfigureAwait(false);
            progress?.Invoke(Progress.Finished);
            foreach (var tile in tasks) current.Image = current.Image.MergeTile(tile.Key.Result, tile.Value);

            Data.Providers["Geneanet"].Registries[Registry.ID].Pages[current.Number - 1] = current;
            await Data.SaveImage(Registry, current).ConfigureAwait(false);
            return current;
        }
    }
}
