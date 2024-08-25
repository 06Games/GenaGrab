using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GeneaGrab.Core.Helpers;
using GeneaGrab.Core.Models;
using GeneaGrab.Core.Models.Dates;
using Newtonsoft.Json.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace GeneaGrab.Core.Providers;

public class Geneanet : Provider
{
    public override string Id => "Geneanet";
    public override string Url => "https://www.geneanet.org/";

    public override Task<RegistryInfo> GetRegistryFromUrlAsync(Uri url)
    {
        if (url.Host != "www.geneanet.org" || !url.AbsolutePath.StartsWith("/registres/view")) return Task.FromResult<RegistryInfo>(null);

        var regex = Regex.Match(url.OriginalString, @"(?:idcollection=(?<col>\d*).*page=(?<page>\d*))|(?:\/(?<col>\d+)(?:\z|\/(?<page>\d*)))");
        return Task.FromResult(new RegistryInfo(this, regex.Groups["col"].Value) { PageNumber = int.TryParse(regex.Groups.TryGetValue("page") ?? "1", out var pageNumber) ? pageNumber : 1 });
    }

    #region Infos

    public override async Task<(Registry, int)> Infos(Uri url)
    {
        var regex = Regex.Match(url.OriginalString, @"(?:idcollection=(?<col>\d*).*page=(?<page>\d*))|(?:\/(?<col>\d+)(?:\z|\/(?<page>\d*)))");
        var registry = new Registry(this, regex.Groups.TryGetValue("col")) { URL = url.OriginalString };
        if (string.IsNullOrEmpty(registry.Id)) return (null, -1);

        var client = new HttpClient();
        var page = await client.GetStringAsync(registry.URL);
        var infos = Regex.Match(page,
            "Informations sur le document.*?<p>(\\[.*\\] - )?(?<location>.*) \\((?<locationDetails>.*?)\\) - (?<globalType>.*?)( \\((?<type>.*?)\\))?( - .*)? *\\| (?<from>.*) - (?<to>.*?)<\\/p>.*?<p>(?<cote>.*)</p>(.*<p>(?<notaire>.*)</p>)?.*<p class=\\\"no-margin-bottom\\\">(?<betterType>.*?)(\\..*| -.*)?</p>.*<p>(?<note>.*)</p>.*<strong>Lien permanent : </strong>",
            RegexOptions.Multiline | RegexOptions.Singleline); //https://regex101.com/r/3Ou7DP/5
        var location = infos.Groups.TryGetValue("locationDetails")?.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries).Reverse().ToList() ?? new List<string>();
        location.Add(infos.Groups["location"].Value.Trim(' '));

        var (types, district, notes) = TryParseNotes(page, infos);
        registry.Types = types;
        registry.Notes = notes;
        if (!string.IsNullOrWhiteSpace(district)) location.Add(district);
        registry.Location = location.ToArray();

        registry.From = Date.ParseDate(infos.Groups["from"].Value);
        registry.To = Date.ParseDate(infos.Groups["to"].Value);

        registry = await UpdateInfos(registry, client);
        if (!int.TryParse(regex.Groups["page"].Success ? regex.Groups["page"].Value : "1", out var pageNumber)) pageNumber = 1;

        return (registry, pageNumber);
    }
    private static async Task<Registry> UpdateInfos(Registry registry, HttpClient client = null)
    {
        client ??= new HttpClient();

        registry.URL = $"https://www.geneanet.org/registres/view/{registry.Id}";

        var pagesData = await client.GetStringAsync($"https://www.geneanet.org/registres/api/images/{registry.Id}?min_page=1&max_page={int.MaxValue}");
        registry.Frames = JObject.Parse($"{{\"results\": {pagesData}}}").Value<JArray>("results")?.Select(p =>
        {
            var pageNumber = p.Value<int>("page");
            var page = registry.Frames.FirstOrDefault(rPage => rPage.FrameNumber == pageNumber) ?? new Frame { FrameNumber = pageNumber };
            page.DownloadUrl = $"https://www.geneanet.org{p.Value<string>("image_base_url")?.TrimEnd('/')}/";
            page.ArkUrl = p.Value<string>("image_route");
            return page;
        }).ToArray() ?? Array.Empty<Frame>();
        return registry;
    }

    private static (List<RegistryType> types, string location, string notes) TryParseNotes(string page, Match infos)
    {
        var types = new List<RegistryType>();
        var global = (infos.Groups.TryGetValue("globalType") ?? infos.Groups.TryGetValue("type"))?.Trim(' ').ToLowerInvariant();
        var better = infos.Groups.TryGetValue("betterType") ?? infos.Groups.TryGetValue("type");
        foreach (var t in better?.Split(',') ?? Array.Empty<string>())
            if (TryGetType(t.Trim(' ').ToLowerInvariant(), out var type))
                types.Add(type);

        var location = Regex.Match(page, "Paroisse de (?<location>.*?)(\\.|-|<)").Groups.TryGetValue("location");
        var note = infos.Groups.TryGetValue("notes");

        return (types, location, note);

        bool TryGetType(string type, out RegistryType t)
        {
            var civilStatus = global?.Contains("état civil") ?? false;
            if (type.Contains("naissances")) t = civilStatus ? RegistryType.Birth : RegistryType.BirthTable;
            else if (type.Contains("baptemes")) t = RegistryType.Baptism;
            else if (type.Contains("communions")) t = RegistryType.Communion;
            else if (type.Contains("confirmations")) t = RegistryType.Confirmation;
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
    }

    #endregion

    #region Page

    public override async Task<Stream> GetFrame(Frame page, Scale scale, Action<Progress> progress)
    {
        var stream = await Data.TryGetImageFromDrive(page, scale);
        if (stream != null) return stream;

        progress?.Invoke(Progress.Unknown);
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("UserAgent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:75.0) Gecko/20100101 Firefox/75.0");

        if (!page.TileSize.HasValue) (page.Width, page.Height, page.TileSize) = await Zoomify.ImageData(page.DownloadUrl, client);
        var maxZoom = Zoomify.CalculateIndex(page);
        var scaleZoom = maxZoom * scale switch
        {
            Scale.Thumbnail => 0,
            Scale.Navigation => 0.75,
            _ => 1
        };
        var zoom = Math.Min((int)Math.Ceiling(scaleZoom), maxZoom);
        var (tiles, diviser) = Zoomify.GetTilesNumber(page, zoom);

        progress?.Invoke(0);
        Image image = new Image<Rgb24>(page.Width!.Value, page.Height!.Value);
        var tasks = new Dictionary<Task<Image>, (int tileSize, int scale, Point pos)>();
        for (var y = 0; y < tiles.Y; y++)
            for (var x = 0; x < tiles.X; x++)
                tasks.Add(Grabber.GetImage($"{page.DownloadUrl}TileGroup0/{zoom}-{x}-{y}.jpg", client).ContinueWith(task =>
                {
                    progress?.Invoke(tasks.Keys.Count(t => t.IsCompleted) / (float)tasks.Count);
                    return task.Result;
                }), (page.TileSize.GetValueOrDefault(), diviser, new Point(x, y)));

        await Task.WhenAll(tasks.Keys).ConfigureAwait(false);
        image = tasks.Aggregate(image, (current, tile) => current.MergeTile(tile.Key.Result, tile.Value));
        page.ImageSize = scale;
        progress?.Invoke(Progress.Finished);

        await Data.SaveImage(page, image, false).ConfigureAwait(false);
        return image.ToStream();
    }

    #endregion
}
