using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using GeneaGrab.Core.Helpers;
using GeneaGrab.Core.Models;
using GeneaGrab.Core.Models.Dates;
using Gx.Source;
using Gx.Types;
using Newtonsoft.Json.Linq;
using Serilog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace GeneaGrab.Core.Providers;

public class FamilySearch : Provider, IAuthentification
{
    private static string BaseUrl => "https://www.familysearch.org";
    private static string IdentBaseUrl => "https://ident.familysearch.org";

    public override string Id => "FamilySearch";
    public override string Url => BaseUrl;

    private static string Locale => CultureInfo.CurrentCulture.TwoLetterISOLanguageName.ToLower();


    private HttpClient _client;
    private async Task<HttpClient> GetClient()
    {
        if (Credentials == null) throw new AuthenticationException("This method requires authentication");
        _client ??= await Authenticate(Credentials.Username, Credentials.Password);
        return _client;
    }

    private Credentials Credentials { get; set; }
    public void Authenticate(Credentials credentials) { Credentials = credentials; }
    private static async Task<HttpClient> Authenticate(string username, string password)
    {
        var cookies = new CookieContainer();
        var handler = new HttpClientHandler { CookieContainer = cookies };
        var client = new HttpClient(handler);

        await client.GetStringAsync($"{BaseUrl}/auth/familysearch/login?returnUrl={HttpUtility.UrlEncode(BaseUrl)}");
        var xsrfCookie = cookies.GetCookies(new Uri(IdentBaseUrl))["XSRF-TOKEN"]?.Value;
        if (xsrfCookie == null) throw new AuthenticationException("XSRF cookie wasn't found");
        var postData = new Dictionary<string, string>
        {
            { "username", username },
            { "password", password },
            { "_csrf", xsrfCookie }
        };
        var response = JObject.Parse(await client.PostAsync($"{IdentBaseUrl}/login", new FormUrlEncodedContent(postData)).Result.Content.ReadAsStringAsync());
        await client.GetStringAsync(response.Value<string>("redirectUrl"));
        return client;
    }

    public override async Task<RegistryInfo> GetRegistryFromUrlAsync(Uri url)
    {
        if (url.Host != "www.familysearch.org" || !url.AbsolutePath.StartsWith("/ark:/")) return null;

        var query = url.ParseQueryString();
        if (query["wc"] != null)
        {
            var wc = HttpUtility.UrlDecode(query["wc"]);
            var ccIndex = wc.IndexOf("?cc=", StringComparison.InvariantCultureIgnoreCase);
            return new RegistryInfo(this, ccIndex >= 0 ? wc.Remove(ccIndex) : wc) { PageNumber = int.TryParse(query["i"], out var prevPage) ? prevPage + 1 : 1 };
        }

        return null; // TODO
    }

    [AuthentificationNeeded]
    public override async Task<(Registry registry, int pageNumber)> Infos(Uri url)
    {
        var imageData = JObject.Parse(await (await GetClient()).PostAsJsonAsync($"{BaseUrl}/search/filmdata/filmdatainfo",
            new
            {
                type = "image-data",
                args = new
                {
                    imageURL = url.GetLeftPart(UriPartial.Path),
                    state = new { imageOrFilmUrl = "", viewMode = "i", selectedImageIndex = -1 },
                    locale = Locale
                }
            }).Result.Content.ReadAsStringAsync());
        var dgsNum = imageData.Value<string>("dgsNum");
        var meta = imageData.SelectToken("meta")!;
        var sourceDescriptions = NavigateSources(meta.SelectToken("sourceDescriptions")?.ToObject<List<SourceDescription>>(), meta.Value<string>("description")).ToList();

        var waypointURL = sourceDescriptions.Find(s => s.KnownResourceType == ResourceType.Collection)?.About;
        if (waypointURL == null) throw new NotImplementedException();
        var waypointData = JObject.Parse(await (await GetClient()).PostAsJsonAsync($"{BaseUrl}/search/filmdata/filmdatainfo",
            new
            {
                type = "waypoint-data",
                args = new
                {
                    dgsNum,
                    waypointURL,
                    state = new { imageOrFilmUrl = "", viewMode = "i", selectedImageIndex = -1 },
                    locale = Locale
                }
            }).Result.Content.ReadAsStringAsync());

        var waypointId = new Uri(waypointURL).AbsolutePath.Split('/')[^1];
        var crumbs = waypointData.SelectToken("waypointCrumbs")?.Select(c => c.Value<string>("title")).ToList() ??
                     sourceDescriptions
                         .Select(elem => (elem.Titles.Find(t => t.Lang == Locale) ?? elem.Titles.FirstOrDefault())?.Value)
                         .Reverse().ToList();
        var title = crumbs[^1];
        var titleDates = Regex.Matches(title, @"(?<from>\d{4})-(?<to>\d{4})").SelectMany(match => new[] { match.Groups["from"].Value, match.Groups["to"].Value }).Order().ToList();
        var registry = new Registry(this, waypointId)
        {
            Title = title,
            Location = crumbs[..^1],
            From = titleDates.Count > 0 ? Date.ParseDate(titleDates[0]) : null,
            To = titleDates.Count > 0 ? Date.ParseDate(titleDates[^1]) : null,
            Types = ParseTypes(title),
            URL = waypointURL,
            Frames = waypointData.SelectToken("images")?.Values<string>().Select((imageUrl, i) => new Frame
            {
                FrameNumber = i + 1,
                ArkUrl = imageUrl
            }).ToList() ?? new List<Frame>(),
            Extra = new Dictionary<string, object>
            {
                { "dgsNum", dgsNum },
                { "waypointId", waypointId },
                { "templates", waypointData.SelectToken("templates")?.ToObject<Dictionary<string, string>>() }
            }
        };

        return (registry, meta.SelectToken("links.self")?.Value<int>("offset") ?? 1);
    }

