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
    public class AD79_86 : Ligeo
    {
        public override string Id => "AD79-86";
        protected override string Host => "archives-deux-sevres-vienne.fr";

        public AD79_86(HttpClient client = null) : base(client) { }


        public override async Task<(Registry, int)> Infos(Uri url)
        {
            var queries = Regex.Match(url.AbsolutePath, "/ark:/(?<something>.*?)/(?<id>.*?)/daogrp/(?<seq>\\d*?)/((?<page>\\d*?)/)?").Groups;
            var registry = new Registry(this, queries["id"].Value);
            registry.URL = $"https://archives-deux-sevres-vienne.fr/ark:/{queries["something"]?.Value}/{registry.Id}";

            var iiif = new LigeoManifest(await HttpClient.GetStringAsync($"{registry.URL}/manifest"));
            int.TryParse(queries["seq"]?.Value, out var seq);
            var sequence = iiif.Sequences.ElementAt(seq);

            registry.Frames = sequence.Canvases.Select((p, i) => new Frame()
            {
                FrameNumber = int.TryParse(p.Label, out var number) ? number : (i + 1),
                ArkUrl = p.Images.First().ServiceId,
                DownloadUrl = p.Images.First().Id,
                Extra = p.Classeur
            }).ToArray();

            var dates = sequence.Label.Split(new[] { "- (" }, StringSplitOptions.RemoveEmptyEntries)[^1].Replace(") ", "").Split('-');
            registry.From = Date.ParseDate(dates[0]);
            registry.To = Date.ParseDate(dates[^1]);
            registry.Types = ParseTypes(iiif.MetaData["Type de document"]).ToArray();
            registry.CallNumber = iiif.MetaData["Cote"];
            registry.Notes = GenerateNotes(iiif.MetaData);
            var location = new List<string> { iiif.MetaData["Commune"] };
            if (iiif.MetaData.TryGetValue("Paroisse", out var paroisse)) location.Add(paroisse);
            registry.Location = location.ToArray();
            registry.ArkURL = sequence.Id;

            return (registry, int.TryParse(queries["page"].Value, out var page) ? page : 1);
        }
        private static string GenerateNotes(IReadOnlyDictionary<string, string> metaData)
        {
            var notes = new List<string>();
            if (metaData.TryGetValue("Documents de substitution", out var docSub)) notes.Add($"Documents de substitution: {docSub}");
            if (metaData.TryGetValue("Présentation du contenu", out var presentation)) notes.Add(presentation);
            return notes.Count == 0 ? null : string.Join("\n\n", notes);
        }
        private static IEnumerable<RegistryType> ParseTypes(string types)
        {
            //TODO: Rewrite this function
            foreach (var type in types.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (type == "naissance") yield return RegistryType.Birth;
                else if (type == "mariage") yield return RegistryType.Marriage;
                else if (type == "décès") yield return RegistryType.Death;
                else if (type == "table décennale")
                {
                    yield return RegistryType.BirthTable;
                    yield return RegistryType.MarriageTable;
                    yield return RegistryType.DeathTable;
                }
            }
        }

        protected override Uri GetImageRequestUri(Frame page, Scale scale)
            => scale == Scale.Full && page.DownloadUrl != null ? new Uri(page.DownloadUrl) : ImageGeneratorRequestUri(page.ArkUrl, size: GetRequestImageSize(ref scale, page));
    }
}
