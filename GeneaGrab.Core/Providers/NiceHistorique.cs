using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using GeneaGrab.Core.Helpers;
using GeneaGrab.Core.Models;
using GeneaGrab.Core.Models.Dates;

namespace GeneaGrab.Core.Providers
{
    public class NiceHistorique : Provider
    {
        public override string Id => "NiceHistorique";
        public override string Url => "https://www.nicehistorique.org/";

        public override async Task<RegistryInfo> GetRegistryFromUrlAsync(Uri url)
        {
            if (url.Host != "www.nicehistorique.org" || !url.AbsolutePath.StartsWith("/vwr")) return null;

            //TODO: Find a way to do this without having to make a request
            var (registry, page) = await Infos(url);
            return new RegistryInfo(registry) { PageNumber = page };
        }

        public override async Task<(Registry, int)> Infos(Uri url)
        {
            var client = new HttpClient();
            var pageBody = await client.GetStringAsync(url).ConfigureAwait(false);

            var data = Regex.Match(pageBody, @"<h2>R&eacute;f&eacute;rence :  (?<title>.* (?<number>\d*)) de l'ann&eacute;e (?<year>\d*).*<\/h2>").Groups;
            var date = Date.ParseDate(data["year"].Value);

            var pageData = Regex.Match(pageBody, "var pages = Array\\((?<pages>.*)\\);\\n.*var path = \"(?<path>.*)\";").Groups;
            Uri.TryCreate(url, pageData["path"].Value, out var path);
            var pages = pageData["pages"].Value.Split(", ");

            var pagesTable = Regex.Matches(pageBody, "<a href=\"#\" class=\"(?<class>.*)\" onclick=\"doc\\.set\\('(?<index>\\d*)'\\); return false;\" title=\".*\">(?<number>\\d*)<\\/a>").ToArray();

            var registry = new Registry(this, data["number"].Value)
            {
                URL = url.OriginalString,
                Types = new[] { RegistryType.Periodical },
                CallNumber = HttpUtility.HtmlDecode(data["title"].Value),
                From = date,
                To = date,
                Frames = pagesTable.Select(page =>
                {
                    var pData = HttpUtility.UrlDecode(pages[int.Parse(page.Groups["index"].Value) - 1]).Trim('"', ' ');
                    return new Frame
                    {
                        FrameNumber = int.Parse(page.Groups["number"].Value),
                        DownloadUrl = $"{path?.AbsoluteUri}{pData}"
                    };
                }).ToArray()
            };

            return (registry, int.Parse(Array.Find(pagesTable, p => p.Groups["class"].Value == "current")?.Groups.TryGetValue("index") ?? "1"));
        }


        public override Task<string> Ark(Frame page) => Task.FromResult($"p{page.FrameNumber}");

        public override async Task<Stream> GetFrame(Frame page, Scale scale, Action<Progress> progress)
        {
            var stream = await Data.TryGetImageFromDrive(page, scale);
            if (stream != null) return stream;

            progress?.Invoke(Progress.Unknown);
            var client = new HttpClient();
            var image = await Grabber.GetImage(page.DownloadUrl, client).ConfigureAwait(false);
            page.ImageSize = scale;
            progress?.Invoke(Progress.Finished);

            await Data.SaveImage(page, image, false).ConfigureAwait(false);
            return image.ToStream();
        }
    }
}
