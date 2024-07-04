using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
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

        protected override Frame CreateFrame(IIiifCanvas p, int i)
        {
            var frame = base.CreateFrame(p, i);
            frame.ArkUrl = p.Images.FirstOrDefault()?.ServiceId;
            frame.DownloadUrl = p.Images.FirstOrDefault()?.Id;
            return frame;
        }

        protected override async Task ReadSequence(Registry registry, IIiifSequence sequence)
        {
            await base.ReadSequence(registry, sequence);

            var dates = sequence.Label.Split("- (", StringSplitOptions.RemoveEmptyEntries)[^1].Replace(") ", "").Split('-');
            registry.From = Date.ParseDate(dates[0]);
            registry.To = Date.ParseDate(dates[^1]);
        }

        protected override void ReadAllMetadata(ref Registry registry, ReadOnlyDictionary<string, string> manifestMetadata)
        {
            registry.Types = ParseTypes(manifestMetadata["Type de document"]).ToArray();
            registry.CallNumber = manifestMetadata["Cote"];
            registry.Notes = GenerateNotes(manifestMetadata);
            var location = new List<string> { manifestMetadata["Commune"] };
            if (manifestMetadata.TryGetValue("Paroisse", out var paroisse)) location.Add(paroisse);
            registry.Location = location.ToArray();
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
            foreach (var type in types.Split(", ", StringSplitOptions.RemoveEmptyEntries))
            {
                switch (type)
                {
                    case "naissance":
                        yield return RegistryType.Birth;
                        break;
                    case "mariage":
                        yield return RegistryType.Marriage;
                        break;
                    case "décès":
                        yield return RegistryType.Death;
                        break;
                    case "table décennale":
                        yield return RegistryType.BirthTable;
                        yield return RegistryType.MarriageTable;
                        yield return RegistryType.DeathTable;
                        break;
                }
            }
        }

        protected override Uri GetImageRequestUri(Frame page, Scale scale)
            => scale == Scale.Full && page.DownloadUrl != null ? new Uri(page.DownloadUrl) : ImageGeneratorRequestUri(page.ArkUrl, size: GetRequestImageSize(ref scale, page));
    }
}
