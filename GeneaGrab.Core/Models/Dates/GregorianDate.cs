using System;
using System.Globalization;

namespace GeneaGrab.Core.Models.Dates;

public class GregorianDate : Date
{
    public GregorianDate(int year, int? month = null, int? day = null, int? hour = null, int? minute = null, int? second = null, Precision precision = Precision.Days)
        : base(year, month, day, hour, minute, second, precision) { }
    public GregorianDate(DateTime dt, Precision precision = Precision.Days) : base(dt, precision) { }

    public static bool TryParse(string dateString, out GregorianDate date)
    {
        date = null;

        var culture = new CultureInfo("fr-FR");
        const DateTimeStyles style = DateTimeStyles.AssumeLocal;

        if (string.IsNullOrWhiteSpace(dateString)) return false;

        Precision precision;
        if (DateTime.TryParse(dateString, culture, style, out var d))
        {
            if (d.Second != 0) precision = Precision.Seconds;
            else if (d.Minute != 0) precision = Precision.Minutes;
            else if (d.Hour != 0) precision = Precision.Hours;
            else if (d.Day != 1) precision = Precision.Days;
            else precision = Precision.Months;
        }
        else if (DateTime.TryParseExact(dateString, "yyyy", culture, style, out d)) precision = Precision.Years;
        else return false;

        if (d.Year < 1582) return false;
        date = new GregorianDate(d, precision);
        return true;
    }
}
