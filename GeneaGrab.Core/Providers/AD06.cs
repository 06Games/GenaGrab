using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GeneaGrab.Core.Helpers;
using GeneaGrab.Core.Models;

namespace GeneaGrab.Core.Providers;

public class AD06 : Ligeo
{
    public override string Id => "AD06";
    protected override string Host => "archives06.fr";

    public AD06(HttpClient client = null) : base(client) { }


    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    protected override void ParseMetaData(ref Registry registry, string key, string value)
    {
        switch (key)
        {
            case "Commune":
            case "Commune d’exercice du notaire":
            case "Lieu":
            case "Lieu d'édition":
                registry.Location[0] = ToTitleCase(value.ToLower());
                break;
            case "Paroisse":
            case "Complément de lieu":
                registry.Location[1] = ToTitleCase(value.ToLower());
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
                base.ParseMetaData(ref registry, key, value);
                break;
        }
    }

    protected override void ReadAllMetadata(ref Registry registry, ReadOnlyDictionary<string, string> manifestMetadata)
    {
        registry.Location = new string[2];
        base.ReadAllMetadata(ref registry, manifestMetadata);
    }

    protected override async Task ParseClasseur(Registry registry, IIiifSequence sequence, LigeoClasseur classeur)
    {
        await base.ParseClasseur(registry, sequence, classeur);

        // ReSharper disable StringLiteralTypo
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

        var locationDetails = new List<string>();
        var location = string.IsNullOrWhiteSpace(registry.Location[0]) ? null : registry.Location[0];
        var district = string.IsNullOrWhiteSpace(registry.Location[1]) ? null : registry.Location[1];
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


            var analyse = await HttpClient.GetStringAsync(registry.URL);
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

    protected override string GetRequestImageSize(ref Scale scale, Frame page)
    {
        var wantedSize = scale switch
        {
            Scale.Thumbnail => 512,
            Scale.Navigation => 2048,
            _ => -1
        };
        var size = "max";
        if (wantedSize < 0 || page.Width == null || page.Width <= wantedSize) scale = Scale.Full;
        else size = $"{wantedSize},"; // AD06 neither supports ^ and ! modifiers nor percentages
        return size;
    }
}
