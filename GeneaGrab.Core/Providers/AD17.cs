using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using GeneaGrab.Core.Helpers;
using GeneaGrab.Core.Models;
using GeneaGrab.Core.Models.Dates;
using Serilog;

namespace GeneaGrab.Core.Providers
{
    public class AD17 : Provider
    {
        public override string Id => "AD17";
        public override string Url => "https://www.archinoe.net/v2/ad17/registre.html";

        public override Task<RegistryInfo> GetRegistryFromUrlAsync(Uri url)
        {
            if (url.Host != "www.archinoe.net" || !url.AbsolutePath.StartsWith("/v2/ad17/")) return Task.FromResult<RegistryInfo>(null);

            var query = HttpUtility.ParseQueryString(url.Query);
            return Task.FromResult(new RegistryInfo(this, query["id"]) { PageNumber = int.TryParse(query["page"], out var p) ? p : 1 });
        }

        public override async Task<(Registry, int)> Infos(Uri uri)
        {
            var url = HttpUtility.UrlDecode(uri.OriginalString);

            var client = new HttpClient();
            var pageBody = await client.GetStringAsync(url).ConfigureAwait(false);

            var query = HttpUtility.ParseQueryString(uri.Query);
            var pages = Regex.Matches(pageBody, @"<img src="".*?"" width=""1px"" height=""1px"" id=""visu_image_(?<num>\d*?)""(.|\n)*?data-original=""(?<original>.*?)"".*?\/>")
                .Cast<Match>(); // https://regex101.com/r/muCsZx/2
            if (!int.TryParse(query["page"], out var pageNumber)) pageNumber = 1;

            var infos = Regex.Match(query["infos"],
                    @"<option value=\""(?<id>\d*?)\"".*?>(?<cote>.*?) - (?<commune>.*?) - (?<collection>.*?) - (?<type>.*?) - (?<actes>.*?) - (?<date_debut>.*?)( - (?<date_fin>.*?))?</option>")
                .Groups; // https://regex101.com/r/Ju2Y1b/3
            if (infos.Count == 0)
                infos = Regex.Match(pageBody, @"<form method=\""get\"">.*<option value=\""\"">(?<cote>.*?) - (?<date_debut>.*?)( - (?<date_fin>.*?)?)</option>",
                    RegexOptions.Multiline | RegexOptions.Singleline).Groups; // https://regex101.com/r/Ju2Y1b/3
            if (infos.Count == 0) return (null, -1);

            var registry = new Registry(this, query["id"] ?? infos["id"].Value)
            {
                URL = url,
                CallNumber = infos["cote"].Value,
                Location = new[] { infos["commune"].Value },
                Notes = infos["type"].Success ? $"{infos["type"].Value}: {infos["collection"].Value}" : null,
                From = Date.ParseDate(infos["date_debut"].Value),
                Types = GetTypes(infos["type"]?.Value, infos["actes"]?.Value).ToArray(),
                Frames = pages.Select((p, i) => new Frame { FrameNumber = i + 1, DownloadUrl = p.Groups["original"].Value }).ToArray()
            };
            registry.To = Date.ParseDate(infos["date_fin"].Value) ?? registry.From;

            IEnumerable<RegistryType> GetTypes(string category, string type)
            {
                if (type.Contains("Naissances")) yield return RegistryType.Birth;
                if (type.Contains("Baptêmes")) yield return RegistryType.Baptism;

                var bannsIndex = type.IndexOf("Publications de Mariages", StringComparison.InvariantCulture);
                if (bannsIndex >= 0)
                {
                    yield return RegistryType.Banns;

                    //Since the term "Mariages" is both a type in itself and a part of "Publications de Mariages", we have to check if there is another occurrence of the term
                    var pos = -1;
                    while ((pos = type.IndexOf("Mariages", pos + 1, StringComparison.InvariantCulture)) != -1)
                    {
                        if (pos == bannsIndex + "Publications de ".Length) continue; // We are in the case where the term "Mariages" belongs to the expression "Publications de Mariages".
                        yield return RegistryType.Marriage;
                        break;
                    }
                }
                else if (type.Contains("Mariages")) yield return RegistryType.Marriage; // There are no "Publications de Mariages", so the term corresponds to the type itself
                if (type.Contains("Divorces")) yield return RegistryType.Divorce;

                if (type.Contains("Décès")) yield return RegistryType.Death;
                if (type.Contains("Sépultures")) yield return RegistryType.Burial;

                if (type.Contains("Tables décennales des naissances")) yield return RegistryType.BirthTable;
                else if (type.Contains("Tables décennales des mariages")) yield return RegistryType.MarriageTable;
                else if (type.Contains("Tables décennales des décès")) yield return RegistryType.DeathTable;
                else if (type.Contains("Tables décennales") && category == "Etat civil")
                {
                    yield return RegistryType.BirthTable;
                    yield return RegistryType.MarriageTable;
                    yield return RegistryType.DeathTable;
                }
                else if (type.Contains("Tables décennales"))
                {
                    yield return RegistryType.BaptismTable;
                    yield return RegistryType.MarriageTable;
                    yield return RegistryType.BurialTable;
                }
            }

            return (registry, pageNumber);
        }

        public override async Task<string> Ark(Frame page)
        {
            if (page.ArkUrl != null) return page.ArkUrl;

            var client = new HttpClient();
            var registry = page.Registry;
            var desc = registry is null ? null : $"{registry.CallNumber} - {registry.Location} - {registry.From} - {registry.To}".Replace(' ', '+');
            var ark = await client.GetStringAsync($"https://www.archinoe.net/v2/ark/permalien.html?chemin={page.DownloadUrl}&desc={desc}&id={page.RegistryId}&ir=&vue=1&ajax=true")
                .ConfigureAwait(false);
            var link = Regex.Match(ark, @"<textarea id=\""inputpermalien\"".*?>(?<link>http.*?)<\/textarea>").Groups["link"]?.Value;

            if (string.IsNullOrWhiteSpace(link))
            {
                Log.Error("AD17: Couldn't parse ark url ({Ark})", ark);
                return $"p{page.FrameNumber}";
            }

            page.ArkUrl = link;
            return link;
        }
        public override async Task<Stream> GetFrame(Frame page, Scale zoom, Action<Progress> progress)
        {
            var stream = await Data.TryGetImageFromDrive(page, zoom);
            if (stream != null) return stream;

            progress?.Invoke(Progress.Unknown);
            var client = new HttpClient();

            string generate = null;
            if (page.Width < 1 || page.Height < 1)
            {
                generate = await client.GetStringAsync($"https://www.archinoe.net/v2/images/genereImage.html?r=0&n=0&b=0&c=0&o=IMG&id=visu_image_${page.FrameNumber}&image={page.DownloadUrl}")
                    .ConfigureAwait(false);
                var data = generate.Split('\t');
                page.Width = int.TryParse(data[4], out var w) ? w : 0;
                page.Height = int.TryParse(data[5], out var h) ? h : 0;
            }

            var (wantedW, wantedH) = zoom switch
            {
                Scale.Thumbnail => (512, 512),
                Scale.Navigation => (2048, 2048),
                Scale.Full => (page.Width!.Value, page.Height!.Value),
                _ => (2048, 2048)
            };
            if (Math.Max(wantedW, wantedH) > 1800 || generate is null)
                generate = await client
                    .GetStringAsync(
                        $"https://www.archinoe.net/v2/images/genereImage.html?l={page.Width}&h={page.Height}&x=0&y=0&r=0&n=0&b=0&c=0&o=TILE&id=tuile_20_2_2_3&image={page.DownloadUrl}&ol={wantedW}&oh={wantedH}")
                    .ConfigureAwait(false);

            //We can't track the progress because we don't know the final size
            var image = await Grabber.GetImage($"https://www.archinoe.net{generate.Split('\t')[1]}", client).ConfigureAwait(false);
            page.ImageSize = zoom;
            progress?.Invoke(Progress.Finished);

            await Data.SaveImage(page, image, false).ConfigureAwait(false);
            return image.ToStream();
        }
    }
}
