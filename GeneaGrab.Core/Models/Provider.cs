﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GeneaGrab
{
    /// <summary>Interface to communicate with the registry provider</summary>
    public interface ProviderAPI
    {
        bool TryGetRegistryID(Uri URL, out RegistryInfo info);
        Task<RegistryInfo> Infos(Uri URL);
        Task<SixLabors.ImageSharp.Image> Thumbnail(Registry Registry, RPage page, Action<Progress> progress);
        Task<SixLabors.ImageSharp.Image> Preview(Registry Registry, RPage page, Action<Progress> progress);
        Task<SixLabors.ImageSharp.Image> Download(Registry Registry, RPage page, Action<Progress> progress);
        Task<string> Ark(Registry Registry, RPage page);

        bool IndexSupport { get; }
    }
    public interface IndexAPI
    {
        Task<IEnumerable<Index>> GetIndex(Registry Registry, RPage page);
        Task AddIndex(Registry Registry, RPage page, Index index);
    }

    /// <summary>Data on the registry provider</summary>
    public class Provider : IEquatable<Provider>
    {
        public Provider(ProviderAPI api) => API = api;
        public Provider(ProviderAPI api, string id)
        {
            ID = id;
            Name = Data.Translate($"Provider/{ID}", ID);
            Icon = $"/Assets/Providers/{ID}.png";
            API = api;
        }

        public string ID { get; set; }
        public string URL { get; set; }
        public string Name { get; set; }
        public ProviderAPI API { get; set; }
        public override string ToString() => Name;

        public string Icon { get; set; }

        public Dictionary<string, Registry> Registries { get; } = new Dictionary<string, Registry>();
        public string RegisterCount
        {
            get
            {
                var count = Registries.Count;
                if (count == 0) return "Aucun registre";
                else if (count == 1) return "1 registre";
                else return $"{count} registres";
            }
        }


        public bool Equals(Provider other) => ID == other.ID;
        public override bool Equals(object obj) => Equals(obj as Provider);
        public static bool operator ==(Provider one, Provider two) => one?.ID == two?.ID;
        public static bool operator !=(Provider one, Provider two) => !(one == two);
        public override int GetHashCode() => ID.GetHashCode();
    }


    public class Progress
    {
        public static readonly Progress Finished = new Progress { Value = 1, Done = true };
        public static readonly Progress Unknown = new Progress { Undetermined = true };
        private Progress() { }

        public static implicit operator Progress(int v) => new Progress(v);
        public static implicit operator Progress(float v) => new Progress(v);
        public static implicit operator Progress(decimal v) => new Progress((float)v);
        public Progress(float value) => Value = value;

        public static implicit operator float(Progress p) => p.Value;
        public float Value { get; private set; }
        public bool Done { get; private set; }
        public bool Undetermined { get; private set; }
    }
}
