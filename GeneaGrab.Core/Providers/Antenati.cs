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
using SixLabors.ImageSharp;

namespace GeneaGrab.Core.Providers
{
    public class Antenati : Provider
    {
        public override string Id => "Antenati";
        public override string Url => $"https://{FrontDomain}/";



        private const string FrontDomain = "antenati.cultura.gov.it";
        private const string ApiDomain = "dam-antenati.cultura.gov.it";

        private HttpClient _httpClient;
        private HttpClient HttpClient
        {
            get
            {
                if (_httpClient != null) return _httpClient;

                _httpClient = new HttpClient();
                _httpClient.DefaultRequestHeaders.Add("Referer", Url);
                return _httpClient;
            }
        }

        private static readonly string[] NotesMetadata = { "Conservato da", "Lingua" };

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
                    var response = await client.GetStringAsync(url);
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

        private static string ExtractDetailFromHtmlHeader(string html, string key)
            => Regex.Match(html, @$"<p>\s*<strong>{key}:</strong>\s*(<.*?>\s*)*(?<value>\S*?)\s*(</.*?>\s*)*</p>").Groups.TryGetValue("value");

        public override async Task<(Registry, int)> Infos(Uri url)
        {
            var (registryId, registrySignature) = await RetrieveRegistryInfosFromPage(url);
            var registry = new Registry(this, registryId)
            {
                URL = $"https://{ApiDomain}/antenati/containers/{registryId}",
                CallNumber = registrySignature
            };

            var iiif = new Iiif(await HttpClient.GetStringAsync($"{registry.URL}/manifest"));
            registry.Frames = iiif.Sequences.First().Canvases.Select(p => new Frame
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

            return (registry, 1);
        }

        private static IEnumerable<RegistryType> ParseTypes(IEnumerable<string> types) => types.Select(type => type switch
        {
            "Nati" => RegistryType.Birth,
            "Matrimoni" => RegistryType.Marriage,
            "Morti" => RegistryType.Death,
            _ => RegistryType.Unknown
        }).Where(result => result != RegistryType.Unknown);

        public override Task<string> Ark(Frame page) => Task.FromResult(page.ArkUrl ?? $"{page.Registry?.ArkURL} (p{page.FrameNumber})");

        public override async Task<Stream> GetFrame(Frame page, Scale scale, Action<Progress> progress)
        {
            var stream = await Data.TryGetImageFromDrive(page, scale);
            if (stream != null) return stream;

            progress?.Invoke(Progress.Unknown);
            var size = scale switch
            {
                Scale.Thumbnail => "!512,512",
                Scale.Navigation => "!2048,2048",
                _ => "max"
            };
            var image = await Image.LoadAsync(await HttpClient.GetStreamAsync(Iiif.GenerateImageRequestUri(page.DownloadUrl, size: size)).ConfigureAwait(false)).ConfigureAwait(false);
            page.ImageSize = scale;
            progress?.Invoke(Progress.Finished);

            await Data.SaveImage(page, image, false).ConfigureAwait(false);
            return image.ToStream();
        }
    }
}
