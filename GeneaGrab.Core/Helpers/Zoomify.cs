﻿using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
using GeneaGrab.Core.Models;
using SixLabors.ImageSharp;

namespace GeneaGrab.Core.Helpers
{
    public static class Zoomify
    {
        /// <summary>Returns the maximum zoom level</summary>
        // Each zoom level multiplies the size of the image by two. The zoom level 0 corresponds to the entire image contained in a single tile.
        // ReSharper disable once PossibleLossOfFraction
        public static int CalculateIndex(RPage page) => Math.Max((int)Math.Ceiling(Math.Log(Math.Max(page.Width, page.Height) / (double)page.TileSize.GetValueOrDefault(256)) / Math.Log(2)), 0);

        /// <summary>Returns the properties of the image</summary>
        /// <param name="baseURL">Image root url</param>
        /// <param name="client">HTTP client to use</param>
        public static async Task<(int w, int h, int tileSize)> ImageData(string baseURL, HttpClient client)
        {
            var data = await client.GetStringAsync($"{baseURL}ImageProperties.xml");
            var dataResp = new XmlDocument();
            if (!string.IsNullOrEmpty(data)) dataResp.LoadXml($"<r>{data}</r>");
            var layer = dataResp.DocumentElement?.SelectSingleNode("IMAGE_PROPERTIES");

            return (
                int.TryParse(layer?.Attributes?["WIDTH"]?.Value, out var w) ? w : 0,
                int.TryParse(layer?.Attributes?["HEIGHT"]?.Value, out var h) ? h : 0,
                int.TryParse(layer?.Attributes?["TILESIZE"]?.Value, out var tileSize) ? tileSize : 0
            );
        }

        public static (Point tiles, int diviser) GetTilesNumber(RPage page, double multiplier)
        {
            var diviser = Math.Pow(2, CalculateIndex(page) - multiplier);
            int NbTiles(int val) => (int)Math.Ceiling(val / diviser / page.TileSize.GetValueOrDefault(256));
            return (new Point(NbTiles(page.Width), NbTiles(page.Height)), (int)diviser);
        }
    }
}
