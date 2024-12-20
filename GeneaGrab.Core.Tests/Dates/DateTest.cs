using GeneaGrab.Core.Models.Dates;
using Xunit.Abstractions;

namespace GeneaGrab.Core.Tests.Dates;

public class DateTest(ITestOutputHelper output)
{
    public static TheoryData<string, Date> ParserData() => new()
    {
        { "17/01/1420", new JulianDate(1420, 1, 17, precision: Precision.Days) },
        { "1420", new JulianDate(1420, precision: Precision.Years) },

        { "2023-07-30 16:00:02", new GregorianDate(2023, 7, 30, 16, 0, 2, precision: Precision.Seconds) },
        { "2023-07", new GregorianDate(2023, 7, precision: Precision.Months) },
        { "2023", new GregorianDate(2023, precision: Precision.Years) },

        { "An XII", new FrenchRepublicanDate(12, precision: Precision.Years) },
        { "An XIII de la République Française", new FrenchRepublicanDate(13, precision: Precision.Years) },
        { "23 Ventôse An 4", new FrenchRepublicanDate(4, 6, 23, precision: Precision.Days) },
        { "23 Ventôse An IV", new FrenchRepublicanDate(4, 6, 23, precision: Precision.Days) },
        { "23 Ventose An IV", new FrenchRepublicanDate(4, 6, 23, precision: Precision.Days) },
        { "10 Vendémiaire An X de la République Française", new FrenchRepublicanDate(10, 1, 10, precision: Precision.Days) },
    };

    [Theory(DisplayName = "Check string parser")]
    [MemberData(nameof(ParserData))]
    public void CheckParser(string text, Date expected)
    {
        var parsed = Date.ParseDate(text);
        Assert.NotNull(parsed);
        output.WriteLine(parsed.ToString());
        Assert.Equal(expected.Precision, parsed.Precision);
        Assert.Equivalent(expected, parsed);
        Assert.True(expected == parsed);
    }

    public static TheoryData<Date, string> StringifyData() => new()
    {
        { new JulianDate(1420, 1, 17), "1420-01-17" },
        { new JulianDate(1420, precision: Precision.Years), "1420" },

        { new GregorianDate(2023, 7, 30, 16, 0, 2, precision: Precision.Seconds), "2023-07-30 16:00:02" },
        { new GregorianDate(2023, 7, precision: Precision.Months), "2023-07" },
        { new GregorianDate(2023, precision: Precision.Years), "2023" },

        { new FrenchRepublicanDate(12, precision: Precision.Years), "An XII" },
        { new FrenchRepublicanDate(13, precision: Precision.Years), "An XIII" },
        { new FrenchRepublicanDate(3, 5, precision: Precision.Months), "Pluviôse An III" },
        { new FrenchRepublicanDate(4, 6, 23, precision: Precision.Days), "23 Ventôse An IV" },
        { new FrenchRepublicanDate(10, 1, 10, precision: Precision.Days), "10 Vendémiaire An X" },
    };

    [Theory(DisplayName = "Check date to string conversion")]
    [MemberData(nameof(StringifyData))]
    public void CheckStringify(Date date, string expected) => Assert.Equal(expected, date.ToString());
}
