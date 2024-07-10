using System;
using System.IO;
using System.Threading.Tasks;

namespace GeneaGrab.Core.Models;

/// <summary>Registry provider</summary>
public abstract class Provider : IEquatable<Provider>
{
    public abstract string Id { get; }
    public abstract string Url { get; }
    public override string ToString() => Id;


    public abstract Task<RegistryInfo> GetRegistryFromUrlAsync(Uri url);
    public abstract Task<(Registry registry, int pageNumber)> Infos(Uri url);
    public abstract Task<Stream> GetFrame(Frame page, Scale scale, Action<Progress> progress);
    public abstract Task<string> Ark(Frame page);


    public bool Equals(Provider other) => Id == other?.Id;
    public override bool Equals(object obj) => Equals(obj as Provider);
    public static bool operator ==(Provider one, Provider two) => one?.Id == two?.Id;
    public static bool operator !=(Provider one, Provider two) => !(one == two);
    public override int GetHashCode() => Id.GetHashCode();

    public bool NeedsAuthentication(string method)
    {
        var property = GetType().GetMethod(method);
        return property is not null && Attribute.IsDefined(property, typeof(AuthentificationNeededAttribute));
    }
}
