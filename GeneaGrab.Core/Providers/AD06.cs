using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GeneaGrab.Core.Helpers;
using GeneaGrab.Core.Models;
using Serilog;
using SixLabors.ImageSharp;

namespace GeneaGrab.Core.Providers
{
    public class AD06 : Provider
    {
        public override string Id => "AD06";
        public override string Url => "https://archives06.fr/";

        public override Task<RegistryInfo> GetRegistryFromUrlAsync(Uri url)
        {
            if (url.Host != "archives06.fr" || !url.AbsolutePath.StartsWith("/ark:/")) return Task.FromResult<RegistryInfo>(null);

            var queries = Regex.Match(url.AbsolutePath, @"/ark:/(?<something>[\w\.]+)(/(?<id>[\w\.]+))?(/(?<tag>[\w\.]+))?(/(?<seq>\d+))?(/(?<page>\d+))?").Groups;
            return Task.FromResult(new RegistryInfo(this, queries["id"].Value) { PageNumber = int.TryParse(queries["page"].Value, out var page) ? page : 1 });
        }

        public override async Task<(Registry, int)> Infos(Uri url)
        {
            var queries = Regex.Match(url.AbsolutePath, @"/ark:/(?<something>[\w\.]+)(/(?<id>[\w\.]+))?(/(?<tag>[\w\.]+))?(/(?<seq>\d+))?(/(?<page>\d+))?").Groups;
            var registry = new Registry(this, queries["id"].Value);
            registry.URL = $"https://archives06.fr/ark:/{queries["something"].Value}/{registry.Id}";

            var client = new HttpClient();
            var manifest = new LigeoManifest(await client.GetStringAsync($"{registry.URL}/manifest"));
            if (!int.TryParse(queries["seq"].Value, out var seq)) Log.Warning("Couldn't parse sequence ({SequenceValue}), using default one", queries["seq"].Value);
            var sequence = manifest.Sequences.Length > seq ? manifest.Sequences[seq] : manifest.Sequences[0];

            registry.Frames = sequence.Canvases.Select((p, i) =>
            {
                var img = p.Images.FirstOrDefault();
                return new Frame
                {
                    FrameNumber = int.TryParse(p.Label, out var number) ? number : i + 1,
                    DownloadUrl = img?.ServiceId,
                    Width = img?.Width,
                    Height = img?.Height,
                    Extra = p.Classeur
                };
            }).ToArray();

            var classeur = sequence.Canvases[0].Classeur;
            registry.CallNumber = classeur == null || string.IsNullOrWhiteSpace(classeur.UnitId) ? null : classeur.UnitId;
            registry.ArkURL = sequence.Id;

            var notes = new List<string>();
            var locationDetails = new List<string>();
            string location = null;
            string district = null;
            // ReSharper disable StringLiteralTypo
            foreach (var metadata in manifest.MetaData)
            {
                var value = Regex.Replace(metadata.Value, "<[^>]*>", ""); // Remove HTML tags
                switch (metadata.Key)
                {
                    case "Commune":
                    case "Commune d’exercice du notaire":
                    case "Lieu":
                    case "Lieu d'édition":
                        location = ToTitleCase(value.ToLower());
                        break;
                    case "Paroisse":
                    case "Complément de lieu":
                        district = ToTitleCase(value.ToLower());
                        break;
                    case "Date":
                    case "Date de l'acte":
                    case "Année (s)":
                    {
                        var dates = value.Split('-');
                        registry.From = dates.FirstOrDefault()?.Trim();
                        registry.To = dates.LastOrDefault()?.Trim();
                        break;
                    }
                    case "Typologie":
                    case "Type de document":
                    case "Type d'acte":
                        registry.Types = registry.Types.Union(GetTypes(value)).ToArray();
                        break;
                    case "Analyse":
                        registry.Title = value;
                        break;
                    case "Folio":
                    case "Volume":
                        registry.Subtitle = value;
                        break;
                    case "Auteur":
                    case "Photographe":
                    case "Sigillant":
                    case "Bureau":
                    case "Présentation du producteur":
                        registry.Author = value;
                        break;
                    default:
                        notes.Add($"{metadata.Key}: {value}");
                        break;
                }
            }

            var (labelRegexExp, type) = classeur?.EncodedArchivalDescriptionId.ToUpperInvariant() switch
            {
                "FRAD006_ETAT_CIVIL" => (@"(?<callnum>.+) +- +(?<type>.*?) *?- *?\((?<from>.+?)( à (?<to>.+))?\)", null),
                "FRAD006_CADASTRE_PLAN" => ("(?<callnum>.+) +- +(?<district>.*?) +- +(?<subtitle>.*?) +- +(?<from>.+?)", new[] { RegistryType.CadastralMap }),
                "FRAD006_CADASTRE_MATRICE" => ("(?<callnum>.+?) +- +(?<title>.*?) *?-", new[] { RegistryType.CadastralMatrix }),
                "FRAD006_CADASTRE_ETAT_SECTION" => ("(?<callnum>.+) +- +(?<title>.*?) *?-", new[] { RegistryType.CadastralSectionStates }),
                "FRAD006_RECENSEMENT_POPULATION" => ("(?<city>.+) +- +(?<from>.+)(, (?<district>.*))", new[] { RegistryType.Census }),
                "FRAD006_HYPOTHEQUES" => ("(?<callnum>.+) +- +(?<title>.+) +-", new[] { RegistryType.Catalogue }),
                "FRAD006_HYPOTHEQUES_ACTES_TRANSLATIFS" => (@"(?<callnum>.+?) ?- +(?<author>.+?) ?\.?- +(?<title>.+?) ?- +(?<from>.+?)(-(?<to>.+))?$", new[] { RegistryType.Engrossments }),
                "FRAD006_REPERTOIRE_NOTAIRES" => ("(?<callnum>.+) +- +(?<title>.+)", new[] { RegistryType.Notarial }),
                "FRAD006_3E" => (@"(?<callnum>3 E [\d ]+?) *- *(?<title>.*)\. *- *(?<from>.*?) *- *(?<to>.*?) *$", new[] { RegistryType.Notarial }), // Notaire
                "FRAD006_C" => (@"(?<callnum>C [\d ]+?) *- *(?<title>.*)\. *- *(?<from>.*?) *- *(?<to>.*?) *$", new[] { RegistryType.Other }), // Archives anciennes
                "FRAD006_ARMOIRIES" => ("(?<callnum>.+) +- +(?<title>.+)", new[] { RegistryType.Other }),
                "FRAD006_OUVRAGES" => ("(?<callnum>.+) +- +(?<title>.+)", new[] { RegistryType.Book }),
                "FRAD006_BN_SOURCES_IMPRIMES" => ("(?<title>.+)", new[] { RegistryType.Book }),
                "FRAD006_ANNUAIRES" => ("(?<title>.+)", new[] { RegistryType.Other }),
                "FRAD006_PRESSE" => (@"(?<title>.+) \(\d*-\d*\), .*? +- +(?<from>(\d|\/)+)(-(?<to>(\d|\/)+))?", new[] { RegistryType.Newspaper }),
                "FRAD006_DELIBERATIONS_CONSEIL_GENERAL" => ("(?<callnum>.+) +- +(?<title>.+) +- +(?<from>.+?)(-(?<to>.+))?$", new[] { RegistryType.Book }),
                "FRAD006_11AV" => ("(?<callnum>.+) +- +(?<title>.+) +- +(?<from>.+?)(-(?<to>.+))?$", new[] { RegistryType.Other }), // Audiovisuel
                "FRAD006_10FI" => (@"(?<callnum>.+) +- +(?<title>.+) +- +\((?<from>.+?)-(?<to>.+)\)", new[] { RegistryType.Other }), // Iconographie
                _ => (null, null)
            };
            // ReSharper restore StringLiteralTypo

            if (labelRegexExp != null)
            {
                var data = Regex.Match(sequence.Label, labelRegexExp).Groups;
                registry.CallNumber ??= data.TryGetValue("callnum");
                location ??= ToTitleCase(data.TryGetValue("city"));
                district ??= ToTitleCase(data.TryGetValue("district"));
                registry.From ??= data.TryGetValue("from");
                registry.To ??= data.TryGetValue(data["to"].Success ? "to" : "from");
                registry.Title ??= data.TryGetValue("title");
                registry.Subtitle ??= data.TryGetValue("subtitle");
                registry.Author ??= data.TryGetValue("author");
                if (data["type"].Success) registry.Types = registry.Types.Union(GetTypes(data.TryGetValue("type"))).ToArray();
                if (type?.Length > 0) registry.Types = registry.Types.Union(type).ToArray();


                var analyse = await client.GetStringAsync(registry.URL);
                locationDetails.AddRange(Regex
                    .Matches(analyse, @"<ul><li><a href=[^>]+?><span>(?<archivePath>[^>]+?)\.?</span></a>")
                    .Select(m => m.Groups.TryGetValue("archivePath")));

                switch (classeur?.EncodedArchivalDescriptionId.ToUpperInvariant())
                {
                    // The civil registry collection only provides the city through the analysis page
                    case "FRAD006_ETAT_CIVIL":
                        location = ToTitleCase(locationDetails[^1]);
                        break;
                    case "FRAD006_3E" when data.TryGetValue("title") == $"{data.TryGetValue("from")}-{data.TryGetValue("to")}":
                        registry.Title = locationDetails.LastOrDefault();
                        break;
                }
            }
            if (location != null)
            {
                var locationInDetails = locationDetails.IndexOf(locationDetails.Find(l => string.Equals(l, location, StringComparison.CurrentCultureIgnoreCase)));
                if (locationInDetails >= 0) locationDetails[locationInDetails] = location;
                else locationDetails.Add(location);
            }
            if (district != null) locationDetails.Add(district);
            registry.Location = locationDetails.ToArray();
            registry.Notes = string.Join("\n", notes);


            return (registry, int.TryParse(queries["page"].Value, out var page) ? page : 1);
        }

