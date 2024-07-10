using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
using GeneaGrab.Core.Models;
using SixLabors.ImageSharp;

namespace GeneaGrab.Core.Helpers;

public static class DeepZoom
{
    /// <summary>Returns the maximum zoom level</summary>
    public static int CalculateIndex(Frame page) => CalculateIndex(page.Width!.Value, page.Height!.Value);
    public static int CalculateIndex(int width, int height) => (int)Math.Ceiling(Math.Log(Math.Max(width, height), 2));

    /// <summary>Returns the properties of the image</summary>
    /// <param name="baseURL">Image root url</param>
    /// <param name="client">HTTP client to use</param>
    public static async Task<(int w, int h, int tileSize, string format)> ImageData(string baseURL, HttpClient client)
    {
        var data = await client.GetStringAsync($"{baseURL}/image.xml");
        var dataResp = new XmlDocument();
        if (!string.IsNullOrEmpty(data)) dataResp.LoadXml(data);
        var layer = dataResp.DocumentElement;
        var size = layer?.FirstChild;

        return (
            int.TryParse(size?.Attributes?["Width"]?.Value, out var w) ? w : 0,
            int.TryParse(size?.Attributes?["Height"]?.Value, out var h) ? h : 0,
            int.TryParse(layer?.Attributes["TileSize"]?.Value ?? "256", out var tileSize) ? tileSize : 256,
            layer?.Attributes["Format"]?.Value ?? "jpg"
        );
    }

    public static (Point tiles, int diviser) GetTilesNumber(Frame page, double multiplier)
    {
        var diviser = Math.Pow(2, CalculateIndex(page) - multiplier);
        return (new Point(NbTiles(page.Width!.Value), NbTiles(page.Height!.Value)), (int)diviser);
        int NbTiles(int val) => (int)Math.Ceiling(val / diviser / page.TileSize.GetValueOrDefault(256));
    }
}
