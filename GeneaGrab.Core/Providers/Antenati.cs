using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GeneaGrab.Core.Helpers;
using GeneaGrab.Core.Models;
using GeneaGrab.Core.Models.Dates;

namespace GeneaGrab.Core.Providers
{
    public sealed class Antenati : Iiif
    {
        public override string Id => "Antenati";
        public override string Url => $"https://{FrontDomain}/";


        private const string FrontDomain = "antenati.cultura.gov.it";
        private const string ApiDomain = "dam-antenati.cultura.gov.it";

        private static readonly string[] NotesMetadata = { "Conservato da", "Lingua" };

        public Antenati(HttpClient client = null) : base(client) => HttpClient.DefaultRequestHeaders.Add("Referer", Url);

        private static string ExtractDetailFromHtmlHeader(string html, string key)
            => Regex.Match(html, @$"<p>\s*<strong>{key}:</strong>\s*(<.*?>\s*)*(?<value>\S*?)\s*(</.*?>\s*)*</p>").Groups.TryGetValue("value");

        private static async Task<(string registryId, string registrySignature)> RetrieveRegistryInfosFromPage(Uri url)
        {
            string registryId = null;
            string registrySignature = null;
            switch (url.Host)
            {
                case ApiDomain when url.AbsolutePath.StartsWith("/antenati/containers/"):
                    registryId = Regex.Match(url.AbsolutePath, "^/antenati/containers/(?<id>.*?)/").Groups.TryGetValue("id");
                    break;
                case FrontDomain when url.AbsolutePath.StartsWith("/ark:/"):
                    var client = new HttpClient();
                    var response = await client.GetStringAsync(url.GetLeftPart(UriPartial.Path));
                    registryId = Regex.Match(response, "let windowsId = '(?<firstPageId>.*?)';").Groups.TryGetValue("firstPageId");
                    registrySignature = ExtractDetailFromHtmlHeader(response, "Segnatura attuale");
                    break;
            }
            return (registryId, registrySignature);
        }

        public override async Task<RegistryInfo> GetRegistryFromUrlAsync(Uri url)
        {
            var (registryId, _) = await RetrieveRegistryInfosFromPage(url);
            return registryId == null ? null : new RegistryInfo(this, registryId) { FrameArkUrl = url.GetLeftPart(UriPartial.Path) };
        }

        public override async Task<(Registry, int)> Infos(Uri url)
        {
            var (registryId, registrySignature) = await RetrieveRegistryInfosFromPage(url);
            var registry = new Registry(this, registryId)
            {
                URL = $"https://{ApiDomain}/antenati/containers/{registryId}",
                CallNumber = registrySignature
            };

            var iiif = new IiifManifest(await HttpClient.GetStringAsync($"{registry.URL}/manifest"));
            registry.Frames = iiif.Sequences[0].Canvases.Select(p => new Frame
            {
                FrameNumber = int.Parse(p.Label.Substring("pag. ".Length)),
                DownloadUrl = p.Images.First().ServiceId,
                ArkUrl = p.Id
            }).ToArray();

            var dates = iiif.MetaData["Datazione"].Split(" - ", StringSplitOptions.RemoveEmptyEntries);
            registry.From = Date.ParseDate(dates[0]);
            registry.To = Date.ParseDate(dates[1]);
            registry.Types = ParseTypes(new[] { iiif.MetaData["Tipologia"] }).ToArray();
            var location = iiif.MetaData["Contesto archivistico"].Split(" > ", StringSplitOptions.RemoveEmptyEntries);
            registry.Location = location;
            registry.ArkURL = Regex.Match(iiif.MetaData["Vedi il registro"], "<a .*>(?<url>.*)</a>").Groups.TryGetValue("url");
            registry.Notes = string.Join('\n', NotesMetadata.Select(key => $"{key}: {iiif.MetaData[key]}"));

            return (registry, registry.Frames.FirstOrDefault(f => f.ArkUrl == url.GetLeftPart(UriPartial.Path))?.FrameNumber ?? 1);
        }

        private static IEnumerable<RegistryType> ParseTypes(IEnumerable<string> types) => types.Select(type => type switch
        {
            "Nati" => RegistryType.Birth,
            "Matrimoni" => RegistryType.Marriage,
            "Morti" => RegistryType.Death,
            _ => RegistryType.Unknown
        }).Where(result => result != RegistryType.Unknown);

        public override Task<string> Ark(Frame page) => Task.FromResult(page.ArkUrl ?? $"{page.Registry?.ArkURL} (p{page.FrameNumber})");
    }
}
