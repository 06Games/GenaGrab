#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GeneaGrab.Core.Helpers;
using GeneaGrab.Core.Providers;
using Serilog;
using SixLabors.ImageSharp;

namespace GeneaGrab.Core.Models;

public static class Data
{
    public static Func<Frame, bool, Stream?> GetImage { get; set; } = (_, _) => null;
    public static Func<Frame, Image, bool, Task<string?>> SaveImage { get; set; } = (_, _, _) => (Task<string?>)Task.CompletedTask;
    public static Func<Image, Task<Image>> ToThumbnail { get; set; } = Task.FromResult;
    public static void SetLogger(ILogger value) => Log.Logger = value;

    private static ReadOnlyDictionary<string, Provider>? _providers;
    public static ReadOnlyDictionary<string, Provider> Providers
    {
        get
        {
            if (_providers != null) return _providers;

            var providers = new List<Provider>
            {
                // France
                new ANFR(),
                new Geneanet(),
                new AD06(),
                new Nice(),
                new NiceHistorique(),
                new AD17(),
                new AD79_86(),

                // Italy
                new Antenati(),

                // Worldwide
                new FamilySearch()
            };
            return _providers = new ReadOnlyDictionary<string, Provider>(providers.ToDictionary(k => k.Id, v => v));
        }
    }

    public static async Task<Stream?> TryGetImageFromDrive(Frame frame, Scale zoom)
    {
        if (zoom > frame.ImageSize) return null;
        if (zoom > Scale.Thumbnail) return GetImage(frame, false);


        var image = GetImage(frame, true);
        if (image != null) return image;

        var stream = await TryGetImageFromDrive(frame, Scale.Navigation);
        if (stream is null) return null;

        var thumb = await ToThumbnail(await Image.LoadAsync(stream).ConfigureAwait(false)).ConfigureAwait(false);
        await SaveImage(frame, thumb, true);
        return thumb.ToStream();
    }
}