    /// <summary>Extracts registry content type from the waypoint crumb</summary>
    /// <param name="title">The waypoint crumb describing the current registry</param>
    /// <remarks>Only supports French and Italian</remarks>
    private static List<RegistryType> ParseTypes(string title)
    {
        var separators = new[] { ',', '-' };
        return Regex
            .Replace(title, @" (?<from>\d{4})-(?<to>\d{4})", ",")
            .Split(separators, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(part => part.ToLower() switch
            {
                "baptêmes" or "battesimi" => RegistryType.Baptism,
                "tables de baptêmes" => RegistryType.BaptismTable,
                "naissances" or "nati" => RegistryType.Birth,
                "pubblicazioni" => RegistryType.Banns,
                "mariages" or "matrimoni" => RegistryType.Marriage,
                "décès" or "morti" => RegistryType.Death,
                "sépultures" or "sepolture" => RegistryType.Burial,
                _ => RegistryType.Unknown
            }).Where(type => type != RegistryType.Unknown).ToList();
    }

    private static IEnumerable<SourceDescription> NavigateSources(List<SourceDescription> sources, string id)
    {
        while (id != null)
        {
            var idToFind = id;
            var elem = sources.Find(d => d.Id == idToFind);
            yield return elem;
            id = elem.ComponentOf?.DescriptionRef.TrimStart('#');
        }
    }

    [AuthentificationNeeded]
    public override async Task<Stream> GetFrame(Frame page, Scale scale, Action<Progress> progress)
    {
        var stream = await Data.TryGetImageFromDrive(page, scale);
        if (stream != null) return stream;
        progress?.Invoke(Progress.Unknown);

        if (page.Registry?.Extra is not Dictionary<string, object> data)
            if (page.Registry?.Extra is JObject obj) data = obj.ToObject<Dictionary<string, object>>();
            else return null;
        var templates = data["templates"] as Dictionary<string, string> ?? (data["templates"] is JObject jTemplates ? jTemplates.ToObject<Dictionary<string, string>>() : null);
        if (templates is null) return null;
        var urlTemplate = templates["dzTemplate"];
        var imageId = new Uri(page.ArkUrl!).AbsolutePath.Split('/')[^1];
        var baseUrl = urlTemplate.Replace("{id}", imageId).Replace("/{image}", "");

        var client = await GetClient();
        Image image;
        progress?.Invoke(0);
        if (scale == Scale.Full) image = await Grabber.GetImage($"{baseUrl}/dist.jpg", client);
        else
        {
            var format = page.Extra as string ?? "jpg";
            if (!page.TileSize.HasValue) (page.Width, page.Height, page.TileSize, page.Extra) = await DeepZoom.ImageData(baseUrl, client);
            var maxZoom = DeepZoom.CalculateIndex(page);
            var scaleZoom = scale switch
            {
                Scale.Thumbnail => DeepZoom.CalculateIndex(512, 512),
                Scale.Navigation => DeepZoom.CalculateIndex(4096, 4096),
                _ => 1
            };
            var zoom = Math.Min(scaleZoom, maxZoom);
            var (tiles, divider) = DeepZoom.GetTilesNumber(page, zoom);

            image = new Image<Rgb24>(page.Width!.Value / divider, page.Height!.Value / divider);
            var tasks = new Dictionary<Task<Image>, (int tileSize, int scale, Point pos)>();
            for (var y = 0; y < tiles.Y; y++)
                for (var x = 0; x < tiles.X; x++)
                    tasks.Add(Grabber.GetImage($"{baseUrl}/image_files/{zoom}/{x}_{y}.{format}", client).ContinueWith(task =>
                    {
                        try { progress?.Invoke(tasks.Keys.Count(t => t.IsCompleted) / (float)tasks.Count); }
                        catch (Exception e) { Log.Warning(e, "Error while updating the progress bar"); }
                        return task.Result;
                    }), (page.TileSize.GetValueOrDefault(), 1, new Point(x, y)));

            await Task.WhenAll(tasks.Keys).ConfigureAwait(false);
            image = tasks.Aggregate(image, (current, tile) => current.MergeTile(tile.Key.Result, tile.Value));
        }
        page.ImageSize = scale;
        progress?.Invoke(Progress.Finished);

        await Data.SaveImage(page, image, false).ConfigureAwait(false);
        return image.ToStream();
    }
}
