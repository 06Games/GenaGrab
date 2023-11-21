﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using GeneaGrab.Core.Models;
using GeneaGrab.Core.Models.Dates;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GeneaGrab.Core.Helpers
{
    /// <summary>
    /// Bach by Anaphore
    /// <a href="https://www.anaphore.eu/project/bach/">Web site</a>
    /// </summary>
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    public abstract class Bach : Provider
    {
        protected abstract string BaseUrl { get; }
        public override string Url => BaseUrl;
        protected HttpClient HttpClient { get; } = new();

        protected enum ImageSize { Thumb, Default, Full }

        protected string DocUrl(string docId) => $"{BaseUrl}/archives/show/{docId}";
        protected string DocInfoUrl(string docId) => $"{DocUrl(docId)}/ajax";
        protected string PageViewerUrl(BachRegistryExtras series, string page) => $"{BaseUrl}/viewer/series/{series.Path}?img={page}";
        protected string PageInfoUrl(BachRegistryExtras series, string page) => $"{BaseUrl}{series.AppUrl}/ajax/series/infos/{series.Path}/{page}";
        protected string PageImageUrl(BachRegistryExtras series, string page, ImageSize size = ImageSize.Default)
            => $"{BaseUrl}{series.AppUrl}/show/{size.ToString().ToLower()}/{series.Path}/{page}";

        protected static BachRegistryExtras GetSeriesInfo(Registry registry)
        {
            if (registry.Extra is JObject obj)
                registry.Extra = obj.ToObject<BachRegistryExtras>();
            return registry.Extra as BachRegistryExtras;
        }

        protected static (string path, string page) ParseViewerUrl(Uri url)
        {
            var path = Regex.Match(url.AbsolutePath, @"/viewer/series/(?<path>.*?)").Groups["path"];
            var img = HttpUtility.ParseQueryString(url.Query).Get("img");
            return (path.Success ? path.Value : null, img);
        }
        protected async Task<BachSerieInfo> RetrievePageInfo(BachRegistryExtras series, string page)
        {
            var jsonUrl = PageInfoUrl(series, page);
            return JsonConvert.DeserializeObject<BachSerieInfo>(await HttpClient.GetStringAsync(jsonUrl));
        }

        protected static (BachRegistryExtras series, string[] pages) ParseViewerPage(string webpage)
        {
            var regex = Regex.Match(webpage,
                @"var series_path = '(?<series_path>.*?)';.*?var series_content =.*?parseJSON\('\[""(?<series_content>.*?)""\]'\);.*?var app_url = '(?<app_url>.*?)';",
                RegexOptions.Singleline).Groups;
            return (new BachRegistryExtras { AppUrl = regex.TryGetValue("app_url"), Path = regex.TryGetValue("series_path") }, regex.TryGetValue("series_content")?.Split("\",\""));
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

        protected async Task<RegistryInfo> RetrieveViewerInfo(Uri url)
        {
            var page = ParseViewerUrl(url).page;
            var webpage = await HttpClient.GetStringAsync(url.OriginalString);
            var (series, pages) = ParseViewerPage(webpage);

            var info = await RetrievePageInfo(series, string.IsNullOrEmpty(page) ? pages.FirstOrDefault() : page);
            var ead = info.Remote.EncodedArchivalDescription;

            var docWebPage = await HttpClient.GetStringAsync(DocInfoUrl(ead.DocId));
            var (from, to) = ParseDateFromDocPage(docWebPage);
            var docPageInfo = ParseDocPage(docWebPage);

            var registry = new Registry
            {
                URL = ead.DocLink,
                Types = null,
                ProviderID = Id,
                ID = ead.DocId,
                CallNumber = ead.UnitId,
                Title = ead.UnitTitle,
                Author = docPageInfo.TryGetValue("Personne", out var personne) && docPageInfo.Remove("Personne") ? string.Join(", ", personne) : null,
                From = from,
                To = to,
                Notes = string.Join("\n", docPageInfo.Select(kv => $"{kv.Key}: {string.Join(", ", kv.Value)}")),
                Pages = pages.Select((pageImage, pageIndex) => new RPage
                {
                    Number = pageIndex + 1,
                    URL = pageImage,
                    Extra = pageIndex + 1 == info.Position ? ead : null
                }).ToArray(),
                Extra = series
            };
            Data.AddOrUpdate(Data.Providers[Id].Registries, registry.ID, registry);
            return new RegistryInfo(registry) { PageNumber = info.Position };
        }

        public override async Task<RegistryInfo> Infos(Uri url)
        {
            var registryInfo = await RetrieveViewerInfo(url);
            return registryInfo;
        }
        public override Task<string> Ark(Registry registry, RPage page) => Task.FromResult(PageViewerUrl(GetSeriesInfo(registry), page.URL));



        protected class BachRegistryExtras
        {
            public string AppUrl { get; init; }
            public string Path { get; init; }
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
            [JsonProperty("count")] public int PageCount { get; set; }
            [JsonProperty("position")] public int Position { get; set; }
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
    }
}
