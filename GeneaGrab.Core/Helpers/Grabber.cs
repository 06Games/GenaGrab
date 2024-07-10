using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Serilog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace GeneaGrab.Core.Helpers;

public static class Grabber
{
    public static async Task<Image> GetImage(string url, HttpClient client = null)
    {
        client ??= new HttpClient();
        try { return await Image.LoadAsync(await client.GetStreamAsync(url).ConfigureAwait(false)).ConfigureAwait(false); }
        catch (HttpRequestException e)
        {
            Log.Error(e, "Failed to retrieve image at {Url}", url);
            return new Image<Rgb24>(1, 1, Color.Black);
        }
    }
    public static Image MergeTile(this Image tex, Image tile, (int tileSize, int scale, Point pos) a) => MergeTile(tex, tile, a.tileSize, a.scale, a.pos);
    public static Image MergeTile(this Image tex, Image tile, int tileSize, int scale, Point pos)
    {
        if (tile is null)
        {
            Log.Warning("The tile at {Position} is null", pos);
            return tex;
        }
        tile.Mutate(x => x.Resize(tile.Width * scale, tile.Height * scale));
        var point = new Point(pos.X * tileSize * scale, pos.Y * tileSize * scale);
        tex.Mutate(x => x.DrawImage(tile, point, 1));
        return tex;
    }

    public static Stream ToStream(this Image image)
    {
        var ms = new MemoryStream();
        image.Save(ms, new BmpEncoder());
        ms.Seek(0, SeekOrigin.Begin);
        return ms;
    }
}