        private static string ToTitleCase(string text) => text is null ? null : Regex.Replace(text, @"\p{L}+", match => match.Value[..1].ToUpper() + match.Value[1..].ToLower());

        private static IEnumerable<RegistryType> GetTypes(string typeActe) => Regex.Split(typeActe, "(?=[A-Z])").Select(t => t.Trim(' ') switch
        {
            "Naissances" => RegistryType.Birth,
            "Tables décennales des naissances" or "Tables alphabétiques des naissances" => RegistryType.BirthTable,
            "Baptêmes" => RegistryType.Baptism,
            "Tables des baptêmes" => RegistryType.BaptismTable,

            "Confirmations" => RegistryType.Confirmation,
            "Tables des communions" => RegistryType.Communion,

            "Publications" or "Publications de mariages" => RegistryType.Banns,
            "Mariages" => RegistryType.Marriage,
            "Tables des mariages" or "Tables décennales des mariages" or "Tables alphabétiques des mariages" => RegistryType.MarriageTable,
            "Divorces" => RegistryType.Divorce,

            "Décès" => RegistryType.Death,
            "Tables décennales des décès" or "Tables alphabétiques des décès" => RegistryType.DeathTable,
            "Sépultures" or "Sépultures des enfants décédés sans baptêmes" => RegistryType.Burial,
            "Tables des sépultures" => RegistryType.BurialTable,

            "Répertoire" => RegistryType.Catalogue,
            "Inventaire" => RegistryType.Other,

            "matrice cadastrale" => RegistryType.CadastralMatrix,
            "état de section" => RegistryType.CadastralSectionStates,

            _ => RegistryType.Unknown
        }).Where(result => result != RegistryType.Unknown);


        public override Task<string> Ark(Frame page) => Task.FromResult(page.Registry == null ? null : $"{page.Registry.ArkURL}/{page.FrameNumber}");

        public override async Task<Stream> GetFrame(Frame page, Scale scale, Action<Progress> progress)
        {
            var stream = await Data.TryGetImageFromDrive(page, scale);
            if (stream != null) return stream;

            progress?.Invoke(Progress.Unknown);
            var client = new HttpClient();
            var wantedSize = scale switch
            {
                Scale.Thumbnail => 512,
                Scale.Navigation => 2048,
                _ => -1
            };
            var size = "max";
            if (wantedSize < 0 || page.Width == null || page.Width <= wantedSize) scale = Scale.Full;
            else size = $"{wantedSize},"; // AD06 neither supports ^ and ! modifiers nor percentages
            var image = await Image
                .LoadAsync(await client.GetStreamAsync(Iiif.GenerateImageRequestUri(page.DownloadUrl, size: size)).ConfigureAwait(false))
                .ConfigureAwait(false);
            page.ImageSize = scale;
            progress?.Invoke(Progress.Finished);

            await Data.SaveImage(page, image, false);
            return image.ToStream();
        }
    }
}
