using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GeneaGrab.Core.Models;
using SixLabors.ImageSharp;

namespace GeneaGrab.Core.Helpers;

public abstract class Iiif : Provider
{
    protected HttpClient HttpClient { get; }
    protected Iiif(HttpClient client) => HttpClient = client ?? new HttpClient();

    protected abstract Task<(Registry registry, int sequence, object page)> ParseUrl(Uri url);

    protected virtual IIiifManifest ParseManifest(string stringAsync) => new IiifManifest(stringAsync);

    protected virtual Frame CreateFrame(IIiifCanvas p, int i)
    {
        var img = p.Images.FirstOrDefault();
        return new Frame
        {
            FrameNumber = int.TryParse(p.Label, out var number) ? number : i + 1,
            DownloadUrl = img?.ServiceId,
            Width = img?.Width,
            Height = img?.Height
        };
    }

    protected virtual void ParseMetaData(ref Registry registry, string key, string value)
    {
        if (!string.IsNullOrEmpty(registry.Notes)) registry.Notes += "\n";
        registry.Notes += $"{key}: {value}";
    }

    protected virtual void ReadAllMetadata(ref Registry registry, ReadOnlyDictionary<string, string> manifestMetadata)
    {
        foreach (var metadata in manifestMetadata)
        {
            var value = Regex.Replace(metadata.Value, "<[^>]*>", ""); // Remove HTML tags
            ParseMetaData(ref registry, metadata.Key, value);
        }
    }

    protected virtual Task ReadSequence(Registry registry, IIiifSequence sequence) => Task.CompletedTask;

    protected abstract int GetPage(Registry registry, object page);

    public override async Task<(Registry, int)> Infos(Uri url)
    {
        var (registry, sequenceIndex, page) = await ParseUrl(url);

        var manifest = ParseManifest(await HttpClient.GetStringAsync($"{registry.URL}/manifest"));
        var sequence = manifest.Sequences.Length > sequenceIndex ? manifest.Sequences[sequenceIndex] : manifest.Sequences[0];

        registry.Frames = sequence.Canvases.Select(CreateFrame).ToArray();

        ReadAllMetadata(ref registry, manifest.MetaData);
        await ReadSequence(registry, sequence);

        return (registry, GetPage(registry, page));
    }

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
