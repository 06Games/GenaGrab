using System;
using System.Globalization;
using System.Text.RegularExpressions;
using FrenchRepublicanCalendar;
using RomanNumerals.Numerals;

namespace GeneaGrab.Core.Models.Dates
{
    public class FrenchRepublicanDate : Date
    {
        public FrenchRepublicanDate(int year, int? month = null, int? day = null, int? hour = null, int? minute = null, int? second = null, Precision precision = Precision.Days)
            : this(new DateTime(year, month ?? 1, day ?? 1, hour ?? 0, minute ?? 0, second ?? 0, 0, DateTimeKind.Utc), precision) { }
        public FrenchRepublicanDate(DateTime dt, Precision precision = Precision.Days)
            : base(new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, dt.Millisecond, new FrenchRepublicanCalendar.FrenchRepublicanCalendar(), dt.Kind), precision) { }

        public static bool TryParse(string dateString, out FrenchRepublicanDate date)
        {
            date = null;
            if (string.IsNullOrWhiteSpace(dateString)) return false;
            var regex = Regex.Match(dateString, @"((?<day>\d+) )?((?<month>\p{L}+) )?an (?<year>[IVX\d]+)", RegexOptions.IgnoreCase);
            if (!regex.Success) return false;

            var precision = Precision.Unknown;
            if (TryGet("year", out var year)) precision = Precision.Years;
            else year = 1;

            if (TryGet("month", out var month)) precision = Precision.Months;
            else month = 1;

            if (TryGet("day", out var day)) precision = Precision.Days;
            else day = 1;

            date = new FrenchRepublicanDate(year, month, day, precision: precision);
            return true;

            bool TryGet(string key, out int value)
            {
                value = -1;
                if (!regex.Groups[key].Success) return false;
                if (int.TryParse(regex.Groups[key].Value, out var intVal)) value = intVal;
                else if (NumeralParser.Default.TryParse(regex.Groups[key].Value, out var uintVal)) value = (int)uintVal;
                else
                {
                    var months = Enum.GetNames(typeof(FrenchRepublicanMonth));
                    var monthName = Array.Find(months,
                        m => string.Compare(m, regex.Groups[key].Value, CultureInfo.InvariantCulture, CompareOptions.IgnoreNonSpace | CompareOptions.IgnoreCase) == 0);
                    if (monthName != null) value = Array.IndexOf(months, monthName) + 1;
                }
                return value != -1;
            }
        }

        protected override string ToString(Precision precision)
        {
            var format = (Precision)Math.Min((int)precision, (int)Precision) switch
            {
                Precision.Years => "An Y",
                Precision.Months => "MMMM An Y",
                Precision.Days => "dd MMMM An Y",
                Precision.Hours => "dd MMMM An Y HH",
                Precision.Minutes => "dd MMMM An Y HH:mm",
                Precision.Seconds => "dd MMMM An Y HH:mm:ss",
                _ => "F"
            };
            return ToString(format);
        }
        protected override string ToString(string format) => new FrenchRepublicanDateTime(GregorianDateTime).ToString(format);
    }
}
