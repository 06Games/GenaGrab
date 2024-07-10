using System;
using Newtonsoft.Json;

namespace GeneaGrab.Core.Models.Dates;

public enum Precision { Unknown = -1, Years, Months, Days, Hours, Minutes, Seconds }
[JsonConverter(typeof(DateConverter))]
public abstract class Date : IComparable<Date>
{
    public DateTime GregorianDateTime { get; }
    public Precision Precision { get; }

    protected Date(int year, int? month = null, int? day = null, int? hour = null, int? minute = null, int? second = null, Precision precision = Precision.Days)
        : this(new DateTime(year, month ?? 1, day ?? 1, hour ?? 0, minute ?? 0, second ?? 0, 0, DateTimeKind.Utc), precision) { }
    protected Date(DateTime dt, Precision precision = Precision.Days)
    {
        GregorianDateTime = dt;
        Precision = precision;
    }

    public static implicit operator Date(string date) => ParseDate(date);
    public static Date ParseDate(string date)
    {
        if (GregorianDate.TryParse(date, out var gregorianDate)) return gregorianDate;
        if (JulianDate.TryParse(date, out var julianDate)) return julianDate;
        return FrenchRepublicanDate.TryParse(date, out var frenchRepublicanDate) ? frenchRepublicanDate : null;
    }

    public override string ToString() => ToString(Precision);
    protected virtual string ToString(Precision precision)
    {
        var format = (Precision)Math.Min((int)precision, (int)Precision) switch
        {
            Precision.Years => "yyyy",
            Precision.Months => "yyyy-MM",
            Precision.Days => "yyyy-MM-dd",
            Precision.Hours => "yyyy-MM-dd HH",
            Precision.Minutes => "yyyy-MM-dd HH:mm",
            Precision.Seconds => "yyyy-MM-dd HH:mm:ss",
            _ => "f"
        };
        return ToString(format);
    }
    protected virtual string ToString(string format) => GregorianDateTime.ToString(format);

    public int CompareTo(Date other) => GregorianDateTime.CompareTo(other.GregorianDateTime);

    public static bool operator ==(Date date1, Date date2)
    {
        if (ReferenceEquals(date1, date2)) return true;
        if (date1 is null || date2 is null) return false;
        if (date1.Precision != date2.Precision) return false;
        return date1.GregorianDateTime == date2.GregorianDateTime;
    }
    public static bool operator !=(Date date1, Date date2) => !(date1 == date2);

    private bool Equals(Date other) => this == other;
    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        return obj.GetType() == GetType() && Equals((Date)obj);
    }
    public static bool operator <(Date date1, Date date2) => date1?.CompareTo(date2) < 0;
    public static bool operator <=(Date date1, Date date2) => date1?.CompareTo(date2) <= 0;
    public static bool operator >(Date date1, Date date2) => date1?.CompareTo(date2) > 0;
    public static bool operator >=(Date date1, Date date2) => date1?.CompareTo(date2) >= 0;
    public override int GetHashCode() => HashCode.Combine(GregorianDateTime, (int)Precision);
}
