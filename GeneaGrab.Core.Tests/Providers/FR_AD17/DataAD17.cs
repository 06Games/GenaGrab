using GeneaGrab.Core.Models;
using GeneaGrab.Core.Models.Dates;

namespace GeneaGrab.Core.Tests.Providers.FR_AD17;

[SuppressMessage("ReSharper", "StringLiteralTypo")]
public class DataAD17 : IEnumerable<object[]>
{
    // How to add data:
    // 1. Go to https://www.archinoe.net/v2/ad17/registre.html and open a registry
    // 2. In the console, execute
    // >>     window.location.href + "&infos=" + encodeURIComponent($("option:selected").prop('outerHTML')) + "&page=" + $("#visu_pagination").text().split('/')[0]
    private readonly List<object[]> _data =
    [
        new Data
        {
            URL =
                "https://www.archinoe.net/v2/ad17/visualiseur/registre.html?id=170039611&infos=%3Coption%20value%3D%22170039611%22%20selected%3D%22%22%3E2%20E%2088%2F%204%20-%20Champagnolles%20-%20Collection%20du%20greffe%20-%20Etat%20civil%20-%20Naissances%20Mariages%20D%C3%A9c%C3%A8s%20Publications%20de%20Mariages%20%20-%201808%20-%201812%3C%2Foption%3E&page=5",
            Id = "170039611",
            Page = 5,
            Cote = "2 E 88/ 4",
            Ville = "Champagnolles",
            Types = [RegistryType.Birth, RegistryType.Marriage, RegistryType.Death, RegistryType.Banns],
            From = new GregorianDate(1808, precision: Precision.Years),
            To = new GregorianDate(1812, precision: Precision.Years)
        },

        new Data
        {
            URL =
                "https://www.archinoe.net/v2/ad17/visualiseur/registre.html?id=170039732&infos=%3Coption%20value%3D%22170039732%22%20selected%3D%22%22%3E5%20E%20447*%20-%20Agonnay%20-%20Collection%20du%20greffe%20-%20Etat%20civil%20-%20Tables%20d%C3%A9cennales%20%20-%201903%20-%201912%3C%2Foption%3E&page=1",
            Id = "170039732",
            Page = 1,
            Cote = "5 E 447*",
            Ville = "Agonnay",
            Types = [RegistryType.BirthTable, RegistryType.MarriageTable, RegistryType.DeathTable],
            From = new GregorianDate(1903, precision: Precision.Years),
            To = new GregorianDate(1912, precision: Precision.Years)
        },

        new Data
        {
            URL =
                "https://www.archinoe.net/v2/ad17/visualiseur/registre.html?id=170031763&infos=%3Coption%20value%3D%22170031763%22%20selected%3D%22%22%3ENon%20cot%C3%A9%20-%20Saintes%20-%20Collection%20communale%20-%20Paroissial%20-%20Tables%20d%C3%A9cennales%20%20-%201621%20-%201793%3C%2Foption%3E&page=1",
            Id = "170031763",
            Page = 1,
            Cote = "Non coté",
            Ville = "Saintes",
            Types = [RegistryType.BaptismTable, RegistryType.MarriageTable, RegistryType.BurialTable],
            From = new GregorianDate(1621, precision: Precision.Years),
            To = new GregorianDate(1793, precision: Precision.Years)
        },

        new Data
        {
            URL =
                "https://www.archinoe.net/v2/ad17/visualiseur/registre.html?id=170023768&infos=%3Coption%20value%3D%22170023768%22%20selected%3D%22%22%3E2%20E%20379%2F3*%20-%20Saint-Martin-de-Juillers%20-%20Collection%20du%20greffe%20-%20Etat%20civil%20-%20Naissances%20Mariages%20D%C3%A9c%C3%A8s%20%20-%201793%20-%20an%20IV%3C%2Foption%3E&page=1",
            Id = "170023768",
            Page = 1,
            Cote = "2 E 379/3*",
            Ville = "Saint-Martin-de-Juillers",
            Types = [RegistryType.Birth, RegistryType.Marriage, RegistryType.Death],
            From = new GregorianDate(1793, precision: Precision.Years),
            To = new FrenchRepublicanDate(4, precision: Precision.Years)
        },

        new Data
        {
            URL =
                "https://www.archinoe.net/v2/ad17/visualiseur/registre.html?id=170061725&infos=%3Coption%20value%3D%22170061725%22%20selected%3D%22%22%3EI%20142%20-%20La%20Rochelle%20-%20Collection%20du%20greffe%20-%20Pastoral%20-%20Bapt%C3%AAmes%20Mariages%20%20-%201567%20-%201575%3C%2Foption%3E&page=1",
            Id = "170061725",
            Page = 1,
            Cote = "I 142",
            Ville = "La Rochelle",
            Types = [RegistryType.Baptism, RegistryType.Marriage],
            From = new JulianDate(1567, precision: Precision.Years),
            To = new JulianDate(1575, precision: Precision.Years)
        }
    ];

    public IEnumerator<object[]> GetEnumerator() => _data.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
public class Data
{
    public string URL = null!;
    public string Id = null!;
    public int Page;
    public string Cote = null!;
    public string Ville = null!;
    public RegistryType[] Types = null!;
    public Date From = null!;
    public Date To = null!;

    public static implicit operator object[](Data data) => [data];
}
