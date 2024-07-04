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


        protected override async Task<(Registry registry, int sequence, object page)> ParseUrl(Uri url)
        {
            var (registryId, registrySignature) = await RetrieveRegistryInfosFromPage(url);
            var registry = new Registry(this, registryId)
            {
                URL = $"https://{ApiDomain}/antenati/containers/{registryId}",
                CallNumber = registrySignature
            };
            return (registry, 0, url.GetLeftPart(UriPartial.Path));
        }

        protected override int GetPage(Registry registry, object page)
        {
            var pageUrl = page as string;
            return registry.Frames.FirstOrDefault(f => f.ArkUrl == pageUrl)?.FrameNumber ?? 1;
        }

        protected override void ParseMetaData(ref Registry registry, string key, string value)
        {
            switch (key)
            {
                case "Datazione":
                {
                    var dates = value.Split(" - ", StringSplitOptions.RemoveEmptyEntries);
                    registry.From = Date.ParseDate(dates[0]);
                    registry.To = Date.ParseDate(dates[1]);
                    break;
                }
                case "Tipologia":
                    registry.Types = ParseTypes(new[] { value }).ToArray();
                    break;
                case "Contesto archivistico":
                    registry.Location = value.Split(" > ", StringSplitOptions.RemoveEmptyEntries);
                    break;
                case "Vedi il registro":
                    registry.ArkURL = value;
                    break;
                default:
                {
                    if (NotesMetadata.Contains(key))
                        base.ParseMetaData(ref registry, key, value);
                    break;
                }
            }
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
