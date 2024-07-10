using System;
using System.Globalization;
using System.Text;

namespace GeneaGrab.Core.Models.Dates;

public class JulianDate : Date
{
    private static readonly JulianCalendar JulianCalendar = new();

    public JulianDate(int year, int? month = null, int? day = null, int? hour = null, int? minute = null, int? second = null, Precision precision = Precision.Days)
        : this(new DateTime(year, month ?? 1, day ?? 1, hour ?? 0, minute ?? 0, second ?? 0, 0, DateTimeKind.Utc), precision) { }
    public JulianDate(DateTime dt, Precision precision = Precision.Days)
        : base(new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, dt.Millisecond, JulianCalendar, dt.Kind), precision) { }

    public static bool TryParse(string dateString, out JulianDate date)
    {
        date = null;

        var culture = new CultureInfo("fr-FR");
        const DateTimeStyles style = DateTimeStyles.AssumeLocal;

        if (string.IsNullOrWhiteSpace(dateString)) return false;
        if (DateTime.TryParse(dateString, culture, style, out var d)) date = new JulianDate(d);
        else if (DateTime.TryParseExact(dateString, "yyyy", culture, style, out d)) date = new JulianDate(d, Precision.Years);
        else return false;
        return true;
    }

    protected override string ToString(Precision precision)
    {
        var format = new StringBuilder();
        if (Precision < precision) precision = Precision;

        if (precision >= Precision.Years) format.Append(JulianCalendar.GetYear(GregorianDateTime).ToString("d4"));
        if (precision >= Precision.Months) format.Append($"-{JulianCalendar.GetMonth(GregorianDateTime):D2}");
        if (precision >= Precision.Days) format.Append($"-{JulianCalendar.GetDayOfMonth(GregorianDateTime):D2}");
        if (precision >= Precision.Hours) format.Append($" {JulianCalendar.GetHour(GregorianDateTime):D2}");
        if (precision >= Precision.Minutes) format.Append($":{JulianCalendar.GetMinute(GregorianDateTime):D2}");
        if (precision >= Precision.Seconds) format.Append($":{JulianCalendar.GetSecond(GregorianDateTime):D2}");

        return ToString(format.ToString());
    }
}
