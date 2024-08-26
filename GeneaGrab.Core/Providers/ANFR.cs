using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using GeneaGrab.Core.Helpers;
using GeneaGrab.Core.Models;
using GeneaGrab.Core.Models.Dates;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace GeneaGrab.Core.Providers;

public class ANFR : Provider
{
    public override string Id => nameof(ANFR);
    public override string Url => "https://www.archives-nationales.culture.gouv.fr/";

    private HttpClient HttpClient { get; }
    public ANFR(HttpClient client = null) => HttpClient = client ?? new HttpClient();

    public override Task<RegistryInfo> GetRegistryFromUrlAsync(Uri url)
    {
        if (url.Host != "www.siv.archives-nationales.culture.gouv.fr" || (!url.AbsolutePath.StartsWith("/siv/UD/") && !url.AbsolutePath.StartsWith("/siv/media/")))
            return Task.FromResult<RegistryInfo>(null);

        var (irId, udId, numberImage) = ParseArkUrl(url);
        return Task.FromResult(new RegistryInfo(this, $"{irId}/{udId}") { FrameArkUrl = GetImageArkUrl(irId, udId, numberImage) });
    }

    public override async Task<(Registry registry, int pageNumber)> Infos(Uri url)
    {
        var (irId, udId, numberImage) = ParseArkUrl(url);
        var registry = await GetInfos(irId, udId);

        var carouselUri = await ExtractCarouselUri(url);
        if (registry == null)
        {
            var carouselUrlQuery = HttpUtility.ParseQueryString(carouselUri.Query);
            registry = new Registry(this, $"{irId}/{udId}")
            {
                URL = url.OriginalString,
                CallNumber = ReadQuery(carouselUrlQuery.Get("cote")),
                Title = ReadQuery(carouselUrlQuery.Get("udTitle")),
                Extra = new Dictionary<string, string>
                {
                    { "irId", irId },
                    { "udId", udId }
                }
            };
        }

        var carousel = await HttpClient.GetStringAsync(carouselUri);
        registry.Frames = GetFrames(irId, udId, carousel, carouselUri);

        var pageNumber = registry.Frames.FirstOrDefault(frame => (frame.Extra as Dictionary<string, string>)?.GetValueOrDefault("fileName") == numberImage)?.FrameNumber;
        return (registry, pageNumber ?? 1);
    }

    private async Task<Uri> ExtractCarouselUri(Uri page)
    {
        var pageBody = await HttpClient.GetStringAsync(page);
        var carouselUrl = Regex.Match(pageBody, @"\$\('#carousel_[^']*'\)\.load\('(?<carousel>\/siv\/rechercheconsultation\/consultation\/multimedia\/Carousel\.action[^']*)'\);")
            .Groups.TryGetValue("carousel");
        Uri.TryCreate(page, carouselUrl, out var carouselUri);
        return carouselUri;
    }

    private static (string irId, string udId, string numberImage) ParseArkUrl(Uri url)
    {
        var regex = Regex.Match(url.OriginalString, @"siv/(?>\w*)/(?<irId>\w*)(?>/(?<udId>\w*)(?>/(?<numberImage>\w*))?)?").Groups;
        return (regex.TryGetValue("irId"), regex.TryGetValue("udId"), regex.TryGetValue("numberImage"));
    }

    private static string GetImageArkUrl(string irId, string udId, string numberImage) => $"https://www.siv.archives-nationales.culture.gouv.fr/siv/media/{irId}/{udId}/{numberImage}";

    private async Task<Registry> GetInfos(string irId, string udId)
    {
        var xml = await HttpClient.GetStreamAsync($"https://www.siv.archives-nationales.culture.gouv.fr/siv/rechercheconsultation/consultation/ir/exportXML.action?irId={irId}");
        var xmlDoc = new XmlDocument();
        xmlDoc.Load(xml);

        var cNode = xmlDoc.SelectSingleNode($"//c[@id='{udId}']");
        var did = cNode?.SelectSingleNode("did");
        var unitDate = did?.SelectSingleNode("unitdate");
        if (unitDate == null) return null;

        var collections = new List<XmlNode>();
        for (var node = cNode.ParentNode; node != null; node = node.ParentNode)
            if (node.Name == "c")
                collections.Insert(0, node);

        var dates = unitDate.Value?.Split('-').Select(Date.ParseDate).Take(2).ToArray();
        var normalizedDates = unitDate.Attributes?["normal"]?.Value.Split('/').Select(Date.ParseDate).Take(2).ToArray();
        var registry = new Registry(this, $"{irId}/{udId}")
        {
            URL = $"https://www.siv.archives-nationales.culture.gouv.fr/siv/UD/{irId}/{udId}",
            Title = did.SelectSingleNode("unittitle")?.InnerText,
            CallNumber = did.SelectSingleNode("unitid")?.InnerText,
            From = dates?[0] ?? normalizedDates?[0],
            To = dates?[^1] ?? normalizedDates?[^1],
            Location = collections.Select(c => c.SelectSingleNode("did")?.SelectSingleNode("unittitle")?.InnerText).ToArray(),
            Extra = new Dictionary<string, string>
            {
                { "irId", irId },
                { "udId", udId }
            }
        };

        return registry;
    }

