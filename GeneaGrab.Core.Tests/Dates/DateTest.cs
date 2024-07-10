using GeneaGrab.Core.Models.Dates;
using Xunit.Abstractions;

namespace GeneaGrab.Core.Tests.Dates;

public class DateTest(ITestOutputHelper output)
{
    public static IEnumerable<object[]> ParserData()
    {
        yield return ["17/01/1420", new JulianDate(1420, 1, 17, precision: Precision.Days)];

        yield return ["2023-07-30 16:00:02", new GregorianDate(2023, 7, 30, 16, 0, 2, precision: Precision.Seconds)];
        yield return ["2023-07", new GregorianDate(2023, 7, precision: Precision.Months)];

        yield return ["An XII", new FrenchRepublicanDate(12, precision: Precision.Years)];
        yield return ["An XIII de la République Française", new FrenchRepublicanDate(13, precision: Precision.Years)];
        yield return ["23 Ventôse An 4", new FrenchRepublicanDate(4, 6, 23, precision: Precision.Days)];
        yield return ["23 Ventôse An IV", new FrenchRepublicanDate(4, 6, 23, precision: Precision.Days)];
        yield return ["23 Ventose An IV", new FrenchRepublicanDate(4, 6, 23, precision: Precision.Days)];
        yield return ["10 Vendémiaire An X de la République Française", new FrenchRepublicanDate(10, 1, 10, precision: Precision.Days)];
    }

    [Theory(DisplayName = "Check string parser")]
    [MemberData(nameof(ParserData))]
    public void CheckParser(string text, Date expected)
    {
        var parsed = Date.ParseDate(text);
        Assert.NotNull(parsed);
        output.WriteLine(parsed.ToString());
        Assert.Equal(expected.Precision, parsed.Precision);
        Assert.Equal(0, expected.CompareTo(parsed));
        Assert.True(expected == parsed);
    }

    public static IEnumerable<object[]> StringifyData()
    {
        yield return [new JulianDate(1420, 1, 17), "1420-01-17"];

        yield return [new GregorianDate(2023, 7, 30, 16, 0, 2, precision: Precision.Seconds), "2023-07-30 16:00:02"];
        yield return [new GregorianDate(2023, 7, precision: Precision.Months), "2023-07"];

        yield return [new FrenchRepublicanDate(12, precision: Precision.Years), "An XII"];
        yield return [new FrenchRepublicanDate(13, precision: Precision.Years), "An XIII"];
        yield return [new FrenchRepublicanDate(3, 5, precision: Precision.Months), "Pluviôse An III"];
        yield return [new FrenchRepublicanDate(4, 6, 23, precision: Precision.Days), "23 Ventôse An IV"];
        yield return [new FrenchRepublicanDate(10, 1, 10, precision: Precision.Days), "10 Vendémiaire An X"];
    }

    [Theory(DisplayName = "Check date to string conversion")]
    [MemberData(nameof(StringifyData))]
    public void CheckStringify(Date date, string expected) => Assert.Equal(expected, date.ToString());
}
