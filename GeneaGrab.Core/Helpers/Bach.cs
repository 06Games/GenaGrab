using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using GeneaGrab.Core.Models;
using GeneaGrab.Core.Models.Dates;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GeneaGrab.Core.Helpers;

/// <summary>
/// Bach by Anaphore
/// <a href="https://www.anaphore.eu/project/bach/">Web site</a>
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public abstract class Bach : Provider
{
    protected abstract string BaseUrl { get; }
    protected HttpClient HttpClient { get; } = new();


    #region Provider Implementation

    public override string Url => BaseUrl;

    public override async Task<RegistryInfo> GetRegistryFromUrlAsync(Uri url)
    {
        var (_, _, info) = await RetrieveInfoFromUrl(url);
        return new RegistryInfo(this, info.Remote.EncodedArchivalDescription.DocId) { PageNumber = info.Position ?? 1 };
    }

    public override Task<(Registry, int)> Infos(Uri url) => RetrieveViewerInfo(url);

    public override Task<string> Ark(Frame page) => Task.FromResult(PageViewerUrl(GetSeriesInfo(page.Registry), page.DownloadUrl));

    public override async Task<Stream> GetFrame(Frame page, Scale zoom, Action<Progress> progress)
    {
        var stream = await Data.TryGetImageFromDrive(page, zoom);
        if (stream != null) return stream;

        progress?.Invoke(Progress.Unknown);
        var image = await Grabber.GetImage(PageImageUrl(GetSeriesInfo(page.Registry), page.DownloadUrl, zoom), HttpClient).ConfigureAwait(false);
        page.ImageSize = zoom;
        progress?.Invoke(Progress.Finished);

        await Data.SaveImage(page, image, false).ConfigureAwait(false);
        return image.ToStream();
    }

    #endregion


    protected string DocUrl(string docId) => $"{BaseUrl}/archives/show/{docId}";
    protected string DocInfoUrl(string docId) => $"{DocUrl(docId)}/ajax";
    protected string PageViewerUrl(BachRegistryExtras series, string page) => $"{BaseUrl}/viewer/{(series.IsSeries ? "series" : "viewer")}/{series.Path}?img={page}";
    protected string PageInfoUrl(BachRegistryExtras series, string page) => $"{BaseUrl}{series.AppUrl}/ajax/{(series.IsSeries ? "series" : "image")}/infos/{series.Path}/{page}";
    protected string PageImageUrl(BachRegistryExtras series, string page, Scale size = Scale.Navigation)
    {
        var sizeTxt = size switch
        {
            Scale.Thumbnail => "thumb",
            Scale.Navigation => "default",
            Scale.Full => "full",
            _ => "default"
        };
        return $"{BaseUrl}{series.AppUrl}/show/{sizeTxt}/{series.Path}/{page}";
    }

    protected static BachRegistryExtras GetSeriesInfo(Registry registry)
    {
        if (registry.Extra is JObject obj)
            registry.Extra = obj.ToObject<BachRegistryExtras>();
        return registry.Extra as BachRegistryExtras;
    }

    protected static (string path, string page) ParseViewerUrl(Uri url)
    {
        return (Regex.Match(url.AbsolutePath, @"/viewer/\w*?/(?<path>.*?)").Groups.TryGetValue("path"), HttpUtility.ParseQueryString(url.Query).Get("img"));
    }
    protected async Task<BachSerieInfo> RetrievePageInfo(BachRegistryExtras series, string page)
    {
        var jsonUrl = PageInfoUrl(series, page);
        return JsonConvert.DeserializeObject<BachSerieInfo>(await HttpClient.GetStringAsync(jsonUrl));
    }

    protected static (BachRegistryExtras series, string[] pages) ParseViewerPage(string webpage)
    {
        var regex = Regex.Match(webpage, @"var series_content =.*?parseJSON\('(\["""")?(?<series_content>.*?)(""""\])?'\);", RegexOptions.Singleline).Groups;
        var pages = regex.TryGetValue("series_content");
        var imgPath = GetVariable("image_path");
        return (new BachRegistryExtras
        {
            AppUrl = GetVariable("app_url"),
            Path = imgPath ?? GetVariable("series_path"),
            IsSeries = imgPath == null
        }, pages == "null" ? new[] { Regex.Match(webpage, @"imageName: '(?<img>.*?)'").Groups.TryGetValue("img") } : pages.Split(","));

        string GetVariable(string variableName) => Regex.Match(webpage, $"var {variableName} = '(?<var>.*?)';").Groups.TryGetValue("var");
    }

    protected static (Date from, Date to) ParseDateFromDocPage(string docWebPage)
    {
        var dates = Regex.Match(docWebPage, "<section property=\"dc:date\" content=\"(?<dates>.*?)\">").Groups.TryGetValue("dates")?
            .Split('/').Select(Date.ParseDate).ToArray();
        return (dates?.FirstOrDefault(), dates?.LastOrDefault());
    }
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    protected static Dictionary<string, string[]> ParsePhysDescFromDocPage(string docWebPage) => ParseKeyValues(docWebPage,
        @"<span><h4>(?<key>[^<>]*?)( :)?<\/h4> (?<value>[^<>]*?)\.?<\/span>",
        @"<section class=""physdesc"">.*?<\/header>(?<dico>.*?)</section>");
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    protected static Dictionary<string, string[]> ParseLegalFromDocPage(string docWebPage)
        => ParseKeyValues(docWebPage, @"<section class=""accessrestrict"">\s*<header><h3>(?<key>[^<>]*?)</h3><\/header>\s*?<section .*?>(?<value>[^<>]*?)</section>\s*?</section>");
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    protected static Dictionary<string, string[]> ParseAltFormFromDocPage(string docWebPage)
        => ParseKeyValues(docWebPage, @"<section class=""altformavail"">\s*<header><h3>(?<key>[^<>]*?)</h3><\/header>\s*<p>(?<value>[^<>]*?)</p>.*?</section>");
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    protected static Dictionary<string, string[]> ParseDescriptorsFromDocPage(string docWebPage) => ParseKeyValues(docWebPage,
        @"<div>.*?<strong>(?<key>[^<>]*?)( :)?<\/strong> (<a.*?>(?<value>[^<>]*?)\.?</a>( • )?)+.*?<\/div>",
        @"<section class=""controlaccess"">.*?<\/header>(?<dico>.*?)</section>");
    protected static Dictionary<string, string[]> ParseKeyValues(string docWebPage, string pattern, string sectionPattern = null, RegexOptions options = RegexOptions.Singleline)
    {
        var section = sectionPattern == null ? docWebPage : Regex.Match(docWebPage, sectionPattern, options).Groups.TryGetValue("dico");
        if (section == null) return new Dictionary<string, string[]>();
        return Regex.Matches(section, pattern, options)
            .Select(match => match.Groups)
            .GroupBy(kv => kv.TryGetValue("key"), kv => kv["value"].Captures)
            .ToDictionary(kv => kv.Key, kv => kv.SelectMany(values => values.Select(v => v.Value)).ToArray());
    }
    protected static Dictionary<string, string[]> ParseDocPage(string docWebPage)
        => ParsePhysDescFromDocPage(docWebPage)
            .Union(ParseLegalFromDocPage(docWebPage))
            .Union(ParseAltFormFromDocPage(docWebPage))
            .Union(ParseDescriptorsFromDocPage(docWebPage))
            .ToDictionary(x => x.Key, x => x.Value);

    protected static (string placeInCity, string city, string[] cityLocation) ParsePlace(Dictionary<string, string[]> docPageInfo)
    {
        string place;
        if (!docPageInfo.TryGetValue("Lieu", out var places) || (place = places.FirstOrDefault()) == null) return (null, null, null);
        var regex = Regex.Match(place, @"^(?<city>[^(]*) \((?<position>[^(]*)\)( +-+ +(?<details>.+))?$").Groups;
        return (regex.TryGetValue("details"), regex.TryGetValue("city"), regex.TryGetValue("position")?.Split(", ").Reverse().ToArray());
    }

    protected async Task<(BachRegistryExtras series, string[] pages, BachSerieInfo info)> RetrieveInfoFromUrl(Uri url)
    {
        var page = ParseViewerUrl(url).page;
        var webpage = await HttpClient.GetStringAsync(url.OriginalString);
        var (series, pages) = ParseViewerPage(webpage);

        var info = await RetrievePageInfo(series, string.IsNullOrEmpty(page) ? pages.FirstOrDefault() : page);
        return (series, pages, info);
    }

    protected async Task<(Registry, int)> RetrieveViewerInfo(Uri url)
    {
        var (series, pages, info) = await RetrieveInfoFromUrl(url);
        var ead = info.Remote.EncodedArchivalDescription;
        var docWebPage = await HttpClient.GetStringAsync(DocInfoUrl(ead.DocId));
        var (from, to) = ParseDateFromDocPage(docWebPage);
        var docPageInfo = ParseDocPage(docWebPage);
        var (placeInCity, city, cityLocation) = ParsePlace(docPageInfo);

        var location = new List<string>(cityLocation);
        if (!string.IsNullOrEmpty(city)) location.Add(city);
        if (!string.IsNullOrEmpty(placeInCity)) location.Add(placeInCity);

        var registry = new Registry(this, ead.DocId)
        {
            URL = ead.DocLink,
            Types = GetTypes(docPageInfo).SelectMany(ParseTypes).ToArray(),
            CallNumber = ead.UnitId,
            Title = ead.UnitTitle,
            Author = docPageInfo.TryGetValue("Auteur", out var authors) && docPageInfo.Remove("Auteur") ? string.Join(", ", authors) : null,
            Location = location,
            From = from,
            To = to,
            Notes = string.Join("\n", docPageInfo.Select(kv => $"{kv.Key}: {string.Join(", ", kv.Value)}")),
            Frames = pages.Select((pageImage, pageIndex) => new Frame
            {
                FrameNumber = pageIndex + 1,
                DownloadUrl = pageImage
            }).ToArray(),
            Extra = series
        };
        return (registry, info.Position ?? 1);
    }

    protected IEnumerable<string[]> GetTypes(Dictionary<string, string[]> docPageInfo)
    {
        if (docPageInfo.TryGetValue("Typologie documentaire", out var typologie)) yield return typologie;
        if (docPageInfo.TryGetValue("Mot matière thésaurus", out var thesaurus)) yield return thesaurus;
    }
    protected IEnumerable<RegistryType> ParseTypes(IEnumerable<string> thesaurus) => thesaurus.Select(ParseTag).Where(type => type != RegistryType.Unknown);
    protected abstract RegistryType ParseTag(string tag);

    #region API Models

    protected class BachRegistryExtras
    {
        public string AppUrl { get; init; }
        public string Path { get; init; }
        public bool IsSeries { get; init; }
    }

    [SuppressMessage("ReSharper", "StringLiteralTypo"), SuppressMessage("ReSharper", "IdentifierTypo")]
    protected class BachSerieInfo
    {
        [JsonProperty("path")] public string Path { get; set; }
        [JsonProperty("current")] public string Current { get; set; }
        [JsonProperty("next")] public string Next { get; set; }
        [JsonProperty("tennext")] public string TenNext { get; set; }
        [JsonProperty("prev")] public string Previous { get; set; }
        [JsonProperty("tenprev")] public string TenPrevious { get; set; }
        [JsonProperty("count")] public int? PageCount { get; set; }
        [JsonProperty("position")] public int? Position { get; set; }
        [JsonProperty("remote")] public BachRemote Remote { get; set; }
    }
    protected class BachRemote
    {
        [JsonProperty("cookie")] public string Cookie { get; set; }
        [JsonProperty("ead")] public BachEncodedArchivalDescription EncodedArchivalDescription { get; set; }
        [JsonProperty("archivist")] public bool? Archivist { get; set; }
        [JsonProperty("reader")] public bool? Reader { get; set; }
        [JsonProperty("communicability")] public bool? Communicability { get; set; }
        [JsonProperty("isCommunicabilitySalleLecture")] public bool? CommunicabilityReadingRoom { get; set; }
    }
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    protected class BachEncodedArchivalDescription
    {
        [JsonProperty("link")] public string Breadcrumb { get; set; }
        [JsonProperty("unitid")] public string UnitId { get; set; }
        [JsonProperty("cUnittitle")] public string UnitTitle { get; set; }
        [JsonProperty("doclink")] public string DocALink { get; set; }
        [JsonIgnore] public string DocLink => Regex.Match(DocALink, "href=\"(?<url>.*?)\"").Groups.TryGetValue("url");
        [JsonIgnore] public string DocId => DocLink.Split('/').LastOrDefault();
        [JsonProperty("communicability_general")] public bool? Communicability { get; set; }
        [JsonProperty("communicability_sallelecture")] public bool? CommunicabilityReadingRoom { get; set; }
        [JsonProperty("cAudience")] public bool? CAudience { get; set; }
        [JsonProperty("audience")] public bool? Audience { get; set; }
    }

    #endregion
}
