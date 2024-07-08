using System;
using Avalonia;

namespace GeneaGrab.Helpers;

public static class RectExtensions
{
    public static Rect Translate(this Rect rect, Size size) => rect.Translate(new Vector(size.Width, size.Height));

    public static Rect Round(this Rect rect, MidpointRounding method = MidpointRounding.AwayFromZero)
        => new(Math.Round(rect.X, method), Math.Round(rect.Y, method), Math.Round(rect.Width, method), Math.Round(rect.Height, method));

    public static Rect Divide(this Rect rect, double divider) => new(rect.X / divider, rect.Y / divider, rect.Width / divider, rect.Height / divider);
}
