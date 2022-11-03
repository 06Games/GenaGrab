﻿using GeneaGrab.Providers;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace GeneaGrab
{
    public class RegistryInfo : IEquatable<RegistryInfo>
    {
        public RegistryInfo() { PageNumber = Registry?.Pages?.FirstOrDefault()?.Number ?? 1; }
        public RegistryInfo(Registry r)
        {
            ProviderID = r.ProviderID;
            RegistryID = r.ID;
            PageNumber = Registry?.Pages?.FirstOrDefault()?.Number ?? 1;
        }

        public string ProviderID;
        public Provider Provider => ProviderID is null ? null : (Data.Providers.TryGetValue(ProviderID, out var p) ? p : null);
        public string RegistryID;
        public Registry Registry => RegistryID is null ? null : (Provider.Registries.TryGetValue(RegistryID, out var r) ? r : null);
        public int PageNumber;
        public int PageIndex => Array.IndexOf(Registry.Pages, Page);
        public RPage Page => GetPage(PageNumber);
        public RPage GetPage(int number) => Registry.Pages.FirstOrDefault(page => page.Number == number);


        public bool Equals(RegistryInfo other) => ProviderID == other?.ProviderID && RegistryID == other?.RegistryID;
        public override bool Equals(object obj) => Equals(obj as RegistryInfo);
        public static bool operator ==(RegistryInfo one, RegistryInfo two) => one?.ProviderID == two?.ProviderID && one?.RegistryID == two?.RegistryID;
        public static bool operator !=(RegistryInfo one, RegistryInfo two) => !(one == two);
        public override int GetHashCode() => (ProviderID + RegistryID).GetHashCode();
    }

    public static class Data
    {
        public static Func<string, string, string> Translate { get; set; } = (id, fallback) => fallback;
        public static Func<Registry, RPage, bool, Task<Stream>> GetImage { get; set; } = (r, p, t) => Task.CompletedTask as Task<Stream>;
        public static Func<Registry, RPage, Image, bool, Task<string>> SaveImage { get; set; } = (r, p, i, t) => Task.CompletedTask as Task<string>;
        public static Action<string, Exception> Log { get; set; } = (l, d) => System.Diagnostics.Debug.WriteLine($"{l}: {d}");
        public static Action<string, Exception> Warn { get; set; } = (l, d) => System.Diagnostics.Debug.WriteLine($"{l}: {d}");
        public static Action<string, Exception> Error { get; set; } = (l, d) => System.Diagnostics.Debug.WriteLine($"{l}: {d}");

        private static ReadOnlyDictionary<string, Provider> _providers;
        public static ReadOnlyDictionary<string, Provider> Providers
        {
            get
            {
                if (_providers != null) return _providers;

                var providers = new List<Provider>
                {
                    // France
                    new Provider(new Geneanet(), "Geneanet") { URL = "https://www.geneanet.org/" },
                    new Provider(new AD06(), "AD06") { URL = "https://www.departement06.fr/archives-departementales/outils-de-recherche-et-archives-numerisees-2895.html" },
                    new Provider(new CG06(), "CG06") { URL = "https://www.departement06.fr/archives-departementales/outils-de-recherche-et-archives-numerisees-2895.html" },
                    new Provider(new NiceHistorique(), "NiceHistorique") { URL = "http://www.nicehistorique.org/" },
                    new Provider(new AD17(), "AD17") { URL = "https://www.archinoe.net/v2/ad17/registre.html" },
                    new Provider(new AD79_86(), "AD79-86") { URL = "https://archives-deux-sevres-vienne.fr/" },
                    //TODO: Gironde and Cantal

                    // Italy
                    new Provider(new Antenati(), "Antenati") { URL = "https://www.antenati.san.beniculturali.it/" },
                };
                return _providers = new ReadOnlyDictionary<string, Provider>(providers.ToDictionary(k => k.ID, v => v));
            }
        }



        public static void AddOrUpdate<T>(Dictionary<string, T> dic, string key, T obj)
        {
            if (dic.ContainsKey(key)) dic[key] = obj;
            else dic.Add(key, obj);
        }
        public static async Task<(bool success, Stream stream)> TryGetThumbnailFromDrive(Registry registry, RPage current)
        {
            var image = await GetImage(registry, current, true).ConfigureAwait(false);
            if (image != null) return (true, image);

            var (success, stream) = await TryGetImageFromDrive(registry, current, 0);
            if (!success) return (false, null);
            
            await SaveImage(registry, current, await Image.LoadAsync(stream).ConfigureAwait(false), true);
            return (true, stream);
        }
        public static async Task<(bool success, Stream stream)> TryGetImageFromDrive(Registry registry, RPage current, double zoom)
        {
            if (zoom > current.Zoom) return (false, null);

            var image = await GetImage(registry, current, false).ConfigureAwait(false);
            return image != null ? (true, image) : (false, null);
        }
    }
}
