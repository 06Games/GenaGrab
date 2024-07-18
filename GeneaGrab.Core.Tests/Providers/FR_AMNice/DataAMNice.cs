using GeneaGrab.Core.Models;
using GeneaGrab.Core.Models.Dates;

namespace GeneaGrab.Core.Tests.Providers.FR_AMNice;

[SuppressMessage("ReSharper", "StringLiteralTypo")]
public class DataAMNice : IEnumerable<object[]>
{
    private readonly List<object[]> _data =
    [
        new Data
        {
            URL = "https://recherche.archives.nicecotedazur.org/viewer/series/VDN_0029/FRAC006088_002E035_2?img=FRAC006088_002E035_2_030.jpg",
            Id = "FRAC006088_000000029_de-194",
            Page = 30,
            Cote = "2 E 35-2",
            Ville = "Nice",
            DetailPosition = ["France", "Alpes-Maritimes"],
            Types = [RegistryType.Birth],
            From = new GregorianDate(1895, 5, 3),
            To = new GregorianDate(1895, 9, 17)
        },

        new Data
        {
            URL = "https://recherche.archives.nicecotedazur.org/viewer/viewer/VDN_0045/FRAC006088_010Fi2967_R.jpg",
            Id = "FRAC006088_000000070_de-4499",
            Page = 1,
            Cote = "10 Fi 2967",
            Ville = "Bourg-en-Bresse",
            DetailPosition = ["France", "Ain"],
            Types = [RegistryType.Other],
            From = new GregorianDate(1890, precision: Precision.Years),
            To = new GregorianDate(1966, precision: Precision.Years)
        },

        new Data
        {
            URL = "https://recherche.archives.nicecotedazur.org/viewer/viewer/VDN_0036/FRAC006088_013Fi0185.jpg",
            Id = "FRAC006088_000000036_de-917",
            Page = 1,
            Cote = "13 Fi 185",
            Rue = "Peïra-Cava (station)",
            Ville = "Lucéram",
            DetailPosition = ["France", "Alpes-Maritimes"],
            Auteur = "Baudoin, Maurice Joseph (1910-1988), Baudoin, Édouard Joseph César (1870-1947)",
            Types = [RegistryType.Other],
            From = new GregorianDate(1910, precision: Precision.Years),
            To = new GregorianDate(1938, precision: Precision.Years)
        },

        new Data
        {
            URL = "https://recherche.archives.nicecotedazur.org/viewer/series/VDN_0116/FRAC006088_033PER/FRAC006088_033PER_1872_1?img=FRAC006088_033PER_1872_1_0009.jpg",
            Id = "FRAC006088_000000116_de-26",
            Page = 5,
            Cote = "33 PER 4",
            Ville = "Nice",
            DetailPosition = ["France", "Alpes-Maritimes"],
            Types = [RegistryType.Newspaper],
            From = new GregorianDate(1872, precision: Precision.Years),
            To = new GregorianDate(1872, precision: Precision.Years)
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
    public string? Rue;
    public string Ville = null!;
    public string[] DetailPosition = [];
    public string? Auteur;
    public RegistryType[] Types = null!;
    public Date From = null!;
    public Date To = null!;

    public static implicit operator object[](Data data) => [data];
}
