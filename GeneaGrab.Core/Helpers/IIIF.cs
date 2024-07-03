using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using GeneaGrab.Core.Models;
using SixLabors.ImageSharp;

namespace GeneaGrab.Core.Helpers;

public abstract class Iiif : Provider
{
    protected HttpClient HttpClient { get; }
    protected Iiif(HttpClient client) => HttpClient = client ?? new HttpClient();




    #region Image download

    protected virtual string GetRequestImageSize(ref Scale scale, Frame page) => scale switch
    {
        Scale.Thumbnail => "!512,512",
        Scale.Navigation => "!2048,2048",
        _ => "max"
    };

    protected static Uri ImageGeneratorRequestUri(string imageURL, string region = "full", string size = "max", string rotation = "0", string quality = "default", string format = "jpg")
        => new($"{imageURL}/{region}/{size}/{rotation}/{quality}.{format}");
    protected virtual Uri GetImageRequestUri(Frame page, Scale scale) => ImageGeneratorRequestUri(page.DownloadUrl, size: GetRequestImageSize(ref scale, page));

    public override async Task<Stream> GetFrame(Frame page, Scale scale, Action<Progress> progress)
    {
        var stream = await Data.TryGetImageFromDrive(page, scale);
        if (stream != null) return stream;

        progress?.Invoke(Progress.Unknown);

        var image = await Image
            .LoadAsync(await HttpClient.GetStreamAsync(GetImageRequestUri(page, scale)).ConfigureAwait(false))
            .ConfigureAwait(false);
        page.ImageSize = scale;
        progress?.Invoke(Progress.Finished);

        await Data.SaveImage(page, image, false);
        return image.ToStream();
    }

    #endregion
}