    private static Frame[] GetFrames(string irId, string udId, string carousel, Uri baseUrl)
    {
        return Regex.Matches(carousel, @"mycarousel_itemList\.push\({url: ""(?<url>.*?)"", vignetteSuffix: ""(?<vignette>.*?)"", downloadSuffix: ""(?<download>.*?)""}\);")
            .Select((match, i) =>
            {
                var groups = match.Groups;
                var url = groups.TryGetValue("url");
                var vignetteSuffix = groups.TryGetValue("vignette");
                var downloadSuffix = groups.TryGetValue("download");
                var suffixPosition = url.LastIndexOf(vignetteSuffix, StringComparison.InvariantCulture);
                var fileNamePosition = url.LastIndexOf('/') + 1;
                if (suffixPosition < 0 || fileNamePosition < 0 || suffixPosition < fileNamePosition) throw new FormatException($"Unexpected url format : {url}");
                var fileName = url.Substring(fileNamePosition, suffixPosition - fileNamePosition);
                var uri = new Uri(baseUrl, url[..suffixPosition]);

                return new Frame
                {
                    FrameNumber = i + 1,
                    DownloadUrl = uri.AbsoluteUri + '/',
                    ArkUrl = $"https://www.siv.archives-nationales.culture.gouv.fr/siv/media/{irId}/{udId}/{fileName}",
                    Extra = new Dictionary<string, string>
                    {
                        { "vignetteSuffix", vignetteSuffix },
                        { "downloadSuffix", downloadSuffix },
                        { "fileName", fileName }
                    }
                };
            }).ToArray();
    }

    private static string ReadQuery(string str)
    {
        if (str.StartsWith("=?UTF-8?B?") && str.EndsWith("?="))
            return Encoding.UTF8.GetString(Convert.FromBase64String(str.Substring(10, str.Length - 12)));
        return str;
    }

    public override async Task<Stream> GetFrame(Frame page, Scale scale, Action<Progress> progress)
    {
        var stream = await Data.TryGetImageFromDrive(page, scale);
        if (stream != null) return stream;

        progress?.Invoke(Progress.Unknown);

        Image image;
        if (scale == Scale.Thumbnail)
        {
            var extraDico = page.Extra as Dictionary<string, string> ?? new Dictionary<string, string>();
            var suffix = extraDico.GetValueOrDefault("vignetteSuffix", "-min");
            image = await Grabber.GetImage($"{page.DownloadUrl!.TrimEnd('/')}{suffix}.jpg", HttpClient).ConfigureAwait(false);
        } 
        else image = await ZoomifyImage(page, scale == Scale.Full ? 1 : 0.75, progress);

        page.ImageSize = scale;
        progress?.Invoke(Progress.Finished);
        await Data.SaveImage(page, image, false).ConfigureAwait(false);
        return image.ToStream();
    }

    private async Task<Image> ZoomifyImage(Frame page, double scale, Action<Progress> progress)
    {
        if (!page.TileSize.HasValue) (page.Width, page.Height, page.TileSize) = await Zoomify.ImageData(page.DownloadUrl, HttpClient);
        var maxZoom = Zoomify.CalculateIndex(page);
        var scaleZoom = maxZoom * scale;
        var zoom = Math.Min((int)Math.Ceiling(scaleZoom), maxZoom);
        var (tiles, diviser) = Zoomify.GetTilesNumber(page, zoom);

        progress?.Invoke(0);
        Image image = new Image<Rgb24>(page.Width!.Value / diviser, page.Height!.Value / diviser);
        var tasks = new Dictionary<Task<Image>, (int tileSize, int scale, Point pos)>();
        var completed = 0;
        for (var y = 0; y < tiles.Y; y++)
            for (var x = 0; x < tiles.X; x++)
                tasks.Add(Grabber.GetImage($"{page.DownloadUrl}TileGroup0/{zoom}-{x}-{y}.jpg", HttpClient).ContinueWith(task =>
                {
                    progress?.Invoke(++completed / (float)tasks.Count);
                    return task.Result;
                }), (page.TileSize.GetValueOrDefault(), diviser, new Point(x, y)));

        await Task.WhenAll(tasks.Keys).ConfigureAwait(false);
        return tasks.Aggregate(image, (current, tile) => current.MergeTile(tile.Key.Result, tile.Value));
    }
}
