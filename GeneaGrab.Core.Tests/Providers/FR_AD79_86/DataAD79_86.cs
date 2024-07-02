using GeneaGrab.Core.Models;
using GeneaGrab.Core.Models.Dates;

namespace GeneaGrab.Core.Tests.Providers.FR_AD79_86;

[SuppressMessage("ReSharper", "StringLiteralTypo")]
public class DataAD79_86 : IEnumerable<object[]>
{
    private readonly List<object[]> _data =
    [
        new Data
        {
            URL = "https://archives-deux-sevres-vienne.fr/ark:/28387/vtae7b67d081526ee00/daogrp/0/layout:table/idsearch:RECH_67ed934afb0e90890dc0c3d2de28aa19",
            Id = "vtae7b67d081526ee00",
            Page = 1,
            Cote = "collection communale 3171",
            Ville = "Poitiers (Vienne, France)",
            Paroisse = "Sainte-Opportune",
            Types = [RegistryType.Death], // Wrongly typed on the website
            From = new JulianDate(1366, precision: Precision.Years),
            To = new GregorianDate(1667, precision: Precision.Years)
        },

        new Data
        {
            URL = "https://archives-deux-sevres-vienne.fr/ark:/28387/vta653909c68277e7e1/daogrp/0/layout:table/idsearch:RECH_a840bb28ce19d01007ed57125416fdd5",
            Id = "vta653909c68277e7e1",
            Page = 1,
            Cote = "collection communale 1994",
            Ville = "Loudun (Vienne, France)",
            Paroisse = "Saint-Pierre-du-Marché",
            Types = [RegistryType.BirthTable, RegistryType.MarriageTable, RegistryType.DeathTable], // Wrongly typed on the website
            From = new GregorianDate(1593, precision: Precision.Years),
            To = new GregorianDate(1678, precision: Precision.Years)
        },

        new Data
        {
            URL = "https://archives-deux-sevres-vienne.fr/ark:/28387/vtac7d3291f3cb41458/daogrp/0/layout:table/idsearch:RECH_a840bb28ce19d01007ed57125416fdd5",
            Id = "vtac7d3291f3cb41458",
            Page = 1,
            Cote = "collection communale 3116",
            Ville = "Poitiers (Vienne, France)",
            Paroisse = "Saint-Didier",
            Types = [RegistryType.BirthTable, RegistryType.MarriageTable, RegistryType.DeathTable], // Wrongly typed on the website
            From = new JulianDate(1564, precision: Precision.Years),
            To = new GregorianDate(1791, precision: Precision.Years)
        },

        new Data
        {
            URL = "https://archives-deux-sevres-vienne.fr/ark:/28387/vtadc65cbfff41a4055/daogrp/0/layout:table/idsearch:RECH_8dc6362082a86dbcc8a7c360dfc61a3e",
            Id = "vtadc65cbfff41a4055",
            Page = 1,
            Cote = "collection communale 906",
            Ville = "Châtellerault (Vienne, France)",
            Paroisse = "Saint-Jean-Baptiste",
            Types = [RegistryType.Birth, RegistryType.Marriage, RegistryType.Death], // Wrongly typed on the website
            From = new JulianDate(1540, precision: Precision.Years),
            To = new JulianDate(1553, precision: Precision.Years)
        },

        new Data
        {
            URL = "https://archives-deux-sevres-vienne.fr/ark:/58825/vtaf13d60438573a517/daogrp/0/layout:table/idsearch:RECH_8dc6362082a86dbcc8a7c360dfc61a3e",
            Id = "vtaf13d60438573a517",
            Page = 1,
            Cote = "E DEPOT 112 / 2 E 8-1",
            Ville = "Amailloux (Deux-Sèvres, France)",
            Types = [RegistryType.Birth], // Wrongly typed on the website
            From = new GregorianDate(1589, precision: Precision.Years),
            To = new GregorianDate(1612, precision: Precision.Years)
        },

        new Data
        {
            URL = "https://archives-deux-sevres-vienne.fr/ark:/28387/vtaf2121e46840a5491/daogrp/0/layout:table/idsearch:RECH_b9fa44b07e9ab743088183667855111a",
            Id = "vtaf2121e46840a5491",
            Page = 1,
            Cote = "11 E 246/1-1",
            Ville = "Saint-Aubin (Vienne, France)",
            Types = [RegistryType.Birth, RegistryType.Marriage, RegistryType.Death], // Wrongly typed on the website
            From = new GregorianDate(1783, precision: Precision.Years),
            To = new GregorianDate(1846, precision: Precision.Years)
        },

        new Data
        {
            URL = "https://archives-deux-sevres-vienne.fr/ark:/58825/vta2639cbd364735e90/daogrp/0/layout:table/idsearch:RECH_dc2e7b893b169aa88fe2df5be9a123d1",
            Id = "vta2639cbd364735e90",
            Page = 1,
            Cote = "E DEPOT 154 / 2 E 317-11",
            Ville = "Tillou (Deux-Sèvres, France)",
            Types = [RegistryType.Birth, RegistryType.Marriage, RegistryType.Death, RegistryType.BirthTable, RegistryType.MarriageTable, RegistryType.DeathTable], // Wrongly typed on the website
            From = new GregorianDate(1640, precision: Precision.Years),
            To = new FrenchRepublicanDate(10, precision: Precision.Years)
        },

        new Data
        {
            URL = "https://archives-deux-sevres-vienne.fr/ark:/58825/vta512b489224012371/daogrp/0/layout:table/idsearch:RECH_a50f70e5d23e0a91af432862af92747d",
            Id = "vta512b489224012371",
            Page = 1,
            Cote = "12 NUM 41/2",
            Ville = "Sainte-Gemme (Deux-Sèvres, France)",
            Types = [RegistryType.Birth, RegistryType.Marriage, RegistryType.Death],
            From = new GregorianDate(1792, precision: Precision.Years),
            To = new FrenchRepublicanDate(10, precision: Precision.Years)
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
    public string? Paroisse;
    public RegistryType[] Types = null!;
    public Date From = null!;
    public Date To = null!;

    public static implicit operator object[](Data data) => [data];
}
